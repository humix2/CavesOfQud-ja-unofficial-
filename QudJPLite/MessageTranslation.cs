using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.Messages;

namespace QudJPLite
{
    // メッセージログの日本語化。
    // メッセージがログに追加される瞬間（MessageQueue.AddPlayerMessage）に1回だけ、正規表現のパターンで訳す。
    // 表示のたびには何もしないので軽い。パターンは QudJP (MIT) の messages.ja.json と自作分（ui-messages.tsv）。
    [HasModSensitiveStaticCache]
    public static class MessageTranslation
    {
        private struct Rule
        {
            public Regex Pattern;
            public string Template;
        }

        private static List<Rule> rules;

        [ModSensitiveCacheInit]
        public static void Load()
        {
            rules = null;
            try
            {
                string dir = UITranslation.ModDirectory();
                string path = dir == null ? null : Path.Combine(dir, "Localization", "ui-messages.tsv");
                if (path == null || !File.Exists(path))
                {
                    return;
                }
                List<Rule> list = new List<Rule>();
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }
                    int tab = line.IndexOf('\t');
                    if (tab <= 0)
                    {
                        continue;
                    }
                    try
                    {
                        list.Add(new Rule
                        {
                            Pattern = new Regex(line.Substring(0, tab), RegexOptions.CultureInvariant),
                            Template = Unescape(line.Substring(tab + 1))
                        });
                    }
                    catch (Exception)
                    {
                        // 壊れたパターンは読み飛ばす
                    }
                }
                rules = list;
                Debug.Log("[QudJPLite] メッセージのパターンを読み込みました: " + list.Count + " 件");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] メッセージのパターンの読み込みエラー: " + ex);
            }
        }

        private static string Unescape(string s)
        {
            return s.IndexOf('\\') < 0 ? s : s.Replace("\\n", "\n").Replace("\\t", "\t");
        }

        // メッセージの訳: UI 辞書・型・パターンを順に試す（UITranslation.Translate が全部やる）
        public static string Translate(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length > 2000)
            {
                return message;
            }
            return UITranslation.Translate(message);
        }

        private static int depth;

        // 訳文の {0} {t0} {d0} などを、捕捉した部分（辞書にあれば訳したもの）に置き換える
        private static readonly Regex Slot = new Regex(@"\{[a-z]*(\d+)\}", RegexOptions.CultureInvariant);

        // 先頭の {{X| に対応する }} が文末のものか（中に別の色指定が入れ子になっていてもよい）
        private static bool ClosesAtEnd(string line, int from)
        {
            int depth = 1;
            for (int i = from; i < line.Length - 1; i++)
            {
                if (line[i] == '{' && line[i + 1] == '{')
                {
                    depth++;
                    i++;
                }
                else if (line[i] == '}' && line[i + 1] == '}')
                {
                    depth--;
                    i++;
                    if (depth == 0)
                    {
                        return i == line.Length - 1;
                    }
                }
            }
            return false;
        }

        // 名前の前に付く状態の形容詞（ゲームが状態異常に応じて付ける）
        private static readonly Dictionary<string, string> Adjectives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "wet", "濡れた" }, { "flaming", "燃えている" }, { "frozen", "凍った" }, { "burning", "燃えている" },
            { "bloody", "血まみれの" }, { "slimy", "ぬめった" }, { "oily", "油まみれの" }, { "tarred", "タールまみれの" }, { "tarry", "タールまみれの" },
            { "salty", "塩まみれの" }, { "muddy", "泥まみれの" }, { "sticky", "べたついた" }, { "dazed", "朦朧とした" },
            { "stunned", "気絶した" }, { "confused", "混乱した" }, { "prone", "倒れた" },
        };

        private static readonly Regex AdjectiveHead = new Regex(@"^(?:\{\{[\w ]+\|)?([A-Za-z]+)(?:\}\})? ", RegexOptions.CultureInvariant);

        // 捕捉した名前を訳す。先頭の状態の形容詞（"wet 巨大ムカデ" など）は外して訳し、日本語で前に付け直す
        // 液体の名前。ゲームは「主な液体の名前」の前に「混ざっている液体の形容詞」を並べる（例: salty water）
        private static readonly Dictionary<string, string> LiquidNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "water", "水" }, { "fresh water", "真水" }, { "salt", "塩" }, { "acid", "酸" }, { "algae", "藻" },
            { "asphalt", "アスファルト" }, { "blood", "血" }, { "brain brine", "脳髄汁" }, { "cider", "サイダー" },
            { "cloning draught", "クローン薬液" }, { "convalessence", "コンバレセンス" }, { "gel", "ゲル" },
            { "green goo", "緑の粘液" }, { "honey", "ハチミツ" }, { "ink", "インク" }, { "lava", "溶岩" },
            { "neutron flux", "中性子フラックス" }, { "oil", "油" }, { "black ooze", "黒い粘液" },
            { "primordial soup", "原始スープ" }, { "putrescence", "腐敗液" }, { "sap", "樹液" }, { "slime", "スライム" },
            { "brown sludge", "茶色いヘドロ" }, { "sunslag", "サンスラグ" }, { "warm static", "ウォームスタティック" },
            { "molten wax", "溶けた蝋" }, { "wine", "ワイン" },
        };

        private static readonly Dictionary<string, string> LiquidAdjectives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "acidic", "酸" }, { "algal", "藻" }, { "tarry", "タール" }, { "bloody", "血" }, { "nervous", "脳髄汁" },
            { "spiced", "サイダー" }, { "homogenized", "クローン薬液" }, { "luminous", "コンバレセンス" }, { "unctuous", "ゲル" },
            { "gooey", "粘液" }, { "honeyed", "ハチミツ" }, { "inky", "インク" }, { "magmatic", "溶岩" },
            { "neutronic", "中性子" }, { "oily", "油" }, { "oozing", "黒い粘液" }, { "soupy", "原始スープ" },
            { "putrid", "腐敗液" }, { "salty", "塩" }, { "brackish", "塩" }, { "sugary", "樹液" }, { "slimy", "スライム" },
            { "sludgy", "ヘドロ" }, { "radiant", "サンスラグ" }, { "entropic", "ウォームスタティック" }, { "waxen", "蝋" },
            { "lush", "ワイン" }, { "dilute", "水" },
        };

        private static readonly Regex ColorMarkup = new Regex(@"\{\{[^|{}]+\|([^{}]*)\}\}", RegexOptions.CultureInvariant);

        // "salty water" → "塩まじりの水"。知らない語があれば null
        private static string TranslateLiquid(string phrase)
        {
            string plain = ColorMarkup.Replace(phrase, "$1").Trim();
            string[] words = plain.Split(' ');
            int nameStart = -1;
            string name = null;
            for (int n = Math.Min(2, words.Length); n >= 1 && name == null; n--)
            {
                string tail = string.Join(" ", words, words.Length - n, n);
                if (LiquidNames.TryGetValue(tail, out name))
                {
                    nameStart = words.Length - n;
                }
            }
            if (name == null)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < nameStart; i++)
            {
                string adj;
                if (!LiquidAdjectives.TryGetValue(words[i], out adj))
                {
                    return null;
                }
                sb.Append(adj).Append(i + 1 < nameStart ? "と" : "まじりの");
            }
            return sb.Append(name).ToString();
        }

        // 「水たまり of salty water」→「水たまり（塩まじりの水）」
        private static readonly Regex PoolOf = new Regex(@"^(.*[^\x00-\x7F].*?) of (.+)$", RegexOptions.CultureInvariant);

        private static string TranslatePool(string v)
        {
            Match m = PoolOf.Match(v);
            if (!m.Success)
            {
                return null;
            }
            string liquid = TranslateLiquid(m.Groups[2].Value);
            return liquid == null ? null : m.Groups[1].Value + "（" + liquid + "）";
        }

        private static readonly Regex ListSeparator = new Regex(@",\s*(?:and\s+)?|\s+and\s+", RegexOptions.CultureInvariant);

        private static readonly Regex Article = new Regex(@"^(?:[Aa]n?|[Tt]he|[Ss]ome) ", RegexOptions.CultureInvariant);

        // 「A X, a Y, and a Z」のような名前の一覧を「X、Y、Z」にする。どの項目にも日本語が入っているときだけ
        private static string TranslateList(string v)
        {
            if (v.IndexOf(',') < 0 && v.IndexOf(" and ", StringComparison.Ordinal) < 0)
            {
                return null;
            }
            string[] items = ListSeparator.Split(v);
            if (items.Length < 2)
            {
                return null;
            }
            for (int i = 0; i < items.Length; i++)
            {
                string item = Article.Replace(items[i].Trim(), "");
                if (item.Length == 0 || !JapaneseWrap.HasJapanese(item))
                {
                    return null;
                }
                items[i] = TranslateName(item);
            }
            return string.Join("、", items);
        }

        private static string TranslateName(string v)
        {
            string t = UITranslation.Translate(v);
            if (!ReferenceEquals(t, v))
            {
                return t;
            }
            string list = TranslateList(v);
            if (list != null)
            {
                return list;
            }
            // 「The サルヴ インジェクター」のように、日本語の名前の前に残った冠詞は外す
            Match art = Article.Match(v);
            if (art.Success && art.Length < v.Length && JapaneseWrap.HasJapanese(v))
            {
                return TranslateName(v.Substring(art.Length));
            }
            string pool = TranslatePool(v);
            if (pool != null)
            {
                return pool;
            }
            string liquid = TranslateLiquid(v);
            if (liquid != null)
            {
                return liquid;
            }
            StringBuilder prefix = null;
            string rest = v;
            for (int n = 0; n < 4; n++)
            {
                Match m = AdjectiveHead.Match(rest);
                string ja;
                if (!m.Success || m.Length >= rest.Length || !Adjectives.TryGetValue(m.Groups[1].Value, out ja))
                {
                    break;
                }
                (prefix ?? (prefix = new StringBuilder())).Append(ja);
                rest = rest.Substring(m.Length);
            }
            return prefix == null ? v : prefix.Append(UITranslation.Translate(rest)).ToString();
        }

        // 1行の文をパターンで訳す。当たらなければ null。
        // 色指定（先頭の &X ^X や、全体を囲む {{X|...}}）は外して照合し、訳文に付け直す
        public static string MatchPatterns(string line)
        {
            List<Rule> list = rules;
            if (list == null || line.Length < 4 || depth > 2)
            {
                return null;
            }
            int start = 0;
            while (start + 1 < line.Length && (line[start] == '&' || line[start] == '^') && line[start + 1] != line[start])
            {
                start += 2;
            }
            if (start > 0)
            {
                string inner = MatchPatterns(line.Substring(start));
                return inner == null ? null : line.Substring(0, start) + inner;
            }
            if (line.StartsWith("{{") && line.EndsWith("}}"))
            {
                int bar = line.IndexOf('|');
                if (bar > 2 && bar < 12 && ClosesAtEnd(line, bar + 1))
                {
                    string inner = MatchPatterns(line.Substring(bar + 1, line.Length - bar - 3));
                    return inner == null ? null : line.Substring(0, bar + 1) + inner + "}}";
                }
            }
            depth++;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Match m = list[i].Pattern.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    string[] values = new string[m.Groups.Count - 1];
                    for (int g = 1; g < m.Groups.Count; g++)
                    {
                        // 名前などの部分も、辞書にあれば訳す
                        string v = m.Groups[g].Value;
                        if (v == "You" || v == "you")
                        {
                            values[g - 1] = "あなた";
                            continue;
                        }
                        values[g - 1] = v.Length < line.Length ? TranslateName(v) : v;
                    }
                    return Slot.Replace(list[i].Template, s =>
                    {
                        int n = int.Parse(s.Groups[1].Value);
                        return n < values.Length ? values[n] : s.Value;
                    });
                }
                return null;
            }
            finally
            {
                depth--;
            }
        }
    }

    // ポップアップの本文。表示側で折り返し（改行）が入る前に、行ごとに訳す
    [HarmonyPatch(typeof(XRL.UI.Popup), nameof(XRL.UI.Popup.WaitNewPopupMessage))]
    public static class PopupMessagePatch
    {
        public static void Prefix(ref string message)
        {
            try
            {
                message = MessageTranslation.Translate(message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップの翻訳エラー: " + ex);
            }
        }
    }

    // Popup.Show / ShowYesNo / ShowBlock などの入口。これらは色指定 {{X|...}} を &X 形式に変換してから表示側に渡すので、
    // 変換される前（入口）で訳す。引数 Message（文字列）を持つ Popup のメソッドすべてが対象。
    [HarmonyPatch]
    public static class PopupEntryPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo m in typeof(XRL.UI.Popup).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsGenericMethodDefinition || m.Name.StartsWith("Render") || m.Name == nameof(XRL.UI.Popup.WaitNewPopupMessage))
                {
                    continue;
                }
                ParameterInfo[] ps = m.GetParameters();
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].Name == "Message" && ps[i].ParameterType == typeof(string))
                    {
                        yield return m;
                        break;
                    }
                }
            }
        }

        // 対象はどれも引数名が Message（string）なので、名前で受け取って書き換えられる
        public static void Prefix(ref string Message)
        {
            try
            {
                Message = MessageTranslation.Translate(Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップの翻訳エラー: " + ex);
            }
        }
    }

    // ポップアップの幅は本文の幅で決まる。日本語は短く収まるので、「食事をとった。」のような短い文だと
    // 枠の飾りより狭くなって枠が崩れる。開いたときに1回だけ、本文の最小幅を確保する
    [HarmonyPatch(typeof(Qud.UI.PopupMessage), nameof(Qud.UI.PopupMessage.SizeContentToWidth))]
    public static class PopupMinWidthPatch
    {
        private const float MinWidth = 360f;

        public static void Postfix(Qud.UI.PopupMessage __instance)
        {
            try
            {
                UnityEngine.UI.LayoutElement le = __instance.Message?.GetComponent<UnityEngine.UI.LayoutElement>();
                if (le == null || __instance.Message.text == "{{y|}}" || le.minWidth >= MinWidth)
                {
                    return;
                }
                le.minWidth = MinWidth;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.transform as RectTransform);
                __instance.ClampPopupPosition();
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] ポップアップの幅の調整エラー: " + ex);
            }
        }
    }

    // 選択肢のポップアップ（Esc メニュー、たき火のメニューなど）。見出し・本文・選択肢をまとめて訳す
    [HarmonyPatch]
    public static class PopupOptionsPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type t = typeof(XRL.UI.Popup);
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickOption));
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickOptionAsync));
            yield return AccessTools.Method(t, nameof(XRL.UI.Popup.PickSeveral));
        }

        // アイテムの操作メニューは "{{hotkey|d}}rop" のようにキーの色付けが単語の中に入っている。
        // そのままでは辞書に当たらないので、色付けを外して引き、訳があればそれを使う（キーは左の [d] に出る）
        private static readonly Regex Hotkey = new Regex(@"\{\{hotkey\|(.)\}\}", RegexOptions.CultureInvariant);

        private static string TranslateOption(string s)
        {
            string t = MessageTranslation.Translate(s);
            if (!ReferenceEquals(t, s) || s == null || s.IndexOf("{{hotkey|", StringComparison.Ordinal) < 0)
            {
                return t;
            }
            string plain = Hotkey.Replace(s, "$1");
            string ja = MessageTranslation.Translate(plain);
            if (ja != plain)
            {
                return ja;
            }
            // キーが大文字のとき（"mark {{hotkey|I}}mportant"）は小文字に戻して引く
            string lower = Hotkey.Replace(s, m => m.Groups[1].Value.ToLowerInvariant());
            ja = MessageTranslation.Translate(lower);
            return ja != lower ? ja : s;
        }

        public static void Prefix(ref string Title, ref string Intro, ref IReadOnlyList<string> Options)
        {
            try
            {
                Title = MessageTranslation.Translate(Title);
                Intro = MessageTranslation.Translate(Intro);
                if (Options != null && Options.Count > 0)
                {
                    string[] translated = new string[Options.Count];
                    bool changed = false;
                    for (int i = 0; i < translated.Length; i++)
                    {
                        translated[i] = TranslateOption(Options[i]);
                        changed |= !ReferenceEquals(translated[i], Options[i]);
                    }
                    if (changed)
                    {
                        Options = translated;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 選択肢の翻訳エラー: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(MessageQueue), nameof(MessageQueue.AddPlayerMessage), new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class AddPlayerMessagePatch
    {
        public static void Prefix(ref string Message)
        {
            try
            {
                Message = MessageTranslation.Translate(Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] メッセージの翻訳エラー: " + ex);
            }
        }
    }
}
