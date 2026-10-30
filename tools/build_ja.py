"""
Caves of Qud 公式ローカライズ基盤 (lang-experimental) 向けの日本語パックを生成する。

入力:
  - ゲームの StreamingAssets/Base/ExampleLanguage/*.example.xml  (翻訳対象の一覧。訳文は "▶英語")
  - ゲームの StreamingAssets/Base/Conversations.xml など         (会話の英語原文)
  - QudJP (Steam ワークショップ, MIT License) の Localization    (既存の日本語訳)
  - tools/overrides/*.xml                                          (手作業・追加の訳。最優先)

出力: dist/QudJA/ (そのまま Mods フォルダへコピーできる MOD)
      dist/report/ (未訳の一覧など)

使い方: python -I tools/build_ja.py [--game <CoQ_Data/StreamingAssets/Base>] [--qudjp <QudJP folder>]
"""
import argparse
import copy
import glob
import json
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)

DEFAULT_GAME = r"D:\Program Files (x86)\Steam\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base"
DEFAULT_QUDJP = r"D:\Program Files (x86)\Steam\steamapps\workshop\content\333640\3718988020"

MARK = "\u25b6"  # ▶ 見本ファイルで訳文を示す印
JA = re.compile(r"[\u3040-\u30ff\u3400-\u9fff\uff01-\uff9f]")
# データ XML の要素照合に使う属性（見本の冒頭コメントで Key とされているもの）
KEY_NAMES = ["Name", "ID", "Command", "Class", "Part", "Tags", "WillBuy", "WillSell",
             "MatchAny", "Inverse", "Level", "x", "y", "name", "Type"]
IGNORE_ATTRS = {"Load", "Replace", "Lang", "Encoding", "LoadPriority"}
# 生成しないファイル（照合が順番頼みで不安定なもの）
SKIP_DATA = {"Naming.example.xml"}


def has_ja(s):
    return bool(s) and bool(JA.search(s))


VAR = re.compile(r"=[A-Za-z_][^=\s]*=")
REJECTED = []  # 変数が壊れていて採用しなかった訳


# 旧バージョン向けの書式・全角化で壊れた変数
BROKEN = re.compile(r"＝[A-Za-z_.']+＝|<spice|\*\w*Name\*|\*[A-Za-z]+\*")


def var_names(s):
    # =ifplayerplural:君たち:そなた= のように引数は訳されることがあるので名前部分だけ比べる
    return {re.split(r"[:|]", t.strip("="))[0] for t in VAR.findall(s)}


def vars_ok(english, japanese, where=""):
    """訳文の変数が今のゲームで使えるか。
    代名詞などの変数を日本語で省くのは自然なので許可し、
    全角化・旧書式・英語原文に無い変数を含む訳だけを不採用にする。"""
    english = english or ""
    japanese = japanese or ""
    bad = False
    if BROKEN.search(japanese) and not BROKEN.search(english):
        bad = True
    else:
        if var_names(japanese) - var_names(english):
            bad = True
    if bad:
        REJECTED.append((where, english, japanese))
        return False
    return True


def is_tr(v):
    return v is not None and v.lstrip().startswith(MARK)


def strip_mark(s):
    s = s.lstrip()
    return s[1:] if s.startswith(MARK) else s


def norm_lines(s):
    """会話ローダーと同じ正規化: 前後の空白除去・各行のインデント除去"""
    return "\n".join(line.strip() for line in s.strip().splitlines()).strip()


def norm_key(s):
    s = re.sub(r"\{\{[^|}]*\|", "", s).replace("}}", "")
    s = re.sub(r"&[A-Za-z]|\^[A-Za-z]", "", s)
    return s.strip().lower()


def load_xml(path):
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as e:
        print(f"  ! XML parse error {path}: {e}", file=sys.stderr)
        return None


# ---------------------------------------------------------------- QudJP 辞書

def load_qudjp_dict(qudjp):
    d = {}
    for f in glob.glob(os.path.join(qudjp, "Localization", "Dictionaries", "**", "*.json"), recursive=True):
        try:
            j = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        if not isinstance(j, dict):
            continue
        for e in j.get("entries", []):
            if isinstance(e, dict) and isinstance(e.get("key"), str) and has_ja(e.get("text")):
                d.setdefault(e["key"], e["text"])
    return d


