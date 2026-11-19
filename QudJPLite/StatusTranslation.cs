using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Qud.UI;
using UnityEngine;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace QudJPLite
{
    // 上部ステータスバー（満腹度・渇き・時刻と日付）、ACTIVE EFFECTS と TARGET の行、効果一覧の画面。
    // どれも部品を1本の文字列につないでから表示するので、辞書の完全一致では引けない。
    // そこで、つなぐ前の部品（FoodStatus() や GetDescription() の戻り値）に訳す呼び出しを差し込む。
    // 結果は覚えておくので、毎フレーム呼ばれる処理でも2回目以降は辞書を1回引くだけ。
    public static class StatusText
    {
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string T(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            lock (cache)
            {
                string r;
                if (cache.TryGetValue(s, out r))
                {
                    return r;
                }
                try
                {
                    r = UITranslation.Translate(s);
                    if (ReferenceEquals(r, s))
                    {
                        // 液体まみれの効果（LiquidCovered）の説明は液体の語をつないだもの
                        r = LiquidWords.Predicate(s) ?? s;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[QudJPLite] ステータスの翻訳エラー: " + ex);
                    r = s;
                }
                if (cache.Count > 4096)
                {
                    cache.Clear();
                }
                cache[s] = r;
                return r;
            }
        }

        // 日付: "6th of Ut yara Ux" → "ウト・ヤラ・ウクス 6日"
        public static string MonthDay()
        {
            string day = Calendar.GetDay();
            string month = T(Calendar.GetMonth());
            if (day == "Ides")
            {
                return month + " イデスの日";
            }
            int n = 0;
            while (n < day.Length && char.IsDigit(day[n]))
            {
                n++;
            }
            return n > 0 ? month + " " + day.Substring(0, n) + "日" : month + " " + T(day);
        }

        public static string Empty()
        {
            return "";
        }

        public static readonly Dictionary<string, string> Literals = new Dictionary<string, string>
        {
            { "{{Y|<color=#508d75>ACTIVE EFFECTS:</color>}} ", "{{Y|<color=#508d75>発動中の効果:</color>}} " },
            { "{{C|<color=#3e83a5>TARGET:</color> ", "{{C|<color=#3e83a5>ターゲット:</color> " },
            { "{{K|TARGET: [none]}}", "{{K|ターゲット: [なし]}}" },
            { ", ", "、" },
            { "&WActive Effects&Y - ", "&W発動中の効果&Y - " },
            { "No active effects.", "発動中の効果はない。" },
        };

        private static readonly MethodInfo TMethod = AccessTools.Method(typeof(StatusText), nameof(T));

        // 指定したメソッドの呼び出しの直後に T() を差し込み、文字列定数を Literals で差し替える
        public static IEnumerable<CodeInstruction> TranslateAfter(IEnumerable<CodeInstruction> instructions, params MethodInfo[] targets)
        {
            foreach (CodeInstruction ins in instructions)
            {
                string s;
                if (ins.opcode == OpCodes.Ldstr && ins.operand is string key && Literals.TryGetValue(key, out s))
                {
                    ins.operand = s;
                }
                yield return ins;
                foreach (MethodInfo m in targets)
                {
                    if (m != null && ins.Calls(m))
                    {
                        yield return new CodeInstruction(OpCodes.Call, TMethod);
                        break;
                    }
                }
            }
        }

        // args を省略すると引数を問わず名前で探す。引数なしのものに限るときは Type.EmptyTypes を渡す
        public static MethodInfo Find(Type t, string name, Type[] args = null)
        {
            MethodInfo m = AccessTools.Method(t, name, args);
            if (m == null)
            {
                Debug.LogWarning("[QudJPLite] メソッドが見つかりません: " + t.Name + "." + name);
            }
            return m;
        }
    }

    // 上部ステータスバー（ターンの終わりに1回組み立てる）
    [HarmonyPatch]
    public static class PlayerStatusBarPatch
    {
        // 対象が無いと Harmony が例外を投げ、ほかのパッチまで止まるので、その場合はこのパッチだけ飛ばす
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(PlayerStatusBar), "BeginEndTurn") != null;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo m = StatusText.Find(typeof(PlayerStatusBar), "BeginEndTurn");
            if (m != null)
            {
                yield return m;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getDay = StatusText.Find(typeof(Calendar), nameof(Calendar.GetDay), Type.EmptyTypes);
            MethodInfo getMonth = StatusText.Find(typeof(Calendar), nameof(Calendar.GetMonth), Type.EmptyTypes);
            MethodInfo monthDay = AccessTools.Method(typeof(StatusText), nameof(StatusText.MonthDay));
            MethodInfo empty = AccessTools.Method(typeof(StatusText), nameof(StatusText.Empty));
            IEnumerable<CodeInstruction> list = StatusText.TranslateAfter(instructions,
                StatusText.Find(typeof(Stomach), nameof(Stomach.FoodStatus)),
                StatusText.Find(typeof(Stomach), nameof(Stomach.WaterStatus)),
                StatusText.Find(typeof(Calendar), nameof(Calendar.GetTime), Type.EmptyTypes));
            // 「時刻 日 of 月」を「時刻 月 日」に並べ替える: GetDay() → MonthDay()、" of " → ""、GetMonth() → ""
            foreach (CodeInstruction ins in list)
            {
                if (getDay != null && getMonth != null && ins.Calls(getDay))
                {
                    ins.operand = monthDay;
                }
                else if (getDay != null && getMonth != null && ins.Calls(getMonth))
                {
                    ins.operand = empty;
                }
                else if (getDay != null && getMonth != null && ins.opcode == OpCodes.Ldstr && (ins.operand as string) == " of ")
                {
                    ins.operand = "";
                }
                yield return ins;
            }
        }
    }

    // アビリティバーの上の ACTIVE EFFECTS と TARGET の行
    [HarmonyPatch]
    public static class AbilityBarEffectsPatch
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(AbilityBar), "AfterRender") != null || AccessTools.Method(typeof(AbilityBar), "InternalUpdateActiveEffects") != null;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "AfterRender", "InternalUpdateActiveEffects" })
            {
                MethodInfo m = StatusText.Find(typeof(AbilityBar), name);
                if (m != null)
                {
                    yield return m;
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return StatusText.TranslateAfter(instructions,
                StatusText.Find(typeof(Effect), nameof(Effect.GetDescription)),
                StatusText.Find(typeof(Strings), nameof(Strings.WoundLevel), new[] { typeof(XRL.World.GameObject) }),
                StatusText.Find(typeof(Description), nameof(Description.GetFeelingDescription), new[] { typeof(XRL.World.GameObject) }),
                StatusText.Find(typeof(Description), nameof(Description.GetDifficultyDescription), new[] { typeof(XRL.World.GameObject) }));
        }
    }

    // 効果一覧の画面（ACTIVE EFFECTS の行をクリックしたときなど）
    [HarmonyPatch(typeof(XRL.World.GameObject), nameof(XRL.World.GameObject.ShowActiveEffects))]
    public static class ShowActiveEffectsPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return StatusText.TranslateAfter(instructions,
                StatusText.Find(typeof(Effect), nameof(Effect.GetDescription)),
                StatusText.Find(typeof(Campfire), nameof(Campfire.ProcessEffectDescription)));
        }
    }
}

