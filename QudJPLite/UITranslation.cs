using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.UI;

namespace QudJPLite
{
    // UI の文字を、完全一致の辞書で日本語に差し替える。
    // 割り込むのは UITextSkin（ゲームの UI 文字表示）の SetText と Apply の入口だけ。
    // 正規表現は使わず、辞書を引くだけなので軽い。
    [HasModSensitiveStaticCache]
    public static class UITranslation
    {
        private const string DictionaryFile = "ui-dictionary.tsv";

        private const string TemplateFile = "ui-templates.tsv";

        private static Dictionary<string, string> table;

        // 数値入りの文の「型」。数値・ダイス表記を <#0> <#1> … に置き換えた英文 → 訳文
        private static Dictionary<string, string> templates;

        // 行ごとの訳の結果（同じ文が何度も表示されるので覚えておく）
        private static readonly Dictionary<string, string> lineCache = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string ModDirectory()
        {
            foreach (ModInfo mod in ModManager.Mods)
            {
                if (mod.ID == "QudJPLite")
                {
                    return mod.Path;
                }
            }
            return null;
        }

        [ModSensitiveCacheInit]
        public static void Load()
        {
            table = null;
            templates = null;
            lineCache.Clear();
            try
            {
                string dir = ModDirectory();
                string tpath = dir == null ? null : Path.Combine(dir, "Localization", TemplateFile);
                if (tpath != null && File.Exists(tpath))
                {
                    Dictionary<string, string> t = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (string line in File.ReadAllLines(tpath))
                    {
                        int tab = line.IndexOf('\t');
                        if (tab <= 0 || line.StartsWith("#"))
                        {
                            continue;
                        }
                        string key = Unescape(line.Substring(0, tab));
                        if (!t.ContainsKey(key))
                        {
                            t.Add(key, Unescape(line.Substring(tab + 1)));
                        }
                    }
                    templates = t;
                    Debug.Log("[QudJPLite] 数値入りの文の型を読み込みました: " + t.Count + " 件");
                }
                string path = dir == null ? null : Path.Combine(dir, "Localization", DictionaryFile);
                if (path == null || !File.Exists(path))
                {
                    Debug.LogWarning("[QudJPLite] UI辞書が見つかりません: " + path);
                    return;
                }
                Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string line in File.ReadAllLines(path))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0)
                    {
                        continue;
                    }
                    string key = Unescape(line.Substring(0, tab));
                    if (!d.ContainsKey(key))
                    {
                        d.Add(key, Unescape(line.Substring(tab + 1)));
                    }
                }
                table = d;
                Debug.Log("[QudJPLite] UI辞書を読み込みました: " + d.Count + " 件");
                TranslatePopupButtons();
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] UI辞書の読み込みエラー: " + ex);
            }
        }

        // ポップアップの共通ボタン（Yes / No / Cancel など）は、PopupMessage の静的な一覧に入っている。
        // 表示が UITextSkin を通らないので、一覧の文字そのものを1回だけ訳しておく
        private static void TranslatePopupButtons()
        {
            try
            {
                foreach (System.Reflection.FieldInfo f in typeof(Qud.UI.PopupMessage).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
                {
                    List<Qud.UI.QudMenuItem> list = f.FieldType == typeof(List<Qud.UI.QudMenuItem>) ? f.GetValue(null) as List<Qud.UI.QudMenuItem> : null;
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        Qud.UI.QudMenuItem item = list[i];
                        if (!string.IsNullOrEmpty(item.text))
                        {
                            item.text = Translate(item.text);
                            list[i] = item;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップのボタンの翻訳エラー: " + ex);
            }
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0)
            {
                return s;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string Translate(string text)
        {
            Dictionary<string, string> d = table;
            if (d == null || string.IsNullOrEmpty(text))
            {
                return text;
            }
            string result;
            if (d.TryGetValue(text, out result))
            {
                return result;
            }
            if (text.Length <= 200 && text.IndexOf('\n') < 0)
            {
                string deco = TranslateDecorated(text);
                if (deco != null)
                {
                    return deco;
                }
            }
            if (templates != null && text.Length <= 6000)
            {
                // 複数行の文（変異の説明など）は行ごとに、辞書 → 型 の順で訳す
                if (text.IndexOf('\n') >= 0)
                {
                    return TranslateLines(text);
                }
                string line = TranslateLine(text);
                if (!ReferenceEquals(line, text))
                {
                    return line;
                }
            }
            if (text.Length > 400)
            {
                return text;
            }

            // 前後の空白は残して中身だけ訳す
            int start = 0;
            int end = text.Length;
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
            while (end > start && char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }
            if (start > 0 || end < text.Length)
            {
                if (end > start && d.TryGetValue(text.Substring(start, end - start), out result))
                {
                    return text.Substring(0, start) + result + text.Substring(end);
                }
                return text;
            }

            // 全体が {{色|...}} で囲まれていれば中身を訳す
            if (text.StartsWith("{{") && text.EndsWith("}}"))
            {
                int bar = text.IndexOf('|');
                if (bar > 2 && text.IndexOf("{{", 2, StringComparison.Ordinal) < 0)
                {
                    string inner = text.Substring(bar + 1, text.Length - bar - 3);
                    if (d.TryGetValue(inner, out result))
                    {
                        return text.Substring(0, bar + 1) + result + "}}";
                    }
                }
            }
            return text;
        }

        // メニュー項目やアビリティバーのように、本文の前後に飾りが付いた文を訳す。
        // 前: 色指定（&C ^K）、キー表示（{{w|[k]}} や [k]）
        // 後: 状態表示（ {{K|[{{y|off}}]}}）、キー表示（ <2>）、空白
        // 飾りを外した本文が辞書にあれば、訳して飾りを付け直す。訳せなければ null
        private static string TranslateDecorated(string text)
        {
            int start = 0;
            int end = text.Length;
            bool moved = true;
            while (moved && start < end)
            {
                moved = false;
                char c = text[start];
                if ((c == '&' || c == '^') && start + 1 < end && text[start + 1] != c)
                {
                    start += 2;
                    moved = true;
                }
                else if (c == '{' && start + 1 < end && text[start + 1] == '{')
                {
                    int close = text.IndexOf("}}", start, StringComparison.Ordinal);
                    int bar = text.IndexOf('|', start);
                    if (close > 0 && bar > 0 && bar < close && bar + 1 < end && text[bar + 1] == '['
                        && close - start <= 16 && text.IndexOf("{{", start + 2, close - start - 2, StringComparison.Ordinal) < 0)
                    {
                        start = close + 2;
                        moved = true;
                    }
                }
                else if (c == '[')
                {
                    int close = text.IndexOf(']', start);
                    if (close > 0 && close - start <= 8 && close + 1 < end && text[close + 1] == ' ')
                    {
                        start = close + 1;
                        moved = true;
                    }
                }
                while (start < end && text[start] == ' ')
                {
                    start++;
                }
            }
            moved = true;
            while (moved && end > start)
            {
                moved = false;
                while (end > start && char.IsWhiteSpace(text[end - 1]))
                {
                    end--;
                    moved = true;
                }
                if (end - start >= 4 && text[end - 1] == '}' && text[end - 2] == '}')
                {
                    // 末尾の {{...}} ブロック（入れ子あり）の始まりを探す
                    int depth = 0;
                    int i = end - 1;
                    for (; i > start; i--)
                    {
                        if (text[i] == '}' && text[i - 1] == '}') { depth++; i--; }
                        else if (text[i] == '{' && text[i - 1] == '{') { depth--; i--; if (depth == 0) break; }
                    }
                    if (depth == 0 && i > start && text[i - 1] == ' ')
                    {
                        end = i - 1;
                        moved = true;
                    }
                }
                else if (end - start >= 3 && text[end - 1] == '>')
                {
                    int open = text.LastIndexOf(" <", end - 1, end - start, StringComparison.Ordinal);
                    if (open > start && end - open <= 12)
                    {
                        end = open;
                        moved = true;
                    }
                }
            }
            if (start == 0 && end == text.Length || end <= start)
            {
                return null;
            }
            string core = text.Substring(start, end - start);
            string ja;
            if (table.TryGetValue(core, out ja))
            {
                return text.Substring(0, start) + ja + text.Substring(end);
            }
            // 本文がさらに {{y|...}} で囲まれている場合（ポップアップの選択肢 "{{W|[k]}} {{y|...}}" など）は中身を引く
            if (core.Length > 5 && core.StartsWith("{{") && core.EndsWith("}}"))
            {
                int bar = core.IndexOf('|');
                if (bar > 2 && bar < 12 && core.IndexOf("{{", 2, StringComparison.Ordinal) < 0)
                {
                    string inner = core.Substring(bar + 1, core.Length - bar - 3);
                    if (table.TryGetValue(inner, out ja))
                    {
                        return text.Substring(0, start) + core.Substring(0, bar + 1) + ja + "}}" + text.Substring(end);
                    }
                }
            }
            return null;
        }

        private static string TranslateLines(string text)
        {
            string[] lines = text.Split('\n');
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = TranslateLine(lines[i]);
                if (!ReferenceEquals(t, lines[i]))
                {
                    lines[i] = t;
                    changed = true;
                }
            }
            return changed ? string.Join("\n", lines) : text;
        }

        // 1行を訳す。訳せなければ同じ参照を返す
        private static string TranslateLine(string line)
        {
            if (line.Length == 0 || line.Length > 600)
            {
                return line;
            }
            string cached;
            if (lineCache.TryGetValue(line, out cached))
            {
                return cached ?? line;
            }
            string result = null;
            string exact;
            if (table.TryGetValue(line, out exact))
            {
                result = exact;
            }
            else
            {
                List<string> values;
                string key = Templatize(line, out values);
                string ja;
                if (values.Count > 0 && templates.TryGetValue(key, out ja))
                {
                    StringBuilder sb = new StringBuilder(ja);
                    for (int i = 0; i < values.Count; i++)
                    {
                        sb.Replace("<#" + i + ">", values[i]);
                    }
                    result = sb.ToString();
                }
                else if (values.Count == 0 && templates.TryGetValue(line, out ja))
                {
                    result = ja;
                }
                else if (HasLatinWord(line))
                {
                    // 名前や数値が入る文（メッセージ・ポップアップなど）は正規表現のパターンで
                    result = MessageTranslation.MatchPatterns(line);
                }
                // 訳文の中に差し込まれた数値の単位（「10 rounds続く」など）
                if (result == null && line.IndexOf(" round", StringComparison.Ordinal) > 0 && JapaneseWrap.HasJapanese(line))
                {
                    string r = RoundUnit.Replace(line, "$1ラウンド");
                    if (r != line)
                    {
                        result = r;
                    }
                }
            }
            if (lineCache.Count > 8192)
            {
                lineCache.Clear();
            }
            lineCache[line] = result;
            return result ?? line;
        }

        private static readonly System.Text.RegularExpressions.Regex RoundUnit = new System.Text.RegularExpressions.Regex(
            @"(\d(?:\}\})?) rounds?(?![A-Za-z])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        // 英単語（英字2文字以上の連なり）を含むか。日本語だけの文にはパターンを当てない
        private static bool HasLatinWord(string s)
        {
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    if (++run >= 2) return true;
                }
                else
                {
                    run = 0;
                }
            }
            return false;
        }

        // 数値・ダイス表記（例: 9, 1d4+1, -2, 25%, 1.5）を <#0> <#1> … に置き換える
        public static string Templatize(string line, out List<string> values)
        {
            values = new List<string>();
            StringBuilder sb = new StringBuilder(line.Length);
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                bool signed = (c == '+' || c == '-') && i + 1 < line.Length && char.IsDigit(line[i + 1])
                    && (i == 0 || !char.IsLetterOrDigit(line[i - 1]));
                if ((char.IsDigit(c) && (i == 0 || !char.IsLetter(line[i - 1]))) || signed)
                {
                    int start = i;
                    if (signed)
                    {
                        i++;
                    }
                    while (i < line.Length && char.IsDigit(line[i])) i++;
                    if (i + 1 < line.Length && line[i] == '.' && char.IsDigit(line[i + 1]))
                    {
                        i++;
                        while (i < line.Length && char.IsDigit(line[i])) i++;
                    }
                    if (i + 1 < line.Length && line[i] == 'd' && char.IsDigit(line[i + 1]))
                    {
                        i++;
                        while (i < line.Length && char.IsDigit(line[i])) i++;
                        if (i + 1 < line.Length && (line[i] == '+' || line[i] == '-') && char.IsDigit(line[i + 1]))
                        {
                            i++;
                            while (i < line.Length && char.IsDigit(line[i])) i++;
                        }
                    }
                    if (i < line.Length && line[i] == '%')
                    {
                        i++;
                    }
                    sb.Append("<#").Append(values.Count).Append('>');
                    values.Add(line.Substring(start, i - start));
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }

    [HarmonyPatch(typeof(UITextSkin), nameof(UITextSkin.SetText))]
    public static class UITextSkinSetTextPatch
    {
        // 訳してから元の処理に渡すので、「同じ文字なら何もしない」判定がそのまま効く
        public static void Prefix(ref string text)
        {
            text = UITranslation.Translate(text);
        }
    }

    // タイトル画面の項目（New Game など）は UITextSkin ではなく Unity の Text に直接書かれる
    [HarmonyPatch(typeof(MainMenuRow), nameof(MainMenuRow.setData))]
    public static class MainMenuRowPatch
    {
        public static void Postfix(MainMenuRow __instance)
        {
            try
            {
                if (__instance.text != null)
                {
                    __instance.text.text = UITranslation.Translate(__instance.text.text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] タイトル画面の翻訳エラー: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(UITextSkin), nameof(UITextSkin.Apply))]
    public static class UITextSkinApplyPatch
    {
        // 画面部品にあらかじめ書かれている見出しなど、SetText を通らない文字用
        public static void Prefix(UITextSkin __instance, out bool __state)
        {
            __instance.text = UITranslation.Translate(__instance.text);

            // ゲームの折り返し（TextBlock.ParseSpan）は空白区切りの単語単位で、
            // 改行直後の段落の最初の単語が1行に収まらないと、その先頭1文字を捨てて空行を1つ足してしまう。
            // 空白のない日本語の長い段落で起きるので、そういう文のときだけゲームの折り返しを飛ばし、
            // TextMeshPro 自身の折り返しに任せる。
            __state = false;
            if (__instance.useBlockWrap && __instance.blockWrap > 0
                && JapaneseWrap.HasLongJapaneseRun(__instance.text, __instance.blockWrap - 1))
            {
                __instance.useBlockWrap = false;
                __state = true;
            }
        }

        public static void Postfix(UITextSkin __instance, bool __state)
        {
            if (__state)
            {
                __instance.useBlockWrap = true;
            }
        }
    }

    internal static class JapaneseWrap
    {
        public static bool HasJapanese(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '぀')
                {
                    return true;
                }
            }
            return false;
        }

        // 空白・改行で区切った塊のうち、limit 文字以上で日本語を含むものがあるか
        public static bool HasLongJapaneseRun(string s, int limit)
        {
            if (string.IsNullOrEmpty(s) || s.Length < limit)
            {
                return false;
            }
            int len = 0;
            bool ja = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '\n')
                {
                    len = 0;
                    ja = false;
                    continue;
                }
                len++;
                if (c >= '　')
                {
                    ja = true;
                }
                if (ja && len >= limit)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
