using System;
using HarmonyLib;
using UnityEngine;
using XRL.World.Text.Delegates;

namespace QudJPLite
{
    // 会話の変数（=EskhindRoadDirection= など）のうち、英単語の方角が入るものを日本語にする。
    // 値はワールド生成時に "north" などで保存されるので、表示のときに置き換える。
    [HarmonyPatch(typeof(VariableReplacers), nameof(VariableReplacers.GameStateEskhindRoadDirection))]
    public static class EskhindRoadDirectionPatch
    {
        public static void Postfix(ref string __result)
        {
            try
            {
                __result = Directions.ToJapanese(__result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 方角の翻訳エラー: " + ex);
            }
        }
    }

    internal static class Directions
    {
        public static string ToJapanese(string dir)
        {
            switch (dir)
            {
                case "north": return "北";
                case "south": return "南";
                case "east": return "東";
                case "west": return "西";
                case "northeast": return "北東";
                case "northwest": return "北西";
                case "southeast": return "南東";
                case "southwest": return "南西";
                default: return dir;
            }
        }
    }
}
