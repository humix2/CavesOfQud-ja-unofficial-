using System;
using System.Collections.Generic;
using System.Text;

namespace QudJPLite
{
    // 液体が付いたときの形容詞を訳す（ゲームは液体ごとの語をつないで作るので、辞書の完全一致では引けない）。
    //   付着（LiquidVolume.SmearedName）: "salty {{G|gooey}}"          → "{{Y|塩}}と{{G|粘液}}まみれの"
    //   染み（LiquidVolume.StainedName）: "{{K|ooze}}-and-{{G|goo}}-stained" → "{{K|黒い粘液}}と{{G|緑の粘液}}で汚れた"
    // 名前の形容詞（NameTranslation）と、ACTIVE EFFECTS の行（LiquidCovered の説明）で使う。
    public static class LiquidWords
    {
        // 付着の語（液体の SmearedName と、混ざった液体の形容詞）→ 液体の名詞
        private static readonly Dictionary<string, string> Smear = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "acid-covered", "酸" }, { "acidic", "酸" }, { "algae-covered", "藻" }, { "algal", "藻" },
            { "tarred", "タール" }, { "tarry", "タール" }, { "bloody", "血" }, { "nervous", "脳髄汁" },
            { "fragrant", "サイダー" }, { "spiced", "サイダー" }, { "homogenized", "クローン薬液" }, { "luminous", "コンバレセンス" },
            { "unctuous", "ゲル" }, { "gooey", "粘液" }, { "sticky", "ハチミツ" }, { "honeyed", "ハチミツ" },
            { "inky", "インク" }, { "lava-covered", "溶岩" }, { "magmatic", "溶岩" }, { "neutral", "中性子" }, { "neutronic", "中性子" },
            { "oily", "油" }, { "oozing", "黒い粘液" }, { "soupy", "原始スープ" }, { "putrid", "腐敗液" },
            { "salt-encrusted", "塩" }, { "salty", "塩" }, { "brackish", "塩" }, { "sappy", "樹液" }, { "sugary", "樹液" },
            { "slimy", "スライム" }, { "sludgy", "ヘドロ" }, { "radiant", "サンスラグ" }, { "entropic", "ウォームスタティック" },
            { "waxy", "蝋" }, { "waxen", "蝋" }, { "lush", "ワイン" }, { "wet", "水" }, { "dilute", "水" }, { "liquid-covered", "液体" },
        };

        // 染みの語（液体の StainedName）→ 液体の名詞
        private static readonly Dictionary<string, string> Stain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "acid", "酸" }, { "algae", "藻" }, { "tar", "タール" }, { "blood", "血" }, { "brain-brine", "脳髄汁" },
            { "cider", "サイダー" }, { "cloning-draught", "クローン薬液" }, { "convalessence", "コンバレセンス" }, { "gel", "ゲル" },
            { "goo", "緑の粘液" }, { "honey", "ハチミツ" }, { "ink", "インク" }, { "lava", "溶岩" }, { "flux", "中性子フラックス" },
            { "oil", "油" }, { "ooze", "黒い粘液" }, { "soup", "原始スープ" }, { "putrescence", "腐敗液" }, { "salt", "塩" },
            { "sap", "樹液" }, { "slime", "スライム" }, { "sludge", "ヘドロ" }, { "sunslag", "サンスラグ" },
            { "warm static", "ウォームスタティック" }, { "water", "水" }, { "wax", "蝋" }, { "wine", "ワイン" },
        };

        private const string StainedSuffix = "-stained";

        // 名詞にかかる形（「〜まみれの」「〜で汚れた」「濡れた」）。液体の語でなければ null
        public static string Adjective(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 200)
            {
                return null;
            }
            // 全体を囲む色 {{X|...}} は外して訳し、付け直す
            if (s.StartsWith("{{", StringComparison.Ordinal) && s.EndsWith("}}", StringComparison.Ordinal))
            {
                int bar = s.IndexOf('|');
                if (bar > 2 && bar < 20 && WrapsAll(s, bar + 1))
                {
                    string inner = Adjective(s.Substring(bar + 1, s.Length - bar - 3));
                    return inner == null ? null : s.Substring(0, bar + 1) + inner + "}}";
                }
            }
            if (s.EndsWith(StainedSuffix, StringComparison.Ordinal))
            {
                string body = s.Substring(0, s.Length - StainedSuffix.Length);
                string nouns = Nouns(body.Split(new[] { "-and-" }, StringSplitOptions.None), Stain);
                return nouns == null ? null : nouns + "で汚れた";
            }
            string[] tokens = s.Split(' ');
            if (tokens.Length == 1)
            {
                string w = Core(tokens[0], out string color);
                if (string.Equals(w, "wet", StringComparison.OrdinalIgnoreCase))
                {
                    return Colored(color, "濡れた");
                }
                if (string.Equals(w, "damp", StringComparison.OrdinalIgnoreCase))
                {
                    return Colored(color, "湿った");
                }
            }
            string smear = Nouns(tokens, Smear);
            return smear == null ? null : smear + "まみれの";
        }

        // ACTIVE EFFECTS の行などの、言い切りの形（「〜まみれ」「〜で汚れている」）
        public static string Predicate(string s)
        {
            string a = Adjective(s);
            if (a == null)
            {
                return null;
            }
            int end = a.Length;
            while (end >= 2 && a[end - 1] == '}' && a[end - 2] == '}')
            {
                end -= 2;
            }
            string head = a.Substring(0, end);
            string rest = a.Substring(end);
            if (head.EndsWith("まみれの", StringComparison.Ordinal))
            {
                head = head.Substring(0, head.Length - 1);
            }
            else if (head.EndsWith("汚れた", StringComparison.Ordinal))
            {
                head = head.Substring(0, head.Length - 1) + "ている";
            }
            else if (head.EndsWith("濡れた", StringComparison.Ordinal) || head.EndsWith("湿った", StringComparison.Ordinal))
            {
                head = head.Substring(0, head.Length - 1) + "ている";
            }
            return head + rest;
        }

        // 語ごとに名詞へ置き換えて「と」でつなぐ。知らない語が1つでもあれば null
        private static string Nouns(string[] parts, Dictionary<string, string> table)
        {
            StringBuilder sb = new StringBuilder();
            List<string> seen = new List<string>();
            foreach (string p in parts)
            {
                if (p.Length == 0)
                {
                    continue;
                }
                string w = Core(p, out string color);
                string ja;
                if (w == null || !table.TryGetValue(w, out ja))
                {
                    return null;
                }
                if (seen.Contains(ja))
                {
                    continue;
                }
                seen.Add(ja);
                if (sb.Length > 0)
                {
                    sb.Append('と');
                }
                sb.Append(Colored(color, ja));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        // "{{G|gooey}}" → "gooey"（color = "G"）。入れ子の色は扱わない（null）
        private static string Core(string token, out string color)
        {
            color = null;
            if (token.StartsWith("{{", StringComparison.Ordinal))
            {
                int bar = token.IndexOf('|');
                if (bar < 3 || !token.EndsWith("}}", StringComparison.Ordinal) || token.IndexOf("{{", 2, StringComparison.Ordinal) >= 0)
                {
                    return null;
                }
                color = token.Substring(2, bar - 2);
                return token.Substring(bar + 1, token.Length - bar - 3);
            }
            return token.IndexOf('{') >= 0 || token.IndexOf('}') >= 0 ? null : token;
        }

        private static string Colored(string color, string s)
        {
            return color == null ? s : "{{" + color + "|" + s + "}}";
        }

        // 先頭の {{X| に対応する }} が文末のものか
        private static bool WrapsAll(string s, int from)
        {
            int depth = 1;
            for (int i = from; i < s.Length - 1; i++)
            {
                if (s[i] == '{' && s[i + 1] == '{')
                {
                    depth++;
                    i++;
                }
                else if (s[i] == '}' && s[i + 1] == '}')
                {
                    depth--;
                    i++;
                    if (depth == 0)
                    {
                        return i == s.Length - 1;
                    }
                }
            }
            return false;
        }
    }
}
