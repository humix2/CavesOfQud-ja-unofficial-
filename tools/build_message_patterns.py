"""
メッセージログ用の正規表現パターン表（ui-messages.tsv）を作る。
材料: tools/ui-messages.tsv（自作・優先）と、QudJP (MIT) の Localization/Dictionaries/messages.ja.json の "patterns"。
出力: 1行 = 正規表現<TAB>訳文（{0} {1} … が捕捉グループ）。改行は \\n、タブは \\t。

使い方: python -I tools/build_message_patterns.py <QudJPフォルダ> <出力ファイル>
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def esc(s):
    return s.replace("\t", "\\t").replace("\r", "").replace("\n", "\\n")


def main():
    qudjp, out = sys.argv[1], sys.argv[2]
    rows = []
    own = os.path.join(HERE, "ui-messages.tsv")
    if os.path.exists(own):
        for line in open(own, encoding="utf-8"):
            line = line.rstrip("\n")
            if line and not line.startswith("#") and "\t" in line:
                rows.append(line)
    n_own = len(rows)
    path = os.path.join(qudjp, "Localization", "Dictionaries", "messages.ja.json")
    j = json.load(open(path, encoding="utf-8"))
    for p in j.get("patterns", []):
        pat, tpl = p.get("pattern"), p.get("template")
        if isinstance(pat, str) and isinstance(tpl, str) and pat and "\t" not in pat and "\n" not in pat:
            rows.append(pat + "\t" + esc(tpl))
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"メッセージのパターン: {len(rows)} 件（自作 {n_own} 件 + QudJP {len(rows) - n_own} 件）")


if __name__ == "__main__":
    main()