def load_overrides():
    """tools/overrides/*.xml の <string Context ID>訳</string> を読み込む"""
    by_ctx, by_id = {}, {}
    for f in sorted(glob.glob(os.path.join(HERE, "overrides", "*.xml"))):
        root = load_xml(f)
        if root is None:
            continue
        for s in root.iter("string"):
            sid = s.get("ID")
            val = s.get("Value") if s.get("Value") is not None else (s.text or "")
            if sid is None:
                continue
            by_ctx[(s.get("Context"), sid)] = val
            by_id.setdefault(sid, val)
    return by_ctx, by_id


# ---------------------------------------------------------------- 会話

def conv_texts(el):
    """要素直下の <text> を変種ごとのリストに展開（入れ子 <text> と '~' 区切りの両方に対応）"""
    out = []
    for t in el.findall("text"):
        inner = t.findall("text")
        if inner:
            out.extend(conv_texts(t))
        else:
            raw = "".join(t.itertext())
            for part in raw.split("~"):
                n = norm_lines(part)
                if n:
                    out.append(n)
    return out


def build_conversation_map(game, qudjp):
    pairs = {}

    def walk(b, q):
        bt, qt = conv_texts(b), conv_texts(q)
        if len(bt) == len(qt):
            for e, j in zip(bt, qt):
                if has_ja(j):
                    pairs.setdefault(e, j)
        qidx = {}
        for c in q:
            if c.get("ID") is not None:
                qidx.setdefault((c.tag, c.get("ID")), c)
        for c in b:
            if c.tag in ("conversation", "node", "choice", "start") and c.get("ID") is not None:
                qc = qidx.get((c.tag, c.get("ID")))
                if qc is not None:
                    walk(c, qc)

    for name in ("Conversations", "HiddenConversations"):
        b = load_xml(os.path.join(game, name + ".xml"))
        q = load_xml(os.path.join(qudjp, "Localization", name + ".jp.xml"))
        if b is not None and q is not None:
            walk(b, q)
    return pairs


# ---------------------------------------------------------------- strings 表

def translate_string(ctx, sid, overrides, qdict, qnorm, extra):
    by_ctx, by_id = overrides
    if (ctx, sid) in by_ctx:
        return by_ctx[(ctx, sid)]
    if sid in by_id:
        return by_id[sid]
    val = lookup_qudjp(sid, qdict, qnorm, extra)
    if val is not None and vars_ok(sid, val, ctx or ""):
        return val
    return None


def lookup_qudjp(sid, qdict, qnorm, extra):
    if sid in qdict:
        return qdict[sid]
    if extra:
        n = norm_lines(sid)
        if n in extra:
            return extra[n]
        if "~" in n:
            parts = [extra.get(norm_lines(p)) for p in n.split("~")]
            if all(parts):
                return "~".join(parts)
    # 色指定などを含まない文だけ、表記ゆれを無視して照合
    if norm_key(sid) == sid.strip().lower() and norm_key(sid) in qnorm:
        return qnorm[norm_key(sid)]
    return None


def build_strings(example_path, out_path, overrides, qdict, qnorm, extra, report):
    root = load_xml(example_path)
    out = ET.Element("strings", {"Lang": "ja", "Encoding": "utf-8"})
    total = done = 0
    missing = []
    for s in root.iter("string"):
        sid = s.get("ID")
        ctx = s.get("Context")
        total += 1
        val = translate_string(ctx, sid, overrides, qdict, qnorm, extra)
        if val is None:
            missing.append((ctx or "", sid))
            continue
        attrs = {}
        if ctx is not None:
            attrs["Context"] = ctx
        attrs["ID"] = sid
        e = ET.SubElement(out, "string", attrs)
        e.text = val
        done += 1
    write_xml(out, out_path)
    report.append((os.path.basename(example_path), done, total))
    return missing


# ---------------------------------------------------------------- データ XML

