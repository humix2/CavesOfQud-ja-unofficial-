using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using XRL.World.Parts;

namespace QudJPLite
{
    // 本「家宝目録（Heirlooms of Qud）」。派閥ごとの好みの品を点線でつないだ一覧の本。
    // ゲームは半角の文字数で点線の長さを決めるので、日本語の名前が入ると行がはみ出す。
    // 本を作るときに1回だけ、名前を訳して見た目の幅で点線を引き直す。
    [HarmonyPatch(typeof(HeirloomsBook), nameof(HeirloomsBook.GetContents))]
    public static class HeirloomsBookPatch
    {
        private const string Title = "家宝目録";

        private const string Intro =
            "物への執着とは奇妙なものだ。私たちが物と結ぶ絆は、しばしば金銭の価値を超える。それは歴史に浸され、伝統にくるまれていて、理屈では説明がつかない。\n\n" +
            "だから、クッドに数多くある派閥がそれぞれ特定の種類の品を好むという私の調査結果も、驚くには当たらない。こうした好みは、どれも記憶から失われた何かの出来事によって形づくられたに違いなく、大切な家宝への崇敬として今も受け継がれている。\n\n" +
            "後世のために、これらの好みを目録にまとめておく。\n\n" +
            "[{{c|シェバ・ハガディアスの覚え書きより編纂}}]\n";

        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>
        {
            { "long blades", "長剣" }, { "short blades", "短剣" }, { "axes", "斧" }, { "cudgels", "棍棒" },
            { "pistols", "拳銃" }, { "rifles", "ライフル" }, { "shields", "盾" }, { "helms", "兜" },
            { "gloves", "手袋" }, { "shoes", "靴" }, { "chest pieces", "胴鎧" }, { "bracelets", "腕輪" },
            { "cloaks", "外套" },
        };

        // 「名前.....品目」の行
        private static readonly Regex CatalogLine = new Regex(@"^\s*(.+?)\.{3,}(.+?)\s*$", RegexOptions.CultureInvariant);

        // 1行の見た目の幅（半角 = 1）。英語版の点線の行と同じ 75 に合わせる
        private const double LineWidth = 70;

        public static void Postfix(List<IBookContents.BookPageInfo> __result)
        {
            try
            {
                if (__result == null || __result.Count == 0)
                {
                    return;
                }
                __result[0].Text = Intro;
                foreach (IBookContents.BookPageInfo page in __result)
                {
                    page.Title = Title;
                    if (page == __result[0] || string.IsNullOrEmpty(page.Text))
                    {
                        continue;
                    }
                    string[] lines = page.Text.Replace("\r", "").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        Match m = CatalogLine.Match(lines[i]);
                        if (m.Success)
                        {
                            lines[i] = BuildLine(FactionName(m.Groups[1].Value.Trim()), CategoryName(m.Groups[2].Value.Trim()));
                        }
                    }
                    page.Text = string.Join("\n", lines);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 家宝目録の翻訳エラー: " + ex);
            }
        }

        private static string BuildLine(string name, string category)
        {
            string left = "  " + name;
            double rest = LineWidth - BookWrap.VisualWidth(left) - BookWrap.VisualWidth(category);
            int dots = Math.Max(3, (int)Math.Round(rest));
            return left + new string('.', dots) + category;
        }

        private static string CategoryName(string s)
        {
            string ja;
            return Categories.TryGetValue(s, out ja) ? ja : UITranslation.Translate(s);
        }

        // 派閥名。辞書にあればその訳、村や教団は「〇〇の村人」「〇〇の教団」。
        // 歴史から自動生成される派閥（Foretold Skull Clan など）は英語のまま
        private static string FactionName(string s)
        {
            string ja = UITranslation.Translate(s);
            if (!ReferenceEquals(ja, s) && ja != s)
            {
                return ja;
            }
            if (s.StartsWith("villagers of "))
            {
                return UITranslation.Translate(s.Substring(13)) + "の村人";
            }
            if (s.StartsWith("Cult of "))
            {
                return UITranslation.Translate(s.Substring(8)) + "の教団";
            }
            return s;
        }
    }
}
