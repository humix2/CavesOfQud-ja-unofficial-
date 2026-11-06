"""
変異の説明文の書き出し（QudJPLite が作る mutation-dump.txt）から、訳すべき「型」の一覧を作る。
数値・ダイス表記を <#0> <#1> … に置き換える処理は、QudJPLite/UITranslation.cs の Templatize と同じ。

使い方: python -I tools/make_templates.py <mutation-dump.txt> <tools/ui-templates.tsv> <出力.tsv>
出力には、まだ ui-templates.tsv に無い型だけを「型<TAB>（空）」で、出現した変異名つきで書く。
"""
import re
import sys


def templatize(line):
    values = []
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        signed = c in "+-" and i + 1 < n and line[i + 1].isdigit() and (i == 0 or not line[i - 1].isalnum())
        if (c.isdigit() and (i == 0 or not line[i - 1].isalpha())) or signed:
            start = i
            if signed:
                i += 1
            while i < n and line[i].isdigit():
                i += 1
            if i + 1 < n and line[i] == "." and line[i + 1].isdigit():
                i += 1
                while i < n and line[i].isdigit():
                    i += 1
            if i + 1 < n and line[i] == "d" and line[i + 1].isdigit():
                i += 1
                while i < n and line[i].isdigit():
                    i += 1
                if i + 1 < n and line[i] in "+-" and line[i + 1].isdigit():
                    i += 1
                    while i < n and line[i].isdigit():
                        i += 1
            if i < n and line[i] == "%":
                i += 1
            out.append(f"<#{len(values)}>")
            values.append(line[start:i])
            continue
        out.append(c)
        i += 1
    return "".join(out), values


def unesc(s):
    return s.replace("\\n", "\n").replace("\\t", "\t")


def main():
    dump, existing, out = sys.argv[1:4]
    have = set()
    for line in open(existing, encoding="utf-8"):
        if "\t" in line and not line.startswith("#"):
            have.add(unesc(line.split("\t", 1)[0]))
    order = []
    where = {}
    name = None
    for raw in open(dump, encoding="utf-8"):
        raw = raw.rstrip("\n")
        if raw.startswith("## "):
            name = raw[3:]
            continue
        if raw.startswith("@"):
            continue
        if not raw.strip() or not re.search(r"[A-Za-z]", raw):
            continue
        key, _ = templatize(raw)
        if key in have:
            continue
        if key not in where:
            where[key] = []
            order.append(key)
        if name not in where[key]:
            where[key].append(name)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for k in order:
            f.write(f"# {', '.join(where[k][:4])}\n{k}\t\n")
    print("型:", len(order))


if __name__ == "__main__":
    main()
