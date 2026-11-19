using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.World.ZoneParts;

namespace QudJPLite
{
    // グリット・ゲート防衛（A Call to Arms）の後のオソの損害報告。
    // ゲームは死者の名前と被害の数（ゲームの記録）から英語の文を組み立てるので、同じ記録から日本語の文を組み立て直す。
    // =name= はゲームの会話の処理が後でプレイヤーの名前に置き換える
    [HarmonyPatch(typeof(ScriptCallToArms), nameof(ScriptCallToArms.GenerateResultConversation))]
    public static class CallToArmsResultPatch
    {
        public static void Postfix(ref string __result)
        {
            try
            {
                __result = Build();
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] オソの損害報告の翻訳エラー: " + ex);
            }
        }

        private static string Build()
        {
            StringBuilder sb = new StringBuilder("=name=、襲撃で受けた損害を確かめた。\n\n");

            List<string> names = new List<string>();
            if (The.Game.HasStringGameState("CallToArmsPersonsKilled"))
            {
                foreach (string n in The.Game.GetStringGameState("CallToArmsPersonsKilled").Split(','))
                {
                    if (!string.IsNullOrEmpty(n) && !names.Contains(n))
                    {
                        names.Add(n);
                    }
                }
            }
            if (names.Count > 0)
            {
                string list = names.Count == 1 ? names[0] : string.Join("、", names.GetRange(0, names.Count - 1).ToArray()) + "と" + names[names.Count - 1];
                sb.Append("何よりもまず、亡くなった友を悼もう。").Append(list).Append("。我々にとって計り知れない損失だ。\n\n");
            }

            // （連用形, 過去形）。数が 0 の被害は挙げない
            List<string[]> losses = new List<string[]>();
            Add(losses, "CallToArmsChromelings", "ロボット{0}体が失われ", "ロボット{0}体が失われた");
            Add(losses, "CallToArmsBooks", "本{0}冊が焼かれ", "本{0}冊が焼かれた");
            Add(losses, "CallToArmsBookshelves", "本棚{0}台が壊され", "本棚{0}台が壊された");
            Add(losses, "CallToArmsCrops", "作物{0}株がだめになり", "作物{0}株がだめになった");
            Add(losses, "CallToArmsGadgets", "装置{0}個が砕かれ", "装置{0}個が砕かれた");
            Add(losses, "CallToArmsFurniture", "家具{0}点が壊され", "家具{0}点が壊された");

            string head = names.Count > 0 ? "そのほかに、" : "";
            if (losses.Count == 0)
            {
                sb.Append(names.Count > 0 ? "そのほかの被害はなかった。" : "被害はなかった。");
            }
            else
            {
                sb.Append(head);
                for (int i = 0; i < losses.Count; i++)
                {
                    bool last = i == losses.Count - 1;
                    sb.Append(losses[i][last ? 1 : 0]).Append(last ? "。" : "、");
                }
            }
            return sb.ToString();
        }

        private static void Add(List<string[]> losses, string state, string cont, string past)
        {
            int n = The.Game.GetIntGameState(state);
            if (n > 0)
            {
                losses.Add(new[] { string.Format(cont, n), string.Format(past, n) });
            }
        }
    }
}
