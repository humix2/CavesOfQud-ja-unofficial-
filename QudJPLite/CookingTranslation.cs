using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using XRL;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;
using Debug = UnityEngine.Debug;
using GameObject = XRL.World.GameObject;

namespace QudJPLite
{
    // 焚き火で料理したときの「You toss X, Y, and Z into a pot and stir.」を日本語で組み立て直す。
    // 材料は HistorySpice.json（spice.cooking）から「量 形容詞 名詞」をランダムに組み立てた英語で、
    // 組み合わせが多すぎて文ごとの辞書では訳せない。量・形容詞・名詞に分けて、それぞれを訳してつなぐ。
    // 語の訳は tools/cooking-words.tsv。そこに無ければ UI 辞書（クリーチャー名・体の部位など）を引く。
    [HasModSensitiveStaticCache]
    public static class CookingTranslation
    {
        private const string WordFile = "cooking-words.tsv";

        private static Dictionary<string, string> words = new Dictionary<string, string>(StringComparer.Ordinal);

        // 訳せなかった材料は1回だけ Player.log に出す（辞書に足すため）
        private static readonly HashSet<string> logged = new HashSet<string>(StringComparer.Ordinal);

        // 量の言い方（長いものから）
        private static readonly string[][] Amounts =
        {
            new[] { "a sprinkle of ", "ひと振りの" },
            new[] { "a smidgen of ", "ほんの少しの" },
            new[] { "a pinch of ", "ひとつまみの" },
            new[] { "a dash of ", "少々の" },
            new[] { "a dram of ", "1ドラムの" },
            new[] { "a nip of ", "ひとかけの" },
            new[] { "some ", "いくらかの" },
        };

        private static readonly string[] Templates =
        {
            "{0}を鍋に入れてかき混ぜた。",
            "食事のために手に入るものを集めた：{0}。\n\nそれを鍋に入れてかき混ぜた。",
            "あたりを漁って、材料を見つけた：{0}。\n\nそれを鍋に入れてかき混ぜた。",
            "食材をいくらか集めた：{0}。\n\nそれを鍋に入れてかき混ぜた。",
        };

        private static readonly Regex Markup = new Regex(@"^\{\{([^|{}]*)\|(.*)\}\}$", RegexOptions.Compiled);
        private static readonly Regex Possessive = new Regex(@"^(.+?)'s? (.+)$", RegexOptions.Compiled);
        private static readonly Regex Dice = new Regex(@"^(\w+)-sided dice?$", RegexOptions.Compiled);
        private static readonly Regex OfPhrase = new Regex(@"^(.+?) of (.+)$", RegexOptions.Compiled);

        [ModSensitiveCacheInit]
        public static void Load()
        {
            Dictionary<string, string> w = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string dir = UITranslation.ModDirectory();
                string path = dir == null ? null : Path.Combine(dir, "Localization", WordFile);
                if (path != null && File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        int tab = line.IndexOf('\t');
                        if (tab > 0 && !line.StartsWith("#"))
                        {
                            w[line.Substring(0, tab)] = line.Substring(tab + 1);
                        }
                    }
                    Debug.Log("[QudJPLite] 料理の材料の訳を読み込みました: " + w.Count + " 件");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 料理の材料の訳の読み込みエラー: " + ex);
            }
            words = w;
        }

