using System;
using HarmonyLib;
using UnityEngine;
using XRL.UI;

namespace QudJPLite
{
    // 端末の画面（グリット・ゲートのエレシュキガル、変容の僻隅など。どれも TerminalScreen の派生）。
    // 新しい UI は本文を RenderedTextForModernUI（MainText を英語の 67 桁で折り返したもの）で、選択肢を Options で読む。
    // 折り返した後では行が途中で切れて訳を引けないので、MainText を訳してから日本語の幅で折り返す。
    [HarmonyPatch(typeof(TerminalScreen), nameof(TerminalScreen.RenderedTextForModernUI), MethodType.Getter)]
    public static class TerminalTextPatch
    {
        // 英語版の 67 桁に収まる全角の文字数
        private const int Columns = 40;

        public static bool Prefix(TerminalScreen __instance, ref string __result)
        {
            try
            {
                for (int i = 0; i < __instance.Options.Count; i++)
                {
                    string o = __instance.Options[i];
                    if (o != null)
                    {
                        __instance.Options[i] = MessageTranslation.Translate(o);
                    }
                }
                string main = __instance.MainText;
                if (string.IsNullOrEmpty(main))
                {
                    return true;
                }
                string ja = MessageTranslation.Translate(main);
                if (ReferenceEquals(ja, main) || ja == main)
                {
                    return true;
                }
                __result = BookWrap.Wrap(ja, Columns);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 端末の翻訳エラー: " + ex);
                return true;
            }
        }
    }

    // グリット・ゲートの端末の下の行（Available power: N amps / 電力の警告）
    [HarmonyPatch(typeof(GritGateTerminalScreen), nameof(GritGateTerminalScreen.BeforeRender))]
    public static class GritGateFooterPatch
    {
        public static void Postfix(ref string footerText)
        {
            try
            {
                footerText = MessageTranslation.Translate(footerText);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 端末の下の行の翻訳エラー: " + ex);
            }
        }
    }
}
