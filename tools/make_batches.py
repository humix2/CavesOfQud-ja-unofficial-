"""
未訳一覧 (dist/report/untranslated_strings.tsv) を、翻訳作業用の束に分ける。

出力: tools/translate/batch_NNN.src.tsv  (No, file, Context, ID)  ※ 改行は \\n で表す
訳は tools/translate/batch_NNN.ja.tsv に「No<TAB>訳文」の形で書き、apply_batches.py で反映する。
既に束になっている文字列は作り直さない（訳した束を壊さない）。

使い方: python -I tools/make_batches.py [--size 200] [--max-len 80] [--file Strings.example.xml]
"""
import argparse
import csv
import glob
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
TR = os.path.join(HERE, "translate")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--size", type=int, default=200)
    ap.add_argument("--max-len", type=int, default=100000)
    ap.add_argument("--file", default="Strings.example.xml")
    a = ap.parse_args()

    os.makedirs(TR, exist_ok=True)
    seen = set()
    nums = []
    for f in glob.glob(os.path.join(TR, "batch_*.src.tsv")):
        nums.append(int(os.path.basename(f)[6:9]))
        for r in csv.DictReader(open(f, encoding="utf-8"), delimiter="\t"):
            seen.add((r["Context"], r["ID"]))
    nxt = max(nums, default=0) + 1

    rows = list(csv.DictReader(open(os.path.join(REPO, "dist", "report", "untranslated_strings.tsv"), encoding="utf-8"),
                               delimiter="\t", quoting=csv.QUOTE_NONE))
    todo = [r for r in rows if r["file"] == a.file and len(r["ID"]) <= a.max_len
            and (r["Context"], r["ID"]) not in seen]
    # 同じ画面・機能の文字列がまとまるよう Context 順、その中は短い順
    todo.sort(key=lambda r: (r["Context"].split(" ")[0].lower(), len(r["ID"])))

    made = 0
    for i in range(0, len(todo), a.size):
        chunk = todo[i:i + a.size]
        path = os.path.join(TR, f"batch_{nxt:03}.src.tsv")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("No\tfile\tContext\tID\n")
            for n, r in enumerate(chunk, 2):  # No = ファイルの行番号（見出しが1行目）
                f.write(f"{n}\t{r['file']}\t{r['Context']}\t{r['ID']}\n")
        nxt += 1
        made += 1
    print(f"{len(todo)} 件を {made} 個の束にしました ({TR})")


if __name__ == "__main__":
    main()
