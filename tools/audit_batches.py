"""
訳文が別の英語行に結び付いていないか（番号ずれ）を、記号の形で簡易チェックする。
使い方: python -I tools/audit_batches.py [batch番号...]
"""
import csv
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TR = os.path.join(HERE, "translate")
VAR = re.compile(r"=[A-Za-z_][\w.\[\]#'|]*(?::[^=\n]*)?=")


def shape(s):
    s = VAR.sub("V", s)
    f = set()
    t = s.strip()
    if t.startswith("[") or t.startswith("{{y|["):
        f.add("[start")
    if "*" in t:
        f.add("*")
    if t.endswith(("?", "？")) or t.endswith(("?)", "？）")):
        f.add("?")
    if "\\n" in t:
        f.add("nl")
    if "|" in t.replace("{{", "").replace("}}", ""):
        f.add("pipe")
    if t.startswith("V") and t.endswith("V"):
        f.add("allvar") if VAR.sub("", t).strip() == "" else None
    for m in re.findall(r"[A-Za-z]{2,}", VAR.sub("", s)):
        f.add("ascii")
        break
    return f


def main():
    want = set(sys.argv[1:])
    for ja in sorted(glob.glob(os.path.join(TR, "batch_*.ja.tsv"))):
        num = os.path.basename(ja)[6:9]
        if want and num not in want and str(int(num)) not in want:
            continue
        src = {r["No"]: r for r in csv.DictReader(open(ja.replace(".ja.tsv", ".src.tsv"), encoding="utf-8"),
                                                    delimiter="\t", quoting=csv.QUOTE_NONE)}
        for line in open(ja, encoding="utf-8"):
            line = line.rstrip("\n")
            if "\t" not in line:
                continue
            no, jp = line.split("\t", 1)
            r = src.get(no)
            if not r:
                continue
            en = r["ID"]
            se, sj = shape(en), shape(jp)
            se.discard("ascii")
            # 英語側の形の特徴が訳に無い/訳だけにある
            diff = (se ^ sj) - {"ascii"}
            if "ascii" in sj and "ascii" not in shape(en):
                diff.add("ascii-only-in-ja")
            if diff:
                print(f"{num}:{no}\t{sorted(diff)}\tEN: {en[:70]}\n\t\tJA: {jp[:50]}")


if __name__ == "__main__":
    main()
