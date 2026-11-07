using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Qud.UI;

namespace QudJPLite
{
    // ゲームのコードに埋め込まれた短い文字列（[on] [off] [disabled] など）を、読み込み時に差し替える。
    // 処理の中身（IL）の文字列定数を置き換えるだけなので、毎フレーム呼ばれる処理でも負担は増えない。
    internal static class Literals
    {
        public static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { " {{K|[disabled]}}", " {{K|[使用不可]}}" },
            { " [disabled]}}", " [使用不可]}}" },
            { " {{K|[{{g|on}}]}}", " {{K|[{{g|オン}}]}}" },
            { " {{K|[{{y|off}}]}}", " {{K|[{{y|オフ}}]}}" },
            { " [attack]", " [攻撃]" },
            { " [astrally tethered]}}", " [アストラル束縛]}}" },
            { "}} turn cooldown]", "}}ターンのクールダウン]" },
            { "}} turn cooldown, astrally tethered]", "}}ターンのクールダウン、アストラル束縛]" },
            { "sort: ", "並び順: " },
            { "custom", "カスタム" },
            { "by class", "種類別" }, { "a-z", "名前順" }, { "list", "一覧順" },
        };

        // ポップアップの共通ボタン（キー表示と組み合わせて毎回作られるもの）。
        // "Cancel" などの単独の語はボタンの命令名なので触らず、{{ }} 付きの表示用の文字列だけを差し替える
        public static readonly Dictionary<string, string> ButtonMap = BuildButtonMap();

        private static Dictionary<string, string> BuildButtonMap()
        {
            Dictionary<string, string> words = new Dictionary<string, string>
            {
                { "Cancel", "キャンセル" }, { "Accept", "決定" }, { "Submit", "送信" }, { "Yes", "はい" }, { "No", "いいえ" },
                { "Trade", "取引" }, { "Color", "色" }, { "Copy", "コピー" }, { "Hold to Accept", "長押しで決定" },
            };
            Dictionary<string, string> map = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> w in words)
            {
                map["{{y|" + w.Key + "}}"] = "{{y|" + w.Value + "}}";
                map["}} {{y|" + w.Key + "}}"] = "}} {{y|" + w.Value + "}}";
                map["]}} {{y|" + w.Key + "}}"] = "]}} {{y|" + w.Value + "}}";
            }
            map[" Cancel}}"] = " キャンセル}}";
            map["}} Cancel"] = "}} キャンセル";
            map["]}} Cancel"] = "]}} キャンセル";
            map["{{W|press "] = "{{W|";
            map["{{W|press ["] = "{{W|[";
            return map;
        }

        public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions)
        {
            return Replace(instructions, Map);
        }

        public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions, Dictionary<string, string> map)
        {
            foreach (CodeInstruction ins in instructions)
            {
                string s;
                if (ins.opcode == OpCodes.Ldstr && ins.operand is string key && map.TryGetValue(key, out s))
                {
                    ins.operand = s;
                }
                yield return ins;
            }
        }
    }

    // PopupMessage のボタン一覧を返すプロパティ（CancelButton など）すべて
    [HarmonyPatch]
    public static class PopupButtonLiteralPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (PropertyInfo p in typeof(PopupMessage).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                MethodInfo get = p.GetGetMethod(true);
                if (get != null && p.PropertyType == typeof(List<QudMenuItem>))
                {
                    yield return get;
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Literals.Replace(instructions, Literals.ButtonMap);
        }
    }

    [HarmonyPatch(typeof(AbilityBar), nameof(AbilityBar.Update))]
    public static class AbilityBarLiteralPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Literals.Replace(instructions);
        }
    }

    [HarmonyPatch(typeof(AbilityManagerLine), nameof(AbilityManagerLine.setData))]
    public static class AbilityManagerLineLiteralPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Literals.Replace(instructions);
        }
    }

    // 並び順の表示（アビリティ・取引・本・アイテム選択の各画面）
    [HarmonyPatch]
    public static class AbilitySortLiteralPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type t in new[] { typeof(AbilityManagerScreen), typeof(TradeScreen), typeof(BookScreen), typeof(PickGameObjectScreen) })
            {
                MethodInfo get = AccessTools.PropertyGetter(t, "sortModeDescription");
                if (get != null)
                {
                    yield return get;
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Literals.Replace(instructions);
        }
    }
}
