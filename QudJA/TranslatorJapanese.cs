using System;
using System.Collections.Generic;
using XRL.Language;

namespace QudJA
{
    // 日本語用の文法処理。英語版 (TranslatorBase) を継承し、日本語で不要・不自然な部分だけ差し替える。
    // ゲームが Language = "ja" のときだけ自動で使う。
    [LanguageProvider("ja")]
    public class TranslatorJapanese : TranslatorBase
    {
        // 行頭に来てはいけない文字（禁則）
        private const string NoLineStart = "、。，．・：；？！ー～…‥）」』】〕〉》］｝ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ,.!?)]}:;%";
        // 行末に来てはいけない文字
        private const string NoLineEnd = "（「『【〔〈《［｛([{";

        // ---- 改行位置: 空白に加え、日本語の文字の前後でも改行できるようにする
        public override void NextPossibleLineBreakIndex(ReadOnlySpan<char> Span, int StartIndex, out int BreakBeforeIndex, out bool ReplaceIfBroken)
        {
            int depth = MarkupDepth(Span, StartIndex + 1);
            for (int i = StartIndex + 1; i < Span.Length; i++)
            {
                char c = Span[i];
                char prev = Span[i - 1];
                if (c == ' ' || c == '\n')
                {
                    BreakBeforeIndex = i;
                    ReplaceIfBroken = true;
                    return;
                }
                if (depth == 0 && (IsCJK(c) || IsCJK(prev)) && CanBreakBetween(Span, i))
                {
                    BreakBeforeIndex = i;
                    ReplaceIfBroken = false;
                    return;
                }
                // {{color|...}} の "{{xxx|" 部分の中では改行しない
                if (c == '{' && i + 1 < Span.Length && Span[i + 1] == '{')
                {
                    depth++;
                }
                else if (c == '|' && depth > 0)
                {
                    depth--;
                }
            }
            BreakBeforeIndex = -1;
            ReplaceIfBroken = false;
        }

        private static bool CanBreakBetween(ReadOnlySpan<char> s, int i)
        {
            char c = s[i];
            char prev = s[i - 1];
            if (NoLineStart.IndexOf(c) >= 0 || NoLineEnd.IndexOf(prev) >= 0)
            {
                return false;
            }
            // 色コード (&X, ^X) や閉じ括弧 "}}" の途中では切らない
            if (prev == '&' || prev == '^' || c == '}' || prev == '{')
            {
                return false;
            }
            if (i >= 2 && (s[i - 2] == '&' || s[i - 2] == '^') && !IsCJK(prev))
            {
                return false;
            }
            return true;
        }

        // StartIndex までに開いている "{{xxx|" の中（色名部分）にいるかを数える
        private static int MarkupDepth(ReadOnlySpan<char> s, int end)
        {
            int depth = 0;
            for (int i = 0; i < end && i < s.Length; i++)
            {
                if (s[i] == '{' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    depth++;
                    i++;
                }
                else if (s[i] == '|' && depth > 0)
                {
                    depth--;
                }
            }
            return depth;
        }

        private static bool IsCJK(char c)
        {
            return (c >= '　' && c <= 'ヿ')   // 記号・ひらがな・カタカナ
                || (c >= '㐀' && c <= '鿿')   // 漢字
                || (c >= '豈' && c <= '﫿')
                || (c >= '＀' && c <= '￯');  // 全角英数・半角カナ
        }

        // ---- 冠詞: 日本語には無い
        public override string DefiniteArticle(DefiniteArticleParams Params)
        {
            return "";
        }

        public override string IndefiniteArticle(IndefiniteArticleParams Params)
        {
            return "";
        }

        public override void AddArticle(AddArticleParams Params)
        {
        }

        // ---- 列挙: 「A、B、C」「AとB」「AかB」
        public override string MakeCommaList(IReadOnlyList<string> List)
        {
            return Join(List, "、");
        }

        public override string MakeAndList(IReadOnlyList<string> List, MakeAndListParams Params = default(MakeAndListParams))
        {
            if (List.Count == 2)
            {
                return List[0] + "と" + List[1];
            }
            return Join(List, "、");
        }

        public override string MakeOrList(IReadOnlyList<string> List, MakeOrListParams Params = default(MakeOrListParams))
        {
            if (List.Count == 0)
            {
                return "";
            }
            if (List.Count == 1)
            {
                return List[0];
            }
            string head = Join(List, "、", List.Count - 1);
            return head + "、または" + List[List.Count - 1];
        }

        private static string Join(IReadOnlyList<string> List, string Separator, int Count = -1)
        {
            if (Count < 0)
            {
                Count = List.Count;
            }
            if (Count == 0)
            {
                return "";
            }
            string result = List[0];
            for (int i = 1; i < Count; i++)
            {
                result += Separator + List[i];
            }
            return result;
        }

        // ---- 数詞: 算用数字で表す
        public override string Cardinal(long num)
        {
            return num.ToString();
        }

        public override string CardinalNo(long num)
        {
            return num == 0 ? "ゼロ" : num.ToString();
        }

        public override string Ordinal(long num)
        {
            return num + "番目";
        }

        // ゲーム内ではほぼ日付（"the 16th of ..."）に使われるので「16日」とする
        public override string OrdinalWithDigits(long num)
        {
            return num + "日";
        }

        public override string Multiplicative(long num)
        {
            return num == 0 ? "一度も" : num + "回";
        }
    }
}
