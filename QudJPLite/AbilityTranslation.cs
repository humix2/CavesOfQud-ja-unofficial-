using System;
using HarmonyLib;
using UnityEngine;
using XRL.World;
using XRL.World.Parts;

namespace QudJPLite
{
    // アビリティ名（アビリティバー・アビリティ一覧）の日本語化。
    // アビリティバーは UITextSkin を通らずに文字を設定するので、表示側ではなく名前そのものを訳す。
    // 訳すのは「アビリティが作られるとき」と「セーブデータから読み込まれるとき」の2か所だけ。
    [HarmonyPatch(typeof(ActivatedAbilities), nameof(ActivatedAbilities.AddAbility))]
    public static class AddAbilityPatch
    {
        public static void Prefix(ref string Name, ref string Description)
        {
            try
            {
                Name = UITranslation.Translate(Name);
                Description = UITranslation.Translate(Description);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] アビリティ名の翻訳エラー: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ActivatedAbilityEntry), nameof(ActivatedAbilityEntry.Read))]
    public static class AbilityEntryReadPatch
    {
        public static void Postfix(ActivatedAbilityEntry __instance)
        {
            try
            {
                __instance.DisplayName = UITranslation.Translate(__instance.DisplayName);
                __instance.Description = UITranslation.Translate(__instance.Description);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] アビリティ名の翻訳エラー: " + ex);
            }
        }
    }
}
