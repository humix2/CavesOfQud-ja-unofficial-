"""
訳語の一括置換。tools/terms.tsv の「置換前<TAB>置換後」を、指定フォルダ以下の .xml と .tsv すべてに適用する。
QudJP の訳と自作訳で呼び名をそろえるために、配置の最後に実行する。

使い方: python -I tools/term_replace.py <tools/terms.tsv> <Localization フォルダ>
"""
import glob
import os
import sys


def load(path):
    pairs = []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n").rstrip("\r")
        if not line or line.startswith("#") or "\t" not in line:
            continue
        old, new = line.split("\t", 1)
        if old:
            pairs.append((old, new))
    return sorted(pairs, key=lambda p: -len(p[0]))


def main():
    pairs = load(sys.argv[1])
    root = sys.argv[2]
    counts = {old: 0 for old, _ in pairs}
    files = 0
    for f in glob.glob(os.path.join(root, "**", "*.*"), recursive=True):
        if not f.endswith((".xml", ".tsv")):
            continue
        text = open(f, encoding="utf-8").read()
        new = text
        for old, rep in pairs:
            n = new.count(old)
            if n:
                counts[old] += n
                new = new.replace(old, rep)
        if new != text:
            with open(f, "w", encoding="utf-8", newline="") as w:
                w.write(new)
            files += 1
    summary = " / ".join(f"{old}→{rep}: {counts[old]}" for old, rep in pairs)
    print(f"訳語の一括置換: {files} ファイル（{summary}）")


if __name__ == "__main__":
    main()
