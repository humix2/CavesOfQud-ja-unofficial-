using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace QudJPLite
{
    // ツールチップで日本語が透明になる問題の修正。
    //
    // 原因: TextMeshPro はフォールバックフォント（日本語）の文字を別の子オブジェクト（サブメッシュ）で描き、
    // メッシュ生成時に親の透明度をコピーする。ゲームのツールチップは「全部品を透明化 → 文字設定・サイズ計算 →
    // 部品を不透明化」という順で表示するが、不透明に戻す対象リストは最初に作ったもので、後から作られた
    // 日本語用サブメッシュが含まれない。そのため日本語だけ透明のまま残る（文字幅だけ確保される）。
    //
    // 対策: フォールバック文字を含むテキストだけを記録し、描画直前にサブメッシュの透明度を親に合わせる。
    internal static class SubMeshAlphaFix
    {
        private static readonly Dictionary<TextMeshProUGUI, TMP_SubMeshUI[]> Tracked =
            new Dictionary<TextMeshProUGUI, TMP_SubMeshUI[]>();

        private static readonly List<TextMeshProUGUI> Removed = new List<TextMeshProUGUI>();

        private static bool installed;

        public static void Install()
        {
            if (installed)
            {
                return;
            }
            installed = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            // TMP のメッシュ生成より後に登録されるので、生成後・描画前に呼ばれる
            Canvas.willRenderCanvases += Sync;
        }

        private static void OnTextChanged(Object obj)
        {
            TextMeshProUGUI text = obj as TextMeshProUGUI;
            if (text == null)
            {
                return;
            }
            if (text.textInfo != null && text.textInfo.materialCount > 1)
            {
                Tracked[text] = text.GetComponentsInChildren<TMP_SubMeshUI>(true);
            }
            else
            {
                Tracked.Remove(text);
            }
        }

        private static void Sync()
        {
            if (Tracked.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<TextMeshProUGUI, TMP_SubMeshUI[]> pair in Tracked)
            {
                TextMeshProUGUI text = pair.Key;
                if (text == null)
                {
                    Removed.Add(text);
                    continue;
                }
                if (!text.isActiveAndEnabled)
                {
                    continue;
                }
                float alpha = text.canvasRenderer.GetAlpha();
                TMP_SubMeshUI[] subs = pair.Value;
                for (int i = 0; i < subs.Length; i++)
                {
                    TMP_SubMeshUI sub = subs[i];
                    if (sub != null && sub.canvasRenderer.GetAlpha() != alpha)
                    {
                        sub.canvasRenderer.SetAlpha(alpha);
                    }
                }
            }
            if (Removed.Count > 0)
            {
                foreach (TextMeshProUGUI t in Removed)
                {
                    Tracked.Remove(t);
                }
                Removed.Clear();
            }
        }
    }
}
