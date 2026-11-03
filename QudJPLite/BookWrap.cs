using System;
using System.Text;
using HarmonyLib;
using UnityEngine;
using XRL.UI;

namespace QudJPLite
{
    // 本の日本語を折り返す。
    //
    // 原因: ゲームは本の文章を「空白で区切った単語」単位で折り返す（BookPage と BookUI.AutoformatPages）。
    // 日本語には空白がないので段落が丸ごと1行になり、本の画面で左右にはみ出して切れる。
    //
    // 対策: ページを作る直前に、日本語を含む段落だけ1行あたりの文字数で改行を入れる。
    // ページ生成時に1回だけ動くので、表示のたびの負荷はない。
    internal static class BookWrap
    {
        // 本の画面に収まる1行の文字数（QudJP 本家と同じ値）。英語版の 77 桁 ≒ 全角 47 文字
        public const int PageColumns = 47;

        // Format="Auto" の本: 余白を引いた幅（英語版は 80 - 余白、日本語は 51 - 余白）
        public const int AutoColumns = 51;

        public static string Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text) || width < 10 || !ContainsJapanese(text))
            {
                return text;
            }
            StringBuilder sb = new StringBuilder(text.Length + text.Length / width + 8);
            int col = 0;          // 今の行に見えている文字数
            int breakAt = -1;     // 改行を入れてよい位置（sb 上の位置）
            int breakCol = 0;     // そこまでの文字数
            for (int i = 0; i < text.Length; i++)
            {
                int skip = MarkupLength(text, i);
                if (skip > 0)
                {
                    // 色指定などの見えない記号は数えずにそのまま写す
                    sb.Append(text, i, skip);
                    i += skip - 1;
                    continue;
                }
                char c = text[i];
                if (c == '\r')
                {
                    continue;
                }
                sb.Append(c);
                if (c == '\n')
                {
                    col = 0;
                    breakAt = -1;
                    continue;
                }
                col++;

                char next = NextVisible(text, i + 1);
                if (next == '\0' || next == '\n')
                {
                    continue;
                }
                if (CanBreakBetween(c, next))
                {
                    breakAt = sb.Length;
                    breakCol = col;
                }
                if (col >= width)
                {
                    if (breakAt > 0 && breakCol > width / 2)
                    {
                        // 直近の改行可能位置で折る（禁則・英単語の途中を避ける）
                        sb.Insert(breakAt, '\n');
                        col -= breakCol;
                    }
                    else if (col > width + 2)
                    {
                        // 折れる位置がなければ強制的に折る
                        sb.Append('\n');
                        col = 0;
                    }
                    else
                    {
                        continue;
                    }
                    breakAt = -1;
                }
            }
            return sb.ToString();
        }

        // {{X|, }}, &X, ^X, <tag> を見えない記号として長さを返す
        private static int MarkupLength(string s, int i)
        {
            char c = s[i];
            if (i + 1 >= s.Length)
            {
                return 0;
            }
            char n = s[i + 1];
            if (c == '{' && n == '{')
            {
                int bar = s.IndexOf('|', i + 2);
                int close = s.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (bar > 0 && (close < 0 || bar < close) && bar - i < 40)
                {
                    return bar - i + 1;
                }
                return 0;
            }
            if (c == '}' && n == '}')
            {
                return 2;
            }
            if ((c == '&' || c == '^') && n != '\n')
            {
                return 2;
            }
            if (c == '<')
            {
                int close = s.IndexOf('>', i + 1);
                if (close > 0 && close - i < 40 && s.IndexOf('\n', i, close - i) < 0)
                {
                    return close - i + 1;
                }
            }
            return 0;
        }

        private static char NextVisible(string s, int i)
        {
            while (i < s.Length)
            {
                int skip = MarkupLength(s, i);
                if (skip == 0)
                {
                    return s[i] == '\r' ? '\n' : s[i];
                }
                i += skip;
            }
            return '\0';
        }

        private static bool CanBreakBetween(char prev, char next)
        {
            if (IsNoLineStart(next) || IsNoLineEnd(prev))
            {
                return false;
            }
            // 英単語・数字の途中では折らない
            if (IsWordChar(prev) && IsWordChar(next))
            {
                return false;
            }
            return IsJapanese(prev) || IsJapanese(next) || prev == ' ';
        }

        private static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                || c == '\'' || c == '-' || c == '.' || c == ',';
        }

        // 行頭に置かない文字
        private static bool IsNoLineStart(char c)
        {
            return "、。，．,.!?！？)]）」』】〉》〕ーぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ…‥・：；:;".IndexOf(c) >= 0;
        }

        // 行末に置かない文字
        private static bool IsNoLineEnd(char c)
        {
            return "(（「『【〈《〔[".IndexOf(c) >= 0;
        }

        private static bool ContainsJapanese(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (IsJapanese(s[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsJapanese(char c)
        {
            return (c >= '　' && c <= 'ヿ')   // 句読点・かな
                || (c >= '㐀' && c <= '鿿')   // 漢字
                || (c >= '豈' && c <= '﫿')
                || (c >= '＀' && c <= '￯');  // 全角英数・記号
        }
    }

    // XML の本（Format 指定なし）・自動生成の本: 1ページ分の文章から行を作る入口
    [HarmonyPatch(typeof(BookPage), MethodType.Constructor, new Type[] { typeof(string), typeof(string) })]
    public static class BookPageWrapPatch
    {
        public static void Prefix(ref string Data)
        {
            try
            {
                Data = BookWrap.Wrap(Data, BookWrap.PageColumns);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 本の折り返しエラー: " + ex);
            }
        }
    }

    // Format="Auto" の本: 行数でページを分ける前に折り返す（でないと1段落=1行と数えられる）
    [HarmonyPatch(typeof(BookUI), nameof(BookUI.AutoformatPages),
        new Type[] { typeof(string), typeof(string), typeof(string), typeof(int), typeof(int), typeof(int), typeof(int) })]
    public static class BookAutoformatWrapPatch
    {
        public static void Prefix(ref string Text, int LeftMargin, int RightMargin)
        {
            try
            {
                Text = BookWrap.Wrap(Text, BookWrap.AutoColumns - LeftMargin - RightMargin);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 本の折り返しエラー: " + ex);
            }
        }
    }
}
