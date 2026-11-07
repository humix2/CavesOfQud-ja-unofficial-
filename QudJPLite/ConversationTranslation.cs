using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using XRL.World;
using XRL.World.Conversations;

namespace QudJPLite
{
    // 会話の選択肢の後ろに付く [begin trade] [End] などのタグを日本語にする。
    // タグはゲームのコードが英語で組み立てるので、組み立てた後（Choice.GetTag の戻り値）を訳す
    [HarmonyPatch(typeof(Choice), nameof(Choice.GetTag))]
    public static class ChoiceTagPatch
    {
        public static void Postfix(ref string __result)
        {
            try
            {
                __result = ChoiceTags.Translate(__result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 選択肢タグの翻訳エラー: " + ex);
            }
        }
    }

    // ベイ・ラーの村人のうわさ話（「血縁と同族」の手がかり）は、ゲームのコードに英語で書かれていて
    // セーブにもその英文が入る。村人に話しかけて取り出す瞬間に、UI 辞書（tools/ui-overrides.tsv）で訳す
    [HarmonyPatch(typeof(KithAndKinGameState), nameof(KithAndKinGameState.getRumorForVillagerCategory))]
    public static class KithAndKinRumorPatch
    {
        public static void Postfix(HindrenClueRumor __result)
        {
            try
            {
                if (__result != null && !string.IsNullOrEmpty(__result.text))
                {
                    __result.text = UITranslation.Translate(__result.text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] うわさ話の翻訳エラー: " + ex);
            }
        }
    }

    // 名前の前に付く称号（Warden など）と、コードで直接付けられる名前（Hindriarch Esk）を日本語にする
    [HarmonyPatch(typeof(GetDisplayNameEvent), nameof(GetDisplayNameEvent.GetFor))]
    public static class DisplayNamePatch
    {
        public static void Postfix(ref string __result, bool ColorOnly)
        {
            if (ColorOnly || string.IsNullOrEmpty(__result))
            {
                return;
            }
            try
            {
                __result = DisplayNames.Fix(__result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 名前の翻訳エラー: " + ex);
            }
        }
    }

    internal static class DisplayNames
    {
        // 名前が日本語のときだけ称号を訳す（英語名のままの相手には付けない）
        private static readonly Regex PrefixTitle = new Regex(
            @"(?<![A-Za-z])(Wardens?|Mayor|Ciderer) (?=[　-￿])", RegexOptions.CultureInvariant);

        // Elder は日本語では後ろに付ける（イルーダッド長老）
        private static readonly Regex Elder = new Regex(
            @"(?<![A-Za-z])Elder ([　-￿]+)", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
        {
            { "Warden", "監視官" },
            { "Wardens", "監視官" },
            { "Mayor", "村長" },
            { "Ciderer", "醸造師" },
        };

        public static string Fix(string name)
        {
            if (name.IndexOf("Hindriarch Esk", StringComparison.Ordinal) >= 0)
            {
                name = name.Replace("Hindriarch Esk", "ヒンドリアーク・エスク");
            }
            if (name.IndexOf("Warden", StringComparison.Ordinal) >= 0
                || name.IndexOf("Mayor", StringComparison.Ordinal) >= 0
                || name.IndexOf("Ciderer", StringComparison.Ordinal) >= 0)
            {
                name = PrefixTitle.Replace(name, m => Titles[m.Groups[1].Value]);
            }
            if (name.IndexOf("Elder ", StringComparison.Ordinal) >= 0)
            {
                name = Elder.Replace(name, "$1長老");
            }
            return name;
        }
    }

    internal static class ChoiceTags
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "[End]", "[終了]" },
            { "[begin trade]", "[取引を始める]" },
            { "[begin water ritual]", "[水の儀式を始める]" },
            { "[Accept Quest]", "[クエストを受ける]" },
            { "[Accept Quest - level-based reward]", "[クエストを受ける - 報酬はレベルに応じる]" },
            { "[Complete Quest Step]", "[クエストのステップを完了]" },
            { "[Complete Quest]", "[クエストを完了]" },
            { "[Complete Quest - level-based reward]", "[クエストを完了 - 報酬はレベルに応じる]" },
            { "[Fight]", "[戦う]" },
            { "[give artifact]", "[遺物を渡す]" },
            { "[Give Books]", "[本を渡す]" },
            { "[Build Golem]", "[ゴーレムを造る]" },
            { "[Share secrets from Resheph's life]", "[レシェフの生涯の秘密を伝える]" },
            { "[Select limb to infect]", "[感染させる部位を選ぶ]" },
            { "[lesser victory]", "[小さな勝利]" },
            { "[victory]", "[勝利]" },
            { "[greater victory]", "[大いなる勝利]" },
        };

