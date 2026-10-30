"""
tools/translate/batch_NNN.ja.tsv（No<TAB>訳文）を、元の英語 (batch_NNN.src.tsv) と結び付けて
tools/overrides/batch_NNN.xml に書き出す。build_ja.py はこの overrides を最優先で使う。

検査: 英語原文にある =変数= の名前が訳文に無い、または英語に無い変数が訳文にある場合は警告して採用しない。
      ({{色|..}} の数が合わない場合も警告)

使い方: python -I tools/apply_batches.py
"""
import csv
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
TR = os.path.join(HERE, "translate")
OV = os.path.join(HERE, "overrides")
VAR = re.compile(r"=[A-Za-z_][\w.\[\]#'|]*(?::[^=\n]*)?=")
DROPPABLE = re.compile(r"(^pronouns|^verb|^useArticle|^withArticle|^ifplayerplural|^plural\.if|\.ifPlural|\.(?:[Tt]heirs?|[Ii]ts|[Ii]tself|[Tt]hem|[Tt]hemselves|[Ii]t|[Tt]hey|[Hh]im|[Hh]er|"
                       r"[Ss]ubjective|[Oo]bjective|[Pp]ossessive\w*|[Rr]eflexive|"
                       r"[Ii]s|[Ii]tis|[Hh]as|[Dd]oes|[Ww]as|[Aa]re|[Tt]he|[Aa]n?|t|s|es|ies|[Ii]ndicative\w*|[Pp]ersonTerm|"
                       r"[Ss]iblingTerm|[Ff]ormalAddressTerm|[Oo]ffspringTerm|[Pp]arentTerm)$)")


def names(s):
    return {re.split(r"[:|]", t.strip("="))[0] for t in VAR.findall(s)}


def unesc(s):
    return s.replace("\\n", "\n")


def main():
    os.makedirs(OV, exist_ok=True)
    total = bad = 0
    for ja in sorted(glob.glob(os.path.join(TR, "batch_*.ja.tsv"))):
        src = ja.replace(".ja.tsv", ".src.tsv")
        rows = {r["No"]: r for r in csv.DictReader(open(src, encoding="utf-8"), delimiter="\t", quoting=csv.QUOTE_NONE)}
        root = ET.Element("strings", {"Encoding": "utf-8"})
        for line in open(ja, encoding="utf-8"):
            line = line.rstrip("\n")
            if not line.strip() or line.startswith("#") or "\t" not in line:
                continue
            no, text = line.split("\t", 1)
            r = rows.get(no.strip())
            if r is None:
                print(f"  ! {os.path.basename(ja)} No={no}: 元の行がありません", file=sys.stderr)
                bad += 1
                continue
            en, jp = unesc(r["ID"]), unesc(text)
            # 代名詞・動詞活用・冠詞・be動詞などの変数は日本語では省いてよい
            # 同じ対象を別の形で残していれば（=subject.Does:x= → =subject.T=）省いてよい
            jroots = {n.split(".")[0] for n in names(jp)}
            miss = {n for n in names(en) - names(jp) if not DROPPABLE.search(n) and n.split(".")[0] not in jroots}
            # 同じ対象 (subject. / object. など) の別の形は使ってよい（例: =subject.Does:x= → =subject.T=は）
            roots = {n.split(".")[0] for n in names(en)}
            extra = {n for n in names(jp) - names(en) if n.split(".")[0] not in roots}
            if miss or extra or en.count("{{") != jp.count("{{"):
                print(f"  ! {os.path.basename(ja)} No={no}: 変数/色指定が合いません 不足={miss} 余分={extra}\n"
                      f"      EN: {en}\n      JA: {jp}", file=sys.stderr)
                bad += 1
                continue
            attrs = {"Context": r["Context"]} if r["Context"] else {}
            attrs["ID"] = en
            e = ET.SubElement(root, "string", attrs)
            e.text = jp
            total += 1
        out = os.path.join(OV, os.path.basename(ja).replace(".ja.tsv", ".xml"))
        ET.indent(root)
        with open(out, "w", encoding="utf-8", newline="\n") as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n')
            f.write(ET.tostring(root, encoding="unicode") + "\n")
    print(f"反映: {total} 件 / 不採用: {bad} 件")


if __name__ == "__main__":
    main()