        public static string DescribeMeal(IReadOnlyList<GameObject> mealObjects)
        {
            string[] parts = Campfire.RollIngredients(Stat.Random(3, 4), mealObjects);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Ingredient(parts[i]);
            }
            string list;
            if (parts.Length == 1)
            {
                list = parts[0];
            }
            else
            {
                list = string.Join("、", parts, 0, parts.Length - 1) + "と" + parts[parts.Length - 1];
            }
            return string.Format(Templates[Stat.Random(0, Templates.Length - 1)], list);
        }

        // 材料1つ（"a pinch of potent dog hair" など）を訳す。訳せない部分は英語のまま残す
        public static string Ingredient(string en)
        {
            if (string.IsNullOrEmpty(en))
            {
                return en;
            }
            string rest = en.Trim();
            string amount = "";
            foreach (string[] a in Amounts)
            {
                if (rest.StartsWith(a[0], StringComparison.OrdinalIgnoreCase))
                {
                    amount = a[1];
                    rest = rest.Substring(a[0].Length);
                    break;
                }
            }
            rest = StripArticle(rest);
            string ja = Phrase(rest);
            if (ja == null)
            {
                lock (logged)
                {
                    if (logged.Add(en))
                    {
                        Debug.Log("[QudJPLite] 料理の材料を訳せませんでした: " + en);
                    }
                }
                ja = rest;
            }
            return amount + ja;
        }

        private static string StripArticle(string s)
        {
            if (s.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
            {
                return s.Substring(2);
            }
            if (s.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
            {
                return s.Substring(3);
            }
            if (s.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            {
                return s.Substring(4);
            }
            return s;
        }

        // 名詞句を訳す。訳せなければ null
        private static string Phrase(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            if (!HasLatinText(s))
            {
                return s;   // 本物の材料（アイテム名・液体名）は日本語になっている
            }
            string ja = Lookup(s);
            if (ja != null)
            {
                return ja;
            }

            Match m = Markup.Match(s);
            if (m.Success)
            {
                string inner = Phrase(m.Groups[2].Value);
                return inner == null ? null : "{{" + m.Groups[1].Value + "|" + inner + "}}";
            }
            if (s.StartsWith("congealed ", StringComparison.Ordinal))
            {
                string t = Phrase(StripArticle(s.Substring(10)));
                return t == null ? null : "凝固した" + t;
            }
            if (s.StartsWith("page torn from ", StringComparison.Ordinal))
            {
                string t = Phrase(StripArticle(s.Substring(15)));
                return t == null ? null : t + "から破り取ったページ";
            }
            if (s.StartsWith("eroded remains of ", StringComparison.Ordinal))
            {
                string t = Phrase(StripArticle(s.Substring(18)));
                return t == null ? null : t + "の朽ちた残骸";
            }
            m = Dice.Match(s);
            if (m.Success)
            {
                string n = Lookup(m.Groups[1].Value);
                return n == null ? null : n + "面ダイス";
            }
            m = Possessive.Match(s);
            if (m.Success)
            {
                string owner = Phrase(StripArticle(m.Groups[1].Value));
                string part = Phrase(m.Groups[2].Value);
                if (owner != null && part != null)
                {
                    return owner + "の" + part;
                }
            }

            // 形容詞 + 名詞（形容詞は複数語のこともある）
            for (int i = s.IndexOf(' '); i > 0; i = s.IndexOf(' ', i + 1))
            {
                string adj;
                if (!words.TryGetValue(s.Substring(0, i), out adj))
                {
                    continue;
                }
                string noun = Phrase(s.Substring(i + 1));
                if (noun != null)
                {
                    return Adjective(adj) + noun;
                }
            }

            // 「X of Y」→「YのX」（puff of gas など）
            m = OfPhrase.Match(s);
            if (m.Success)
            {
                string x = Phrase(m.Groups[1].Value);
                string y = Phrase(StripArticle(m.Groups[2].Value));
                if (x != null && y != null)
                {
                    return y + "の" + x;
                }
            }

            // 複数形（objectNouns.pluralize）
            string single = Singular(s);
            if (single != null)
            {
                return Lookup(single);
            }
            return null;
        }

        private static string Lookup(string s)
        {
            string ja;
            if (words.TryGetValue(s, out ja))
            {
                return ja;
            }
            ja = UITranslation.Translate(s);
            if (!ReferenceEquals(ja, s) && ja != s && JapaneseWrap.HasJapanese(ja))
            {
                return ja;
            }
            // 体の部位などは UI 辞書に大文字始まりで入っている（Left Hand）
            string title = TitleCase(s);
            if (title != s)
            {
                ja = UITranslation.Translate(title);
                if (ja != title && JapaneseWrap.HasJapanese(ja))
                {
                    return ja;
                }
            }
            return null;
        }

        // 名詞形の訳を名詞にかかる形にする（銅 → 銅の）。ひらがなで終わる訳（強力な・古い）はそのまま
        private static string Adjective(string ja)
        {
            if (ja.Length == 0)
            {
                return ja;
            }
            char c = ja[ja.Length - 1];
            if (c >= 'ぁ' && c <= 'ゖ')
            {
                return ja;
            }
            return ja + "の";
        }

        private static string Singular(string s)
        {
            if (s.EndsWith("ies", StringComparison.Ordinal) && s.Length > 4)
            {
                return s.Substring(0, s.Length - 3) + "y";
            }
            if (s.EndsWith("es", StringComparison.Ordinal) && s.Length > 3 && words.ContainsKey(s.Substring(0, s.Length - 2)))
            {
                return s.Substring(0, s.Length - 2);
            }
            if (s.EndsWith("s", StringComparison.Ordinal) && s.Length > 2)
            {
                return s.Substring(0, s.Length - 1);
            }
            return null;
        }

        private static string TitleCase(string s)
        {
            StringBuilder sb = new StringBuilder(s);
            for (int i = 0; i < sb.Length; i++)
            {
                if ((i == 0 || sb[i - 1] == ' ') && char.IsLower(sb[i]))
                {
                    sb[i] = char.ToUpperInvariant(sb[i]);
                }
            }
            return sb.ToString();
        }

        // 色の指定（{{w|…}} の w など）を除いて、英字が残るか
        private static bool HasLatinText(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    int bar = s.IndexOf('|', i + 2);
                    if (bar < 0)
                    {
                        return true;
                    }
                    i = bar;
                    continue;
                }
                if (c == '&' || c == '^')
                {
                    i++;   // &Y などの色コード
                    continue;
                }
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Campfire), nameof(Campfire.DescribeMeal))]
    public static class CampfireDescribeMealPatch
    {
        public static bool Prefix(IReadOnlyList<GameObject> mealObjects, ref string __result)
        {
            try
            {
                __result = CookingTranslation.DescribeMeal(mealObjects);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 料理の説明の翻訳エラー: " + ex);
                return true;
            }
        }
    }
}
