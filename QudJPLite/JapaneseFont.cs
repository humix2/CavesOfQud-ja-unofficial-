using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using XRL;

namespace QudJPLite
{
    // Windows に入っている日本語フォントを TextMeshPro のフォールバックに登録するだけの最小構成。
    // ゲーム処理への割り込み（Harmony）は一切行わない。
    [HasModSensitiveStaticCache]
    public static class JapaneseFont
    {
        // 上から順に、見つかった最初のフォントを使う
        private static readonly string[] Candidates =
        {
            "BIZ-UDGothicR.ttc",
            "YuGothM.ttc",
            "meiryo.ttc",
            "msgothic.ttc",
        };

        private static TMP_FontAsset fontAsset;

        [ModSensitiveCacheInit]
        public static void Init()
        {
            if (fontAsset != null)
            {
                return;
            }

            // フォント生成はテクスチャを作るので Unity のメインスレッドで行う
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.uiQueue != null)
            {
                manager.uiQueue.queueTask(Register);
            }
            else
            {
                Register();
            }
        }

        private static void Register()
        {
            if (fontAsset != null)
            {
                return;
            }
            try
            {
                string path = FindFont();
                if (path == null)
                {
                    Debug.LogError("[QudJPLite] 日本語フォントが見つかりません: " + string.Join(", ", Candidates));
                    return;
                }

                // 2048x2048 の動的アトラス。足りなくなったら自動で追加される
                fontAsset = TMP_FontAsset.CreateFontAsset(path, 0, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048);
                if (fontAsset == null)
                {
                    Debug.LogError("[QudJPLite] フォントアセットの作成に失敗しました: " + path);
                    return;
                }
                fontAsset.name = "QudJPLite-" + Path.GetFileNameWithoutExtension(path);
                RaiseAscent(fontAsset);
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;

                // 全体のフォールバック（あとから読み込まれるフォントにも効く）
                if (TMP_Settings.fallbackFontAssets == null)
                {
                    TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
                }
                TMP_Settings.fallbackFontAssets.Insert(0, fontAsset);

                // 既に読み込まれているフォントにも追加（1回だけ）
                int patched = 0;
                foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (asset == null || asset == fontAsset)
                    {
                        continue;
                    }
                    if (asset.fallbackFontAssetTable == null)
                    {
                        asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    }
                    if (!asset.fallbackFontAssetTable.Contains(fontAsset))
                    {
                        asset.fallbackFontAssetTable.Add(fontAsset);
                        patched++;
                    }
                }

                SubMeshAlphaFix.Install();

                Debug.Log("[QudJPLite] 日本語フォントを登録しました: " + path + " (既存フォント " + patched + " 件に追加)");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] フォント登録エラー: " + ex);
            }
        }

        // TextMeshPro は行の高さを各フォントの「上端（ascentLine）」から決める。
        // 日本語フォントは漢字の上端がこの値より高く、行の上にはみ出して1行目の上が欠ける。
        // 代表的な漢字の実際の上端を測り、ascentLine をそこまで引き上げる（起動時に1回だけ）
        // 文字の上に足す余白と、行間に足す余白（フォントの大きさに対する割合）
        private const float TopPadding = 0.08f;
        private const float LineGap = 0.10f;

        private static void RaiseAscent(TMP_FontAsset asset)
        {
            try
            {
                UnityEngine.TextCore.FaceInfo face = asset.faceInfo;
                float top = face.ascentLine;
                foreach (char c in "漢字国鬱語")
                {
                    if (asset.TryAddCharacters(c.ToString()) && asset.characterLookupTable.TryGetValue(c, out TMP_Character ch) && ch.glyph != null)
                    {
                        top = Math.Max(top, ch.glyph.metrics.horizontalBearingY);
                    }
                }
                // 文字の上端が ascentLine ぴったりだと、輪郭のぼかし分が表示欄の上端で切れる。
                // 上に少し余白を足し、行の高さにも余裕を持たせる（行どうしがくっつかないように）
                float before = face.ascentLine;
                float extra = Math.Max(0f, top - before) + face.pointSize * TopPadding;
                face.ascentLine = before + extra;
                face.lineHeight += extra + face.pointSize * LineGap;
                asset.faceInfo = face;
                Debug.Log("[QudJPLite] 日本語フォントの上端: " + before + " → " + face.ascentLine + " (文字の上端 " + top + ", 行の高さ " + face.lineHeight + ")");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 日本語フォントの上端の調整エラー: " + ex);
            }
        }

        private static string FindFont()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            foreach (string name in Candidates)
            {
                foreach (string d in new[] { dir, localDir })
                {
                    string p = Path.Combine(d, name);
                    if (File.Exists(p))
                    {
                        return p;
                    }
                }
            }
            return null;
        }
    }
}
