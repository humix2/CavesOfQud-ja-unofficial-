using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using XRL.UI;
using XRL.World;

namespace QudJPLite
{
    // クエスト名（Quest.DisplayName）を訳す。「Visit ダマス」のように、村や史跡の名前から作られる動的クエストは英語のまま。
    // 名前（Quest.Name）そのものはクエストの ID にも使われるので書き換えず、表示用の DisplayName だけを訳す
    [HarmonyPatch(typeof(Quest), nameof(Quest.DisplayName), MethodType.Getter)]
    public static class QuestDisplayNamePatch
    {
        public static void Postfix(Quest __instance, ref string __result)
        {
            try
            {
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }
                string ja = UITranslation.Translate(name);
                if (!ReferenceEquals(ja, name) && ja != name)
                {
                    __result = "{{W|" + ja + "}}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] クエスト名の翻訳エラー: " + ex);
            }
        }
    }

    // クエスト画面の各ステップ。ゲームはステップの名前と本文を英語の幅で折り返し、記号と色を付けてから行に分けるので、
    // 表示の行になってからでは型に当たらない。行を作る間だけ名前と本文を訳文に差し替え、終わったら戻す（セーブには残さない）
    [HarmonyPatch(typeof(QuestLog), nameof(QuestLog.GetLinesForQuest))]
    public static class QuestLogLinesPatch
    {
        public struct Saved
        {
            public QuestStep Step;
            public string Name;
            public string Text;
        }

        public static void Prefix(Quest Q, out List<Saved> __state)
        {
            __state = null;
            try
            {
                if (Q?.StepsByID == null)
                {
                    return;
                }
                foreach (QuestStep step in Q.StepsByID.Values)
                {
                    string name = Tr(step.Name);
                    string text = Tr(step.Text);
                    if (ReferenceEquals(name, step.Name) && ReferenceEquals(text, step.Text))
                    {
                        continue;
                    }
                    (__state ?? (__state = new List<Saved>())).Add(new Saved { Step = step, Name = step.Name, Text = step.Text });
                    step.Name = name;
                    step.Text = text;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] クエストのステップの翻訳エラー: " + ex);
            }
        }

        // 例外が出ても必ず元に戻す
        public static void Finalizer(List<Saved> __state)
        {
            if (__state == null)
            {
                return;
            }
            foreach (Saved s in __state)
            {
                s.Step.Name = s.Name;
                s.Step.Text = s.Text;
            }
        }

        // 訳せなければ同じ参照を返す
        private static string Tr(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            string ja = UITranslation.Translate(s);
            return ja == s ? s : ja;
        }
    }
}
