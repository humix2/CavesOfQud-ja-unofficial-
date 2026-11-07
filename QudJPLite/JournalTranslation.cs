using System;
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