def key_attrs(el):
    named = {k: v for k, v in el.attrib.items() if k in KEY_NAMES and not is_tr(v)}
    if named:
        return named
    return {k: v for k, v in el.attrib.items() if k not in IGNORE_ATTRS and not is_tr(v)}


class Index:
    """同じタグ・キー属性の要素をすばやく探すための索引"""

    def __init__(self, children):
        self.children = list(children)
        self.by_tag = {}
        for c in self.children:
            self.by_tag.setdefault(c.tag, []).append(c)
        self.cache = {}

    def find(self, ex, ordinal):
        ka = key_attrs(ex)
        same = self.by_tag.get(ex.tag, [])
        if not ka:
            return same[ordinal] if ordinal < len(same) else None
        sig = (ex.tag, tuple(sorted(ka)))
        idx = self.cache.get(sig)
        if idx is None:
            idx = {}
            for c in same:
                idx.setdefault(tuple(c.get(k) for k in sorted(ka)), c)
            self.cache[sig] = idx
        found = idx.get(tuple(ka[k] for k in sorted(ka)))
        if found is None and "ID" in ka:
            # 新版で ID が付いた要素（クエストなど）は、旧版の Name と照合する
            for c in same:
                if c.get("Name") == ka["ID"]:
                    return c
        return found


QDICT = {}  # 変異の説明文などデータ XML の補助に使う QudJP 辞書


def convert_element(ex, q, stats, owner=None):
    """見本要素 ex と QudJP 側の対応要素 q から、日本語訳だけを持つ要素を作る。訳が無ければ None"""
    out = ET.Element(ex.tag, {k: v for k, v in ex.attrib.items() if not is_tr(v)})
    has = False
    for k, v in ex.attrib.items():
        if is_tr(v):
            stats[1] += 1
            qv = q.get(k) if q is not None else None
            if has_ja(qv) and vars_ok(strip_mark(v), qv, f"{ex.tag} {k}"):
                out.set(k, qv)
                stats[0] += 1
                has = True
    text_tr = ex.text is not None and is_tr(ex.text)
    if text_tr:
        stats[1] += 1
        english = strip_mark(ex.text) + "".join(ET.tostring(c, encoding="unicode") for c in ex)
        done = False
        if q is not None:
            inner = (q.text or "") + "".join(ET.tostring(c, encoding="unicode") for c in q)
            if has_ja(inner) and vars_ok(english, inner, ex.tag):
                out.text = q.text
                for c in q:
                    out.append(copy.deepcopy(c))
                done = True
        if not done and ex.tag == "description" and owner:
            jt = QDICT.get("mutation:" + owner)
            if has_ja(jt) and vars_ok(english, jt, "mutation " + owner):
                out.text = jt
                done = True
        if done:
            stats[0] += 1
            has = True
        if len(ex):  # 混在コンテンツ（<p> など）は丸ごと扱ったので子は見ない
            return out if has else None
    qi = Index(q) if q is not None else None
    ordinals = {}
    for c in ex:
        n = ordinals.get(c.tag, 0)
        ordinals[c.tag] = n + 1
        qc = qi.find(c, n) if qi is not None else None
        r = convert_element(c, qc, stats, ex.get("Name") or owner)
        if r is not None:
            out.append(r)
            has = True
    return out if has else None


def load_qudjp_roots(qudjp):
    roots = {}
    for f in glob.glob(os.path.join(qudjp, "Localization", "**", "*.xml"), recursive=True):
        r = load_xml(f)
        if r is not None:
            roots.setdefault(r.tag, []).append(r)
    return roots


def build_data(example_path, out_path, qroots, report):
    ex = load_xml(example_path)
    if ex is None:
        return
    merged = ET.Element(ex.tag)
    for r in qroots.get(ex.tag, []):
        merged.extend(list(r))
    stats = [0, 0]
    out = convert_element(ex, merged, stats)
    report.append((os.path.basename(example_path), stats[0], stats[1]))
    if out is None:
        return
    out.set("Lang", "ja")
    out.set("Encoding", "utf-8")
    write_xml(out, out_path)


# ---------------------------------------------------------------- 出力

