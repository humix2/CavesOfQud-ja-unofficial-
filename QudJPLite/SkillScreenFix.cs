using System;
using System.Collections.Generic;
using HarmonyLib;
using Qud.UI;
using UnityEngine;
using XRL.UI;

namespace QudJPLite
{
    // スキル画面で、習得済みのスキル名が表示されない問題の修正。
    // 習得済みの行だけ、ゲームは名前を " {{G|名前}}" のように先頭に半角スペースを付けて設定する。
    // 日本語の名前だとこれが表示されなくなるので、先頭のスペースを外して設定し直す（行を作るときに1回だけ）。
    [HarmonyPatch(typeof(SkillsAndPowersLine), nameof(SkillsAndPowersLine.setData))]
    public static class SkillLineNamePatch
    {

        public static void Postfix(SkillsAndPowersLine __instance)
        {
            try
            {
                UITextSkin skin = __instance.skillText;
                string t = skin?.text;
                if (string.IsNullOrEmpty(t) || t[0] != ' ' || !t.StartsWith(" {{") || !JapaneseWrap.HasJapanese(t))
                {
                    return;
                }
                skin.SetText(t.TrimStart(' '));
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] スキル名の表示修正エラー: " + ex);
            }
        }
    }
}
