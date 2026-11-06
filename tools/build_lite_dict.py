"""
QudJP Lite 用の UI 辞書（英語 → 日本語の完全一致表）を作る。

材料:
  - QudJP (MIT) の Localization/Dictionaries/**/*.json の "entries"（完全一致の訳のみ。正規表現の patterns は使わない）
  - tools/overrides/*.xml の自作訳のうち、=変数= を含まない固定の文
出力: TSV（1行 = 英語<TAB>日本語。改行は \\n、タブは \\t で表す）

使い方: python -I tools/build_lite_dict.py <QudJPフォルダ> <出力ファイル>
"""
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
VAR = re.compile(r"=[A-Za-z_][\w.\[\]#'|]*(?::[^=\n]*)?=")
JA = re.compile(r"[\u3040-\u30ff\u3400-\u9fff\uff01-\uff9f]")


def esc(s):
    return s.replace("\\", "\\\\").replace("\t", "\\t").replace("\r", "").replace("\n", "\\n")


def usable(key, val):
    if not key or not val or key == val:
        return False
    if not re.search(r"[A-Za-z]{2,}", key):  # 英字を含まないキー（記号・数字だけ）は誤爆しやすい
        return False
    if not JA.search(val):
        return False
    return True


def main():
    qudjp, out = sys.argv[1], sys.argv[2]
    d = {}
    # 手で直した訳（QudJP の誤訳の上書きなど）を最優先: tools/ui-overrides.tsv（英語<TAB>訳）
    fix = os.path.join(HERE, "ui-overrides.tsv")
    if os.path.exists(fix):
        for line in open(fix, encoding="utf-8"):
            line = line.rstrip("\n")
            if line and not line.startswith("#") and "\t" in line:
                k, v = line.split("\t", 1)
                d[k.replace("\\n", "\n")] = v.replace("\\n", "\n")
    # 自作訳を優先（新しい公式の訳文に合わせて作ったもの）
    for f in sorted(glob.glob(os.path.join(HERE, "overrides", "*.xml"))):
        for s in ET.parse(f).getroot().iter("string"):
            k, v = s.get("ID"), s.text or ""
            if k and not VAR.search(k) and not VAR.search(v) and usable(k, v):
                d.setdefault(k, v)
    own = len(d)
    for f in sorted(glob.glob(os.path.join(qudjp, "Localization", "Dictionaries", "**", "*.json"), recursive=True)):
        try:
            j = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        if not isinstance(j, dict):
            continue
        for e in j.get("entries", []):
            if isinstance(e, dict):
                k, v = e.get("key"), e.get("text")
                if isinstance(k, str) and isinstance(v, str) and usable(k, v) and ":" not in k[:12].split(" ")[0]:
                    d.setdefault(k, v)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for k, v in d.items():
            f.write(f"{esc(k)}\t{esc(v)}\n")
    print(f"UI辞書: {len(d)} 件（自作 {own} 件 + QudJP {len(d) - own} 件）")


if __name__ == "__main__":
    main()
