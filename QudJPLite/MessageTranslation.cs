using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.Messages;

namespace QudJPLite
{
    // メッセージログの日本語化。
    // メッセージがログに追加される瞬間（MessageQueue.AddPlayerMessage）に1回だけ、正規表現のパターンで訳す。
    // 表示のたびには何もしないので軽い。パターンは QudJP (MIT) の messages.ja.json と自作分（ui-messages.tsv）。
    [HasModSensitiveStaticCache]
    public static class MessageTranslation
    {
        private struct Rule
        {
            public Regex Pattern;
            public string Template;
        }

        private static List<Rule> rules;

        [ModSensitiveCacheInit]
        public static void Load()
        {
            rules = null;
            try
            {
                string dir = UITranslation.ModDirectory();
                string path = dir == null ? null : Path.Combine(dir, "Localization", "ui-messages.tsv");
                if (path == null || !File.Exists(path))
                {
                    return;
                }
                List<Rule> list = new List<Rule>();
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }
                    int tab = line.IndexOf('\t');
                    if (tab <= 0)
                    {
                        continue;
                    }
                    try
                    {
                        list.Add(new Rule
                        {
                            Pattern = new Regex(line.Substring(0, tab), RegexOptions.CultureInvariant),
                            Template = Unescape(line.Substring(tab + 1))
                        });
                    }
                    catch (Exception)
                    {
                        // 壊れたパターンは読み飛ばす
                    }
                }
                rules = list;
                Debug.Log("[QudJPLite] メッセージのパターンを読み込みました: " + list.Count + " 件");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] メッセージのパターンの読み込みエラー: " + ex);
            }
        }

        private static string Unescape(string s)
        {
            return s.IndexOf('\\') < 0 ? s : s.Replace("\\n", "\n").Replace("\\t", "\t");
        }

        // メッセージの訳: UI 辞書・型・パターンを順に試す（UITranslation.Translate が全部やる）
        public static string Translate(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length > 2000)
            {
                return message;
            }
            return UITranslation.Translate(message);
        }

        private static int depth;

        // 訳文の {0} {t0} {d0} などを、捕捉した部分（辞書にあれば訳したもの）に置き換える
        private static readonly Regex Slot = new Regex(@"\{[a-z]*(\d+)\}", RegexOptions.CultureInvariant);

        // 先頭の {{X| に対応する }} が文末のものか（中に別の色指定が入れ子になっていてもよい）
        private static bool ClosesAtEnd(string line, int from)
        {
            int depth = 1;
            for (int i = from; i < line.Length - 1; i++)
            {
                if (line[i] == '{' && line[i + 1] == '{')
                {
                    depth++;
                    i++;
                }
                else if (line[i] == '}' && line[i + 1] == '}')
                {
                    depth--;
                    i++;
                    if (depth == 0)
                    {
                        return i == line.Length - 1;
                    }
                }
            }
            return false;
        }

        // 1行の文をパターンで訳す。当たらなければ null。
        // 色指定（先頭の &X ^X や、全体を囲む {{X|...}}）は外して照合し、訳文に付け直す
        public static string MatchPatterns(string line)
        {
            List<Rule> list = rules;
            if (list == null || line.Length < 4 || depth > 2)
            {
                return null;
            }
            int start = 0;
            while (start + 1 < line.Length && (line[start] == '&' || line[start] == '^') && line[start + 1] != line[start])
            {
                start += 2;
            }
            if (start > 0)
            {
                string inner = MatchPatterns(line.Substring(start));
                return inner == null ? null : line.Substring(0, start) + inner;
            }
            if (line.StartsWith("{{") && line.EndsWith("}}"))
            {
                int bar = line.IndexOf('|');
                if (bar > 2 && bar < 12 && ClosesAtEnd(line, bar + 1))
                {
                    string inner = MatchPatterns(line.Substring(bar + 1, line.Length - bar - 3));
                    return inner == null ? null : line.Substring(0, bar + 1) + inner + "}}";
                }
            }
            depth++;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Match m = list[i].Pattern.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    string[] values = new string[m.Groups.Count - 1];
                    for (int g = 1; g < m.Groups.Count; g++)
                    {
                        // 名前などの部分も、辞書にあれば訳す
                        string v = m.Groups[g].Value;
                        if (v == "You" || v == "you")
                        {
                            values[g - 1] = "あなた";
                            continue;
                        }
                        values[g - 1] = v.Length < line.Length ? UITranslation.Translate(v) : v;
                    }
                    return Slot.Replace(list[i].Template, s =>
                    {
                        int n = int.Parse(s.Groups[1].Value);
                        return n < values.Length ? values[n] : s.Value;
                    });
                }
                return null;
            }
            finally
            {
                depth--;
            }
        }
    }

    // ポップアップの本文。表示側で折り返し（改行）が入る前に、行ごとに訳す
    [HarmonyPatch(typeof(XRL.UI.Popup), nameof(XRL.UI.Popup.WaitNewPopupMessage))]
    public static class PopupMessagePatch
    {
        public static void Prefix(ref string message)
        {
            try
            {
                message = MessageTranslation.Translate(message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップの翻訳エラー: " + ex);
            }
        }
    }

    // Popup.Show / ShowYesNo / ShowBlock などの入口。これらは色指定 {{X|...}} を &X 形式に変換してから表示側に渡すので、
    // 変換される前（入口）で訳す。引数 Message（文字列）を持つ Popup のメソッドすべてが対象。
    [HarmonyPatch]
    public static class PopupEntryPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo m in typeof(XRL.UI.Popup).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsGenericMethodDefinition || m.Name.StartsWith("Render") || m.Name == nameof(XRL.UI.Popup.WaitNewPopupMessage))
                {
                    continue;
                }
                ParameterInfo[] ps = m.GetParameters();
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].Name == "Message" && ps[i].ParameterType == typeof(string))
                    {
                        yield return m;
                        break;
                    }
                }
            }
        }

        // 対象はどれも引数名が Message（string）なので、名前で受け取って書き換えられる
        public static void Prefix(ref string Message)
        {
            try
            {
                Message = MessageTranslation.Translate(Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップの翻訳エラー: " + ex);
            }
        }
    }

    // 選択肢のポップアップ（Esc メニュー、たき火のメニューなど）。見出し・本文・選択肢をまとめて訳す
    [HarmonyPatch]
    public static class PopupOptionsPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type t = typeof(XRL.UI.Popup);
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickOption));
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickOptionAsync));
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickSeveral));
        }

        public static void Prefix(ref string Title, ref string Intro, ref IReadOnlyList<string> Options)
        {
            try
            {
                Title = MessageTranslation.Translate(Title);
                Intro = MessageTranslation.Translate(Intro);
                if (Options != null && Options.Count > 0)
                {
                    string[] translated = new string[Options.Count];
                    bool changed = false;
                    for (int i = 0; i < translated.Length; i++)
                    {
                        translated[i] = MessageTranslation.Translate(Options[i]);
                        changed |= !ReferenceEquals(translated[i], Options[i]);
                    }
                    if (changed)
                    {
                        Options = translated;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 選択肢の翻訳エラー: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(MessageQueue), nameof(MessageQueue.AddPlayerMessage), new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class AddPlayerMessagePatch
    {
        public static void Prefix(ref string Message)
        {
            try
            {
                Message = MessageTranslation.Translate(Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] メッセージの翻訳エラー: " + ex);
            }
        }
    }
}
