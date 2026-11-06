using System;
using System.Collections.Generic;
using System.IO;
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

        private static Dictionary<string, string> table;

        [ModSensitiveCacheInit]
        public static void Load()
        {
            table = null;
            try
            {
                string dir = null;
                foreach (ModInfo mod in ModManager.Mods)
                {
                    if (mod.ID == "QudJPLite")
                    {
                        dir = mod.Path;
                        break;
                    }
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
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] UI辞書の読み込みエラー: " + ex);
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
            if (d == null || string.IsNullOrEmpty(text) || text.Length > 400)
            {
                return text;
            }
            string result;
            if (d.TryGetValue(text, out result))
            {
                return result;
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