namespace QudJPLite
{
    // 見る（Look）のポップアップとアイテムのツールチップ。敵対・難易度・負傷の度合いと、本文の後ろに足される行
    // （Base demeanor / Physical features / Equipped / 出身の次元）を、組み立てが終わった後に1回だけ訳す
    [HarmonyPatch(typeof(XRL.UI.Look), nameof(XRL.UI.Look.GenerateTooltipInformation))]
    public static class LookTooltipPatch
    {
        public static void Postfix(ref XRL.UI.Look.TooltipInformation __result)
        {
            try
            {
                __result.FeelingText = UITranslation.Translate(__result.FeelingText);
                __result.DifficultyText = UITranslation.Translate(__result.DifficultyText);
                __result.WoundLevel = UITranslation.Translate(__result.WoundLevel);
                __result.LongDescription = MessageTranslation.Translate(__result.LongDescription);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[QudJPLite] 見る画面の翻訳エラー: " + ex);
            }
        }
    }
}

namespace QudJPLite
{
    // 見る画面の本文の最後にある効果の並び（dazed, bleeding, …）。つなぐ前の Effect.GetDescription() の戻り値を訳す。
    // GetLongDescription は同名が2つ（引数なし / StringBuilder）あるので、引数の型で1つに絞る
    [HarmonyPatch(typeof(XRL.World.Parts.Description), nameof(XRL.World.Parts.Description.GetLongDescription), new System.Type[] { typeof(System.Text.StringBuilder) })]
    public static class LookEffectsPatch
    {
        public static System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> Transpiler(System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> instructions)
        {
            return StatusText.TranslateAfter(instructions,
                StatusText.Find(typeof(XRL.World.Effect), nameof(XRL.World.Effect.GetDescription)));
        }
    }
}
