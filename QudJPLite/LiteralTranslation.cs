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
            { "by class", "種類別" },
        };

        public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ins in instructions)
            {
                string s;
                if (ins.opcode == OpCodes.Ldstr && ins.operand is string key && Map.TryGetValue(key, out s))
                {
                    ins.operand = s;
                }
                yield return ins;
            }
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

    [HarmonyPatch(typeof(AbilityManagerScreen), nameof(AbilityManagerScreen.sortModeDescription), MethodType.Getter)]
    public static class AbilitySortLiteralPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Literals.Replace(instructions);
        }
    }
}
