using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;
using Debug = UnityEngine.Debug;

namespace QudJPLite
{
    // ランダムに作られる本（MarkovBook）・落書き・刺青・墓碑の文は、LibraryCorpus.json の英文を
    // マルコフ連鎖でつないで作られる。訳しようがないので、元にする文例を QudJP の日本語コーパス
    // （分かち書き済み・文末は「 . 」）に差し替え、できた文から分かち書きの空白を取り除く
    internal static class MarkovJapanese
    {
        public const string CorpusName = "LibraryCorpus.json";

        private const string JaFile = "LibraryCorpus.ja.json";

        // 日本語コーパスから作った連鎖（ゲームごとに作り直される）
        public static MarkovChainData Ja;

        private static string[] jaKeys;

        [Serializable]
        private class CorpusFile
        {
            public int order;
            public List<string> sentences;
        }

        public static bool IsJa(MarkovChainData data)
        {
            return data != null && ReferenceEquals(data, Ja);
        }

        // 日本語の連鎖を作る。ファイルが無ければ null
        public static MarkovChainData Build()
        {
            string dir = UITranslation.ModDirectory();
            string path = dir == null ? null : Path.Combine(dir, "Localization", JaFile);
            if (path == null || !File.Exists(path))
            {
                return null;
            }
            CorpusFile file = UnityEngine.JsonUtility.FromJson<CorpusFile>(File.ReadAllText(path, Encoding.UTF8));
            if (file?.sentences == null || file.sentences.Count == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            foreach (string s in file.sentences)
            {
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }
                // 名前の区切りの「=」は、ゲームが =name= のような変数と見て本のページから外してしまうので全角にする
                string t = s.Replace(" = ", " ＝ ").Trim();
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(t);
                if (!t.EndsWith(".", StringComparison.Ordinal))
                {
                    sb.Append(" .");
                }
            }
            MarkovChainData data = MarkovChain.BuildChain(sb.ToString(), file.order > 0 ? file.order : 2);
            Ja = data;
            jaKeys = null;
            return data;
        }

        public static string[] Keys()
        {
            if (jaKeys == null && Ja != null)
            {
                jaKeys = new string[Ja.Chain.Count];
                Ja.Chain.Keys.CopyTo(jaKeys, 0);
            }
            return jaKeys;
        }

        // 分かち書きの空白を取り除く。英数字の語どうしの間の空白だけ残す。2回かけても同じ結果になる
        public static string Detokenize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            string[] parts = s.Split(' ');
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (string p in parts)
            {
                if (p.Length == 0)
                {
                    continue;
                }
                string t = p == "." ? "。" : p == "," ? "、" : p;
                if (sb.Length > 0 && IsAsciiWord(sb[sb.Length - 1]) && IsAsciiWord(t[0]))
                {
                    sb.Append(' ');
                }
                sb.Append(t);
            }
            return sb.ToString();
        }

        private static bool IsAsciiWord(char c)
        {
            return c < 0x80 && char.IsLetterOrDigit(c);
        }

        // 題名の頭や終わりに来ると不自然な語
        private static readonly HashSet<string> Bad = new HashSet<string>(StringComparer.Ordinal)
        {
            "の", "を", "が", "に", "は", "と", "で", "へ", "も", "や", "か", "て", "た", "だ", "し", "ば", "れ", "る",
            "から", "まで", "より", "など", "なく", "ず", "な", "ね", "よ", "です", "ます", "ませ", "ん", "う", "せ", "さ",
            "、", "。", ".", ",", "・", "―", "「", "」", "『", "』", "（", "）", "(", ")", "…", "!", "?", "！", "？", "＝", "\"",
        };

        // 日本語の題名。英語用の処理（冠詞を外す・大文字化）は使わない
        public static string Title(MarkovChainData data)
        {
            string[] keys = Keys();
            for (int attempt = 0; attempt < 200; attempt++)
            {
                string seed = keys[Stat.Random(0, keys.Length - 1)];
                if (seed.Contains("."))
                {
                    continue;
                }
                List<string> words = new List<string>(seed.Split(' '));
                int length = Stat.Random(1, 5);
                for (int i = 0; i < length; i++)
                {
                    string key = words[words.Count - 2] + " " + words[words.Count - 1];
                    List<string> next;
                    if (!data.Chain.TryGetValue(key, out next) || next.Count == 0)
                    {
                        break;
                    }
                    string w = next[Stat.Random(0, next.Count - 1)];
                    if (w.Contains("."))
                    {
                        break;
                    }
                    words.Add(w);
                }
                while (words.Count > 0 && Bad.Contains(words[0]))
                {
                    words.RemoveAt(0);
                }
                while (words.Count > 0 && Bad.Contains(words[words.Count - 1]))
                {
                    words.RemoveAt(words.Count - 1);
                }
                string title = Detokenize(string.Join(" ", words));
                if (title.Length >= 2 && title.IndexOf('「') < 0 && title.IndexOf('」') < 0)
                {
                    return title;
                }
            }
            return "無題";
        }

