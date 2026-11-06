"""
会話の自作訳を管理するツール。

自作訳は tools/conv/*.txt に、次のブロック形式で書く:

    ### Angohind | node:Acceptance | text#0
    > Oh, truly? You won't regret this, kendren, I promise!     ← 英語原文（"> " で始まる行）
    ほんとに？ 後悔はさせないよ、ケンドレン、約束する！         ← 訳文（それ以外の行）

"###" の行が場所（キー）。会話ID | 要素の道筋 | text#番号。
要素の道筋は「タグ:ID#同名の何番目か」を " / " でつないだもの（ID がない選択肢は >GotoID）。

使い方:
  python -I tools/conv_tool.py extract <ゲームのBase/Conversations.xml> <出力.txt> <会話ID ...>
      英語原文から訳の下書きファイルを作る（訳文の欄には QudJP の訳を入れておく）。
      環境変数 QUDJP_CONV に QudJP の Conversations.jp.xml を指定すると、その訳を下書きに使う
  python -I tools/conv_tool.py apply <ゲームのBase/Conversations.xml> <QudJPのConversations.jp.xml> <tools/conv>
      QudJP の会話ファイルの文を自作訳で差し替える（ファイルを上書き）。
      英語原文が変わった訳は使わずに一覧を出す
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

CONTAINERS = ("conversation", "node", "choice", "start")


def norm(s):
    """行ごとの前後の空白を除き、先頭と末尾の空行を落とす（ゲームも行頭の字下げは表示しない）。"""
    lines = [l.strip() for l in (s or "").replace("\r", "").split("\n")]
    while lines and not lines[0]:
        lines.pop(0)
    while lines and not lines[-1]:
        lines.pop()
    return "\n".join(lines)


def label(el):
    if el.get("ID"):
        return f"{el.tag}:{el.get('ID')}"
    if el.get("GotoID"):
        return f"{el.tag}:>{el.get('GotoID')}"
    if el.get("Target"):
        return f"{el.tag}:@{el.get('Target')}"
    return f"{el.tag}:"


def units(root):
    """{キー: 要素, 'direct'|'text'} を返す。キーは '会話ID | 道筋 | text#n'"""
    out = {}

    def walk(el, conv, path):
        seen = {}
        texts = 0
        for ch in el:
            if ch.tag == "text":
                out[f"{conv} | {' / '.join(path) or '-'} | text#{texts}"] = ch
                texts += 1
            elif ch.tag in CONTAINERS:
                lb = label(ch)
                n = seen.get(lb, 0)
                seen[lb] = n + 1
                walk(ch, conv, path + [f"{lb}#{n}"])
        # 選択肢の直下に書かれた文（<choice GotoID="X">文</choice>）
        if el.tag == "choice" and texts == 0 and norm(el.text):
            out[f"{conv} | {' / '.join(path)} | direct"] = el

    # 同じ ID の会話が複数あるとき（BaseConversation など）は、2つ目以降を「ID#n」で区別する
    seen_conv = {}
    for c in root.iter("conversation"):
        cid = c.get("ID") or ""
        n = seen_conv.get(cid, 0)
        seen_conv[cid] = n + 1
        walk(c, cid if n == 0 else f"{cid}#{n}", [])
    return out


def get_text(el):
    return norm(el.text)


def set_text(el, ja):
    # 元の字下げを真似る（見た目のためだけ。ゲームは行頭の空白を無視する）
    orig = el.text or ""
    m = re.search(r"\n([ \t]*)\S", orig)
    if "\n" in ja or m:
        ind = m.group(1) if m else ""
        tail = re.search(r"\n[ \t]*$", orig)
        el.text = "\n" + "\n".join((ind + l) if l else "" for l in ja.split("\n")) + (tail.group(0) if tail else "\n")
    else:
        el.text = ja


def _attrs_for(lbl):
    if lbl.startswith(">"):
        return {"GotoID": lbl[1:]}
    if lbl.startswith("@"):
        return {"Target": lbl[1:]}
    return {"ID": lbl} if lbl else {}


def ensure(root, broot, key):
    """QudJP 側に無い要素を、英語原文と同じ位置（同じタグ・ID・同じIDの何番目か）に作って返す。
    ゲームは「タグ＋ID＋同じIDの順番」で会話を合成する。属性の有無で ID の扱いが変わる
    （属性つきの <text> は ID なし扱い）ので、足す要素には英語原文の同じ要素の属性を写す。
    中身のない要素は合成しても既存の内容を変えない。"""
    conv, path, last = key.split(" | ")
    cid, _, cn = conv.partition("#")
    ci = int(cn or 0)

    def convs_of(r):
        return [c for c in r.iter("conversation") if (c.get("ID") or "") == cid]

    bconvs = convs_of(broot)
    convs = convs_of(root)
    while len(convs) <= ci:
        convs.append(ET.SubElement(root, "conversation", {"ID": cid}))
    el, bel = convs[ci], bconvs[ci]

    def grow(parent, bparent, match, n):
        have = [c for c in parent if match(c)]
        bhave = [c for c in bparent if match(c)]
        while len(have) <= n:
            src = bhave[len(have)]
            have.append(ET.SubElement(parent, src.tag, dict(src.attrib)))
        return have[n], bhave[n]

    if path != "-":
        for seg in path.split(" / "):
            head, _, n = seg.rpartition("#")
            tag = head.partition(":")[0]
            el, bel = grow(el, bel, lambda c, t=tag, h=head: c.tag == t and label(c) == h, int(n))
    if last == "direct":
        return el
    k = int(last.split("#")[1])
    return grow(el, bel, lambda c: c.tag == "text", k)[0]


def read_blocks(path):
    """訳ファイルを読む。{キー: (英語, 訳)}"""
    res = {}
    key, en, ja = None, [], []

    def flush():
        if key:
            res[key] = ("\n".join(en), norm("\n".join(ja)))

    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n").rstrip("\r")
        if line.startswith("### "):
            flush()
            key, en, ja = line[4:].strip(), [], []
        elif line.startswith(">"):
            en.append(line[2:] if line.startswith("> ") else line[1:])
        elif key is not None:
            ja.append(line)
    flush()
    return {k: (norm(e), j) for k, (e, j) in res.items()}


def extract(base, out, ids):
    b = units(ET.parse(base).getroot())
    q = {}
    if os.environ.get("QUDJP_CONV"):
        q = units(ET.parse(os.environ["QUDJP_CONV"]).getroot())
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for k, el in b.items():
            if k.split(" | ")[0].split("#")[0] not in ids:
                continue
            en = get_text(el)
            if not re.search(r"[A-Za-z]", en):
                continue
            f.write(f"### {k}\n")
            for l in en.split("\n"):
                f.write(f"> {l}\n" if l else ">\n")
            ja = get_text(q[k]) if k in q else ""
            f.write((ja + "\n") if ja else "\n")
            f.write("\n")
    print(f"書き出し: {out}")


def apply(base, target, convdir):
    broot = ET.parse(base).getroot()
    b = units(broot)
    tree = ET.parse(target)
    t = units(tree.getroot())
    done = stale = missing = empty = 0
    for f in sorted(glob.glob(os.path.join(convdir, "*.txt"))):
        for k, (en, ja) in read_blocks(f).items():
            if not ja:
                empty += 1
                continue
            if k not in b or get_text(b[k]) != en:
                stale += 1
                print(f"  [原文が変わった/見つからない] {os.path.basename(f)}: {k}")
                continue
            if k in t:
                set_text(t[k], ja)
            else:
                # QudJP に無い場面（英語のまま表示されている所）は、同じ位置に要素を足す
                set_text(ensure(tree.getroot(), broot, k), ja)
                missing += 1
            done += 1
    tree.write(target, encoding="utf-8", xml_declaration=True)
    print(f"会話の自作訳: 適用 {done}（うち QudJP に無く追加 {missing}） / 原文変更で不採用 {stale} / 未訳 {empty}")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "extract":
        extract(sys.argv[2], sys.argv[3], set(sys.argv[4:]))
    elif cmd == "apply":
        apply(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        sys.exit(__doc__)