def write_xml(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ET.indent(root, space="  ")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n')
        f.write(ET.tostring(root, encoding="unicode"))
        f.write("\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game", default=DEFAULT_GAME)
    ap.add_argument("--qudjp", default=DEFAULT_QUDJP)
    ap.add_argument("--out", default=os.path.join(REPO, "dist"))
    a = ap.parse_args()

    example = os.path.join(a.game, "ExampleLanguage")
    if not os.path.isdir(example):
        # ゲームが通常版に戻っている場合は、保存しておいた見本（reference/）を使う
        refs = sorted(glob.glob(os.path.join(REPO, "reference", "lang-experimental-*")))
        if not refs:
            sys.exit(f"ExampleLanguage が見つかりません: {example}\nlang-experimental ブランチに切り替えてください。")
        a.game = refs[-1]
        example = os.path.join(a.game, "ExampleLanguage")
        print(f"ゲームに ExampleLanguage が無いため、保存版を使います: {a.game}")

    mod_out = os.path.join(a.out, "QudJA")
    rep_out = os.path.join(a.out, "report")
    for d in (mod_out, rep_out):
        if os.path.isdir(d):
            shutil.rmtree(d)
    shutil.copytree(os.path.join(REPO, "QudJA"), mod_out)
    loc_out = os.path.join(mod_out, "Localization")

    print("QudJP 辞書を読み込み中...")
    qdict = load_qudjp_dict(a.qudjp)
    QDICT.update(qdict)
    qnorm = {}
    for k, v in qdict.items():
        qnorm.setdefault(norm_key(k), v)
    overrides = load_overrides()
    print(f"  辞書 {len(qdict)} 件 / 手動訳 {len(overrides[1])} 件")

    print("会話の対応表を作成中...")
    conv_map = build_conversation_map(a.game, a.qudjp)
    print(f"  会話 {len(conv_map)} 件")

    report = []
    missing_all = []
    for name in ("Strings.example.xml", "Strings.appendix.example.xml", "Strings.Conversations.example.xml"):
        p = os.path.join(example, name)
        if os.path.exists(p):
            extra = conv_map if "Conversations" in name else None
            miss = build_strings(p, os.path.join(loc_out, name.replace(".example.", ".ja.")),
                                 overrides, qdict, qnorm, extra, report)
            missing_all += [(name, c, i) for c, i in miss]

    print("データ XML を変換中...")
    qroots = load_qudjp_roots(a.qudjp)
    for p in sorted(glob.glob(os.path.join(example, "*.example.xml"))):
        name = os.path.basename(p)
        if name.startswith("Strings") or name in SKIP_DATA:
            continue
        build_data(p, os.path.join(loc_out, name.replace(".example.", ".ja.")), qroots, report)

    shutil.copy(os.path.join(a.qudjp, "LICENSE"), os.path.join(mod_out, "LICENSE-QudJP.txt"))

    os.makedirs(rep_out, exist_ok=True)
    with open(os.path.join(rep_out, "coverage.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("file\ttranslated\ttotal\n")
        for n, d, t in report:
            f.write(f"{n}\t{d}\t{t}\n")
    with open(os.path.join(rep_out, "untranslated_strings.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("file\tContext\tID\n")
        for n, c, i in missing_all:
            f.write(f"{n}\t{c}\t{i.replace(chr(10), '\\n')}\n")
    with open(os.path.join(rep_out, "rejected_broken_vars.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("where\tenglish\tjapanese\n")
        for w, e, j in REJECTED:
            f.write("\t".join(x.replace("\n", "\\n").replace("\t", " ") for x in (w, e, j)) + "\n")
    print(f"変数が壊れていて不採用にした訳: {len(REJECTED)} 件 (report/rejected_broken_vars.tsv)")

    print("\n訳文カバー率:")
    td = tt = 0
    for n, d, t in report:
        td += d
        tt += t
        print(f"  {n:<40} {d:>6} / {t:<6} ({(d * 100 // t) if t else 0}%)")
    print(f"  {'合計':<40} {td:>6} / {tt:<6} ({td * 100 // tt}%)")
    print(f"\n出力: {mod_out}")


if __name__ == "__main__":
    main()