        // 「, Vol. IX」「: Unabridged」「, 3rd Edition」を日本語にする
        public static string TitleSuffix(string title)
        {
            int i = title.LastIndexOf(", Vol. ", StringComparison.Ordinal);
            if (i >= 0)
            {
                return title.Substring(0, i) + " 第" + title.Substring(i + 7) + "巻";
            }
            if (title.EndsWith(": Unabridged", StringComparison.Ordinal))
            {
                return title.Substring(0, title.Length - 12) + "（完全版）";
            }
            i = title.LastIndexOf(", ", StringComparison.Ordinal);
            if (i >= 0 && title.EndsWith(" Edition", StringComparison.Ordinal))
            {
                string n = title.Substring(i + 2, title.Length - i - 2 - 8);
                int k = 0;
                while (k < n.Length && char.IsDigit(n[k]))
                {
                    k++;
                }
                if (k > 0)
                {
                    return title.Substring(0, i) + " 第" + n.Substring(0, k) + "版";
                }
            }
            return title;
        }
    }

    // 本のコーパスを読み込むところで、日本語のコーパスを代わりに読み込む
    [HarmonyPatch(typeof(MarkovBook), nameof(MarkovBook.EnsureCorpusLoaded))]
    public static class MarkovCorpusLoadPatch
    {
        public static bool Prefix(string Corpus)
        {
            if (Corpus != MarkovJapanese.CorpusName)
            {
                return true;
            }
            try
            {
                MarkovChainData loaded;
                if (MarkovBook.CorpusData.TryGetValue(Corpus, out loaded))
                {
                    return false;
                }
                MarkovChainData data = MarkovJapanese.Build();
                if (data == null)
                {
                    return true;
                }
                MarkovBook.CorpusData[Corpus] = data;
                MarkovBook.PostprocessLoadedCorpus(data);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 日本語コーパスの読み込みエラー: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(MarkovChain), nameof(MarkovChain.GenerateSentence))]
    public static class MarkovSentencePatch
    {
        public static void Postfix(MarkovChainData Data, ref string __result)
        {
            if (MarkovJapanese.IsJa(Data))
            {
                __result = MarkovJapanese.Detokenize(__result);
            }
        }
    }

    [HarmonyPatch(typeof(MarkovChain), nameof(MarkovChain.GenerateFragment))]
    public static class MarkovFragmentPatch
    {
        public static void Postfix(MarkovChainData Data, ref string __result)
        {
            if (MarkovJapanese.IsJa(Data))
            {
                __result = MarkovJapanese.Detokenize(__result);
            }
        }
    }

    [HarmonyPatch(typeof(MarkovChain), nameof(MarkovChain.GenerateTitle))]
    public static class MarkovTitlePatch
    {
        public static bool Prefix(MarkovChainData Data, ref string __result)
        {
            if (!MarkovJapanese.IsJa(Data))
            {
                return true;
            }
            __result = MarkovJapanese.Title(Data);
            return false;
        }
    }

    // 短い文（墓碑・碑文）。元は語数で長さを測るが、空白を取った日本語では測れないので文字数で測る
    [HarmonyPatch(typeof(MarkovChain), nameof(MarkovChain.GenerateShortSentence))]
    public static class MarkovShortSentencePatch
    {
        public static bool Prefix(MarkovChainData Data, string seed, int maxWords, ref string __result)
        {
            if (!MarkovJapanese.IsJa(Data))
            {
                return true;
            }
            int max = Math.Max(maxWords, 1) * 3;
            string text = null;
            for (int n = 0; n < 50; n++)
            {
                text = MarkovChain.GenerateSentence(Data, seed);
                if (text.Length <= max)
                {
                    break;
                }
            }
            __result = text;
            return false;
        }
    }

    // 英単語で始まる鍵を探すところ。日本語のコーパスには無いので、元のままだと永遠に探し続ける
    [HarmonyPatch(typeof(MarkovChain), nameof(MarkovChain.GenerateSeedFromWord))]
    public static class MarkovSeedFromWordPatch
    {
        public static bool Prefix(MarkovChainData Data, ref string __result)
        {
            if (!MarkovJapanese.IsJa(Data))
            {
                return true;
            }
            string[] keys = MarkovJapanese.Keys();
            for (int n = 0; n < 100; n++)
            {
                string k = keys[Stat.Random(0, keys.Length - 1)];
                if (!k.Contains("."))
                {
                    __result = k;
                    return false;
                }
            }
            __result = keys[0];
            return false;
        }
    }

    // 題名の「第N巻」などを日本語にする
    [HarmonyPatch(typeof(MarkovBook), nameof(MarkovBook.SetContents))]
    public static class MarkovBookSetContentsPatch
    {
        public static void Postfix(MarkovBook __instance)
        {
            try
            {
                if (__instance.BookCorpus != MarkovJapanese.CorpusName || MarkovJapanese.Ja == null || string.IsNullOrEmpty(__instance.Title))
                {
                    return;
                }
                __instance.Title = MarkovJapanese.TitleSuffix(__instance.Title);
                if (__instance.ParentObject?.Render != null)
                {
                    __instance.ParentObject.Render.DisplayName = __instance.Title;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 本の題名の翻訳エラー: " + ex);
            }
        }
    }

    // 日本語化する前に作られた本は題名が英語のまま保存されているので、読むときに同じ種から日本語で作り直す
    [HarmonyPatch(typeof(MarkovBook), nameof(MarkovBook.HandleEvent), new Type[] { typeof(InventoryActionEvent) })]
    public static class MarkovBookReadPatch
    {
        public static void Prefix(MarkovBook __instance, InventoryActionEvent E)
        {
            try
            {
                if (E?.Command != "Read" || __instance.BookCorpus != MarkovJapanese.CorpusName || JapaneseWrap.HasJapanese(__instance.Title ?? ""))
                {
                    return;
                }
                MarkovBook.EnsureCorpusLoaded(__instance.BookCorpus);
                if (MarkovJapanese.Ja == null)
                {
                    return;
                }
                __instance.SetContents(__instance.BookSeed, __instance.BookCorpus);
                __instance.Pages = null;
                __instance.Sentence = null;
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 本の題名の作り直しエラー: " + ex);
            }
        }
    }
}
