using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Qud.API;
using UnityEngine;

namespace QudJPLite
{
    // ジャーナルの各項目（村の歴史・観察・手がかりなど）の本文を UI 辞書で訳す。
    // 画面ではゲームが先頭に「$ 」を付けてから表示するので、表示の段階では辞書と一致しない。
    // そこで本文を取り出すところ（GetDisplayText）で、行ごとに訳しておく
    [HarmonyPatch(typeof(IBaseJournalEntry), nameof(IBaseJournalEntry.GetDisplayText))]
    public static class JournalEntryTextPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = JournalText.Translate(__result);
        }
    }

    // 村の記録は GetDisplayText を独自に持っているので、別にパッチする
    [HarmonyPatch(typeof(JournalVillageNote), nameof(JournalVillageNote.GetDisplayText))]
    public static class JournalVillageNoteTextPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = JournalText.Translate(__result);
        }
    }

    // 地図の記録（場所タブ）も GetDisplayText を独自に持っている。
    // 名前・目印からの方角（LoreGenerator.GenerateLandmarkDirectionsTo）・最後に訪れた日を1行ずつ並べたもの
    [HarmonyPatch(typeof(JournalMapNote), nameof(JournalMapNote.GetDisplayText))]
    public static class JournalMapNoteTextPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = MapNoteText.Translate(__result);
        }
    }

    internal static class MapNoteText
    {
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        // "1 parasang west and 2 parasangs north of Red Rock"
        private static readonly Regex Step = new Regex(@"^(\d+) parasangs? (west|east|north|south)$", RegexOptions.Compiled);

        private static readonly Regex LastVisit = new Regex(@"^Last visited on the (.+) of (.+)$", RegexOptions.Compiled);

        private static readonly Regex Article = new Regex(@"^(?:an?|some|the) ", RegexOptions.Compiled);

        private static readonly Regex Color = new Regex(@"\{\{[^|{}]*\||\}\}", RegexOptions.Compiled);

        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            lock (cache)
            {
                string r;
                if (cache.TryGetValue(text, out r))
                {
                    return r;
                }
                try
                {
                    string[] lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = Line(lines[i]);
                    }
                    r = string.Join("\n", lines);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[QudJPLite] ジャーナルの場所の翻訳エラー: " + ex);
                    r = text;
                }
                if (cache.Count > 1024)
                {
                    cache.Clear();
                }
                cache[text] = r;
                return r;
            }
        }

        private static string Line(string line)
        {
            // 方角と日付は自前で組み立てる（QudJP の型だと Ides などの日付が英語のまま残る）
            Match m = LastVisit.Match(line);
            if (m.Success)
            {
                return "最後に訪れた日: " + Date(m.Groups[1].Value, m.Groups[2].Value);
            }
            string d = Directions(line);
            if (d != null)
            {
                return d;
            }
            string ja = UITranslation.Translate(line);
            if (!ReferenceEquals(ja, line) && ja != line)
            {
                return ja;
            }
            return Name(line);
        }

        // 方角の行。形が違えば null
        private static string Directions(string line)
        {
            if (line.StartsWith("near ", StringComparison.Ordinal))
            {
                return Place(line.Substring(5)) + "の近く";
            }
            int of = line.IndexOf(" of ", StringComparison.Ordinal);
            if (of < 0)
            {
                return null;
            }
            string[] steps = line.Substring(0, of).Split(new[] { " and " }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();
            sb.Append(Place(line.Substring(of + 4))).Append("から");
            for (int i = 0; i < steps.Length; i++)
            {
                Match m = Step.Match(steps[i]);
                if (!m.Success)
                {
                    return null;
                }
                if (i > 0)
                {
                    sb.Append("、");
                }
                sb.Append(Direction(m.Groups[2].Value)).Append("へ").Append(m.Groups[1].Value).Append("パラサング");
            }
            return sb.ToString();
        }

        private static string Direction(string en)
        {
            switch (en)
            {
                case "west": return "西";
                case "east": return "東";
                case "north": return "北";
                default: return "南";
            }
        }

        // 目印の名前（Red Rock / the Yd Freehold など）
        private static string Place(string en)
        {
            string ja = Lookup(en);
            if (ja != null)
            {
                return ja;
            }
            string bare = Article.Replace(en, "");
            ja = Lookup(bare) ?? Lookup(char.ToUpperInvariant(en[0]) + en.Substring(1));
            return ja ?? en;
        }

        // 項目名（"a {{r|rust}} bog"・"ミケスフ Embassy" など）
        private static string Name(string line)
        {
            string plain = Color.Replace(line, "");
            string bare = Article.Replace(plain, "");
            string ja = Lookup(plain) ?? Lookup(bare);
            if (ja != null)
            {
                return ja;
            }
            if (bare.EndsWith(" Embassy", StringComparison.Ordinal))
            {
                string who = bare.Substring(0, bare.Length - 8);
                return (Lookup(who) ?? who) + "の大使館";
            }
            return line;
        }

        private static string Lookup(string en)
        {
            if (string.IsNullOrEmpty(en))
            {
                return null;
            }
            string ja = UITranslation.Translate(en);
            return !ReferenceEquals(ja, en) && ja != en && JapaneseWrap.HasJapanese(ja) ? ja : null;
        }

        // ステータスバーの日付（StatusText.MonthDay）と同じ書き方: "ニヴン・ウト 21日" / "ニヴン・ウト イデスの日"
        private static string Date(string day, string month)
        {
            string m = StatusText.T(month);
            if (day == "Ides")
            {
                return m + " イデスの日";
            }
            int n = 0;
            while (n < day.Length && char.IsDigit(day[n]))
            {
                n++;
            }
            return n > 0 ? m + " " + day.Substring(0, n) + "日" : m + " " + StatusText.T(day);
        }
    }

    internal static class JournalText
    {
        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            try
            {
                if (text.IndexOf('\n') < 0)
                {
                    return UITranslation.Translate(text);
                }
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = UITranslation.Translate(lines[i]);
                }
                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ジャーナルの翻訳エラー: " + ex);
                return text;
            }
        }
    }
}