        // {{c|1}} のような色付きの数値
        private const string Num = @"\{\{\w+\|[+-]?\d+(?:\{\{c\|[+-]\d+\}\})?\}\}";

        private static readonly Regex WaterRitualCost = new Regex(
            @"^\[begin water ritual; (" + Num + @") dram of (.+)\]$", RegexOptions.CultureInvariant);

        private static readonly Regex Reputation = new Regex(
            "(" + Num + ") reputation", RegexOptions.CultureInvariant);

        private static readonly Regex SkillPointCost = new Regex(
            @", \{\{C\|-(\d+)\}\} SP", RegexOptions.CultureInvariant);

        private static readonly Regex Prefixed = new Regex(
            @"^\[(learn to cook|learn|gain|become infected with) (.+?): (.+)\]$", RegexOptions.CultureInvariant);

        private static readonly Regex Standing = new Regex(
            @"^\[(Hated by|Disliked by|Indifferent to|Liked by|Loved by) (.+)\]$", RegexOptions.CultureInvariant);

        public static string Translate(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag.IndexOf('[') < 0)
            {
                return tag;
            }
            // 全体を囲む {{色|...}} を外して中身を訳す
            string prefix = "";
            string inner = tag;
            string suffix = "";
            if (tag.StartsWith("{{") && tag.EndsWith("}}"))
            {
                int bar = tag.IndexOf('|');
                if (bar > 2 && bar < 12)
                {
                    prefix = tag.Substring(0, bar + 1);
                    inner = tag.Substring(bar + 1, tag.Length - bar - 3);
                    suffix = "}}";
                }
            }
            string ja = TranslateInner(inner);
            return ReferenceEquals(ja, inner) ? tag : prefix + ja + suffix;
        }

        private static string TranslateInner(string s)
        {
            string ja;
            if (Exact.TryGetValue(s, out ja))
            {
                return ja;
            }
            Match m = WaterRitualCost.Match(s);
            if (m.Success)
            {
                return "[水の儀式を始める：" + Liquid(m.Groups[2].Value) + m.Groups[1].Value + "ドラム]";
            }
            m = Standing.Match(s);
            if (m.Success)
            {
                string who = UITranslation.Translate(m.Groups[2].Value);
                switch (m.Groups[1].Value)
                {
                    case "Hated by": return "[" + who + "に憎まれている]";
                    case "Disliked by": return "[" + who + "に嫌われている]";
                    case "Indifferent to": return "[" + who + "とは可もなく不可もない]";
                    case "Liked by": return "[" + who + "に好かれている]";
                    default: return "[" + who + "に慕われている]";
                }
            }
            if (s.IndexOf(" reputation", StringComparison.Ordinal) < 0)
            {
                return s;
            }
            string r = Reputation.Replace(s, "評判 $1");
            r = SkillPointCost.Replace(r, "、スキルポイント {{C|-$1}}");
            r = r.Replace("{{W|skill points}}", "{{W|スキルポイント}}");
            m = Prefixed.Match(r);
            if (m.Success)
            {
                string what = UITranslation.Translate(m.Groups[2].Value);
                string rest = m.Groups[3].Value;
                switch (m.Groups[1].Value)
                {
                    case "learn to cook": return "[" + what + "の作り方を学ぶ：" + rest + "]";
                    case "learn": return "[" + what + "を習得：" + rest + "]";
                    case "gain": return "[" + what + "を得る：" + rest + "]";
                    default: return "[" + what + "に感染する：" + rest + "]";
                }
            }
            return r;
        }

        // 液体名（{{B|water}} など）。辞書の "water" は「水の」（形容詞形）なので先に拾う
        private static string Liquid(string name)
        {
            string plain = name;
            string open = "";
            string close = "";
            if (name.StartsWith("{{") && name.EndsWith("}}"))
            {
                int bar = name.IndexOf('|');
                if (bar > 2)
                {
                    open = name.Substring(0, bar + 1);
                    plain = name.Substring(bar + 1, name.Length - bar - 3);
                    close = "}}";
                }
            }
            string ja;
            switch (plain)
            {
                case "water": ja = "水"; break;
                case "fresh water": ja = "真水"; break;
                default: ja = UITranslation.Translate(plain); break;
            }
            return open + ja + close + " ";
        }
    }
}
