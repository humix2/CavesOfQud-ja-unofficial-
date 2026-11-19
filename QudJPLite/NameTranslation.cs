using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.World;

namespace QudJPLite
{
    // アイテム・クリーチャー・液体の名前の組み立て（DescriptionBuilder.ToString）を日本語の語順にする。
    // ゲームは「形容詞 本体 節 タグ」を英語の語順で空白区切りにつなぐ。本体は XML で日本語になっているが、
    // 形容詞（small / sturdy）や液体の節（of salty asphalt）は英語のまま残るので、部品ごとに訳して並べ直す。
    //   形容詞 → 本体の前（名詞形の訳には「の」を付ける）
    //   液体の節「of X」→「Xの」を本体の直前に
    //   「with X and Y」→「X・Y付き」を本体の前に
    //   タグ「[32 drams of fresh water]」→「[真水 32ドラム]」
    // 名前は表示のたびに組み立て直されるので、部品の訳は覚えておき、2回目以降は辞書を1回引くだけにする。
    [HasModSensitiveStaticCache]
    public static class NameTranslation
    {
        private const string WordFile = "name-words.tsv";

        // 名前の中で使う訳（UI 辞書より優先。形容詞を名詞にかかる形にしたいときなど）
        private static Dictionary<string, string> words = new Dictionary<string, string>(StringComparer.Ordinal);

        // 部品 → 訳（訳せなかったものは Miss を入れておく）
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        private const string Miss = "\u0000";

        private static bool errorLogged;

        private static readonly Regex Amount = new Regex(@"^(\{\{rules\|\d+\}\}|\d+) drams? of (.+)$", RegexOptions.Compiled);

        private static readonly Regex Stack = new Regex(@"^x(\d+)$", RegexOptions.Compiled);

        [ModSensitiveCacheInit]
        public static void Load()
        {
            lock (cache)
            {
                cache.Clear();
                Dictionary<string, string> w = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    string dir = UITranslation.ModDirectory();
                    string path = dir == null ? null : Path.Combine(dir, "Localization", WordFile);
                    if (path != null && File.Exists(path))
                    {
                        foreach (string line in File.ReadAllLines(path))
                        {
                            int tab = line.IndexOf('\t');
                            if (tab > 0 && !line.StartsWith("#"))
                            {
                                w[line.Substring(0, tab)] = line.Substring(tab + 1);
                            }
                        }
                        Debug.Log("[QudJPLite] 名前の訳を読み込みました: " + w.Count + " 件");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[QudJPLite] 名前の訳の読み込みエラー: " + ex);
                }
                words = w;
            }
        }

        // DescriptionBuilder の中身を日本語の語順で組み立てる。英語のままでよい（訳す部品が無い）ときは null
        public static string Build(DescriptionBuilder db)
        {
            db.Resolve();
            int count = db.Count;
            if (count == 0)
            {
                return null;
            }
            if (count == 1)
            {
                string only = db.LastAdded;
                if (!HasLatin(only))
                {
                    return null;
                }
                string ja = Piece(only);
                if (ja == null)
                {
                    return null;
                }
                return string.IsNullOrEmpty(db.Color) ? ja : "{{" + db.Color + "|" + ja + "}}";
            }

            // 英語の部品が無ければ（本体も形容詞も日本語）ゲームの処理に任せる
            bool any = false;
            foreach (string k in db.Keys)
            {
                if (HasLatin(k))
                {
                    any = true;
                    break;
                }
            }
            if (!any)
            {
                return null;
            }
            List<string> keys = new List<string>(db.Keys);
            keys.Sort(db.descOrderComparison);

            StringBuilder front = new StringBuilder();   // 形容詞・with 句
            string liquid = null;                        // 「Xの」
            StringBuilder body = new StringBuilder();    // 本体
            StringBuilder inner = new StringBuilder();   // 色の内側に残す後ろの部品（称号など）
            StringBuilder tail = new StringBuilder();    // 色の外のタグなど
            List<string> with = null;

            foreach (string k in keys)
            {
                int order = db[k];
                if (order < 10)
                {
                    Join(front, Adjective(k));
                }
                else if (order < 300)
                {
                    Join(body, HasLatin(k) ? (Piece(k) ?? k) : k);
                }
                else if (k.StartsWith("of ", StringComparison.Ordinal))
                {
                    string l = Liquid(k.Substring(3), out string amount);
                    liquid = l == null ? null : (amount != null ? amount + "ドラムの" : "") + l + (Visible(l).EndsWith("の", StringComparison.Ordinal) ? "" : "の");
                    if (l == null)
                    {
                        Append(order > 600 ? tail : inner, k);
                    }
                }
                else if (k.StartsWith("with ", StringComparison.Ordinal))
                {
                    with = new List<string>();
                    foreach (string item in SplitList(k.Substring(5)))
                    {
                        string ja = Piece(item) ?? item;
                        with.Add(Visible(ja).EndsWith("付き", StringComparison.Ordinal) ? ja : ja + "付き");
                    }
                }
                else
                {
                    Match m = Stack.Match(k);
                    if (m.Success)
                    {
                        // 「×」(U+00D7) はゲームのフォントで CP437 の 0xD7「╫」として描かれるので、英語版と同じ x を使う
                        tail.Append(" x").Append(m.Groups[1].Value);
                        continue;
                    }
                    string ja;
                    if (k.StartsWith(", ", StringComparison.Ordinal))
                    {
                        ja = "、" + Titles(k.Substring(2));
                        (order > 600 ? tail : inner).Append(ja);
                        continue;
                    }
                    if (k.StartsWith("and ", StringComparison.Ordinal))
                    {
                        ja = "・" + (Piece(k.Substring(4)) ?? k.Substring(4));
                        (order > 600 ? tail : inner).Append(ja);
                        continue;
                    }
                    ja = HasLatin(k) ? (Tag(k) ?? Piece(k) ?? k) : k;
                    Append(order > 600 ? tail : inner, ja);
                }
            }
            if (with != null)
            {
                Join(front, string.Join("・", with.ToArray()));
            }

            StringBuilder sb = new StringBuilder();
            bool color = !string.IsNullOrEmpty(db.Color);
            if (color)
            {
                sb.Append("{{").Append(db.Color).Append('|');
            }
            Join(sb, front.ToString());
            Join(sb, liquid);
            Join(sb, body.ToString());
            sb.Append(inner);
            if (color)
            {
                sb.Append("}}");
            }
            sb.Append(tail);
            return sb.ToString();
        }

        // 形容詞を名詞にかかる形にする。訳せなければ英語のまま
        private static string Adjective(string s)
        {
            if (!HasLatin(s))
            {
                return s;
            }
            string ja = Piece(s);
            if (ja == null)
            {
                return s;
            }
            string v = Visible(ja);
            if (v.Length == 0)
            {
                return ja;
            }
            char last = v[v.Length - 1];
            // 「小さな」「頑丈な」「燃え盛る」などはそのまま。「傑作」「帯電」など名詞で終わる訳には「の」を付ける
            if (IsHiragana(last) || !IsJapanese(last))
            {
                return ja;
            }
            return ja + (last == '的' ? "な" : "の");
        }

        // 液体の名前（"{{Y|salty}} {{K|asphalt}}"、"{{rules|32}} drams of fresh water" など）。訳せなければ null
        private static string Liquid(string s, out string amount)
        {
            amount = null;
            string sealedJa = null;
            foreach (string seal in new[] { ", {{c|sealed}}", ", sealed", "{{c|sealed}}", "sealed" })
            {
                if (s.EndsWith(seal, StringComparison.Ordinal))
                {
                    s = s.Substring(0, s.Length - seal.Length);
                    sealedJa = seal.Contains("{{") ? "{{c|密封}}" : "密封";
                    break;
                }
            }
            Match m = Amount.Match(s);
            if (m.Success)
            {
                amount = m.Groups[1].Value;
                s = m.Groups[2].Value;
            }
            string ja = s.Length == 0 ? "" : Piece(s);
            if (ja == null)
            {
                return null;
            }
            if (sealedJa != null)
            {
                ja = ja.Length == 0 ? sealedJa : ja + "、" + sealedJa;
            }
            return ja;
        }

        // 角かっこのタグ。液体の量の入ったもの（[32 drams of fresh water]）を日本語の語順にする。それ以外は null
        private static string Tag(string s)
        {
            string open, close;
            if (s.StartsWith("{{y|[", StringComparison.Ordinal) && s.EndsWith("]}}", StringComparison.Ordinal))
            {
                open = "{{y|[";
                close = "]}}";
            }
            else if (s.StartsWith("[", StringComparison.Ordinal) && s.EndsWith("]", StringComparison.Ordinal))
            {
                open = "[";
                close = "]";
            }
            else
            {
                return null;
            }
            string body = s.Substring(open.Length, s.Length - open.Length - close.Length);
            if (body.IndexOf(" dram", StringComparison.Ordinal) < 0 && body.IndexOf("sealed", StringComparison.Ordinal) < 0)
            {
                return null;
            }
            string ja = Liquid(body, out string amount);
            if (ja == null)
            {
                return null;
            }
            if (amount != null)
            {
                int comma = ja.IndexOf('、');
                string name = TrimNo(comma < 0 ? ja : ja.Substring(0, comma));
                string rest = comma < 0 ? "" : ja.Substring(comma);
                ja = name + " " + amount + "ドラム" + rest;
            }
            return open + ja + close;
        }

        // 液体名の訳が「酸の」のように「の」で終わっていれば外す（タグ「[酸 32ドラム]」用）
        private static string TrimNo(string s)
        {
            int end = s.Length;
            while (end >= 2 && s[end - 1] == '}' && s[end - 2] == '}')
            {
                end -= 2;
            }
            if (end > 1 && s[end - 1] == 'の')
            {
                return s.Substring(0, end - 1) + s.Substring(end);
            }
            return s;
        }

        // 部品1つを訳す。名前用の表 → UI 辞書 → 色を外して → 空白で区切った語ごと、の順。訳せなければ null
        public static string Piece(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            lock (cache)
            {
                string r;
                if (cache.TryGetValue(s, out r))
                {
                    return ReferenceEquals(r, Miss) ? null : r;
                }
                try
                {
                    r = Word(s);
                    if (r == null)
                    {
                        List<string> tokens = SplitTop(s);
                        if (tokens.Count > 1)
                        {
                            StringBuilder sb = new StringBuilder();
                            bool all = true;
                            foreach (string t in tokens)
                            {
                                string ja = HasLatin(t) ? Word(t) : t;
                                if (ja == null)
                                {
                                    all = false;
                                    break;
                                }
                                Join(sb, ja);
                            }
                            r = all ? sb.ToString() : null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogOnce(ex);
                    r = null;
                }
                if (cache.Count > 8192)
                {
                    cache.Clear();
                }
                cache[s] = r ?? Miss;
                return r;
            }
        }

        // 1語（または色付きの1かたまり）を訳す。訳せなければ null
        private static string Word(string s)
        {
            string r;
            if (words.TryGetValue(s, out r))
            {
                return r;
            }
            string color = null;
            string core = s;
            if (s.StartsWith("{{", StringComparison.Ordinal) && s.EndsWith("}}", StringComparison.Ordinal))
            {
                int bar = s.IndexOf('|');
                if (bar > 2 && s.IndexOf("{{", 2, StringComparison.Ordinal) < 0)
                {
                    color = s.Substring(2, bar - 2);
                    core = s.Substring(bar + 1, s.Length - bar - 3);
                    if (words.TryGetValue(core, out r))
                    {
                        return "{{" + color + "|" + r + "}}";
                    }
                }
            }
            r = UITranslation.Translate(s);
            if (!ReferenceEquals(r, s) && r != s && !HasLatin(r))
            {
                return r;
            }
            // 液体の付着・染み（"salty {{G|gooey}}"、"{{K|ooze}}-and-{{G|goo}}-stained"）
            return LiquidWords.Adjective(s);
        }

        // 名前の後ろの称号の並び（"A and B" / "A, B and C"）。まとめて訳せなければ1つずつ訳して「、」でつなぐ
        private static string Titles(string s)
        {
            string whole = Piece(s);
            if (whole != null)
            {
                return whole;
            }
            List<string> items = SplitList(s);
            if (items.Count < 2)
            {
                return s;
            }
            for (int i = 0; i < items.Count; i++)
            {
                items[i] = Piece(items[i]) ?? items[i];
            }
            return string.Join("、", items.ToArray());
        }

        // "A, B, and C" / "A and B" を分ける
        private static List<string> SplitList(string s)
        {
            List<string> list = new List<string>();
            foreach (string part in s.Replace(", and ", ", ").Replace(" and ", ", ").Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(part.Trim());
            }
            return list;
        }

        // 空白で区切る。ただし {{ }} の内側の空白では区切らない
        private static List<string> SplitTop(string s)
        {
            List<string> list = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    depth++;
                    i++;
                }
                else if (c == '}' && i + 1 < s.Length && s[i + 1] == '}')
                {
                    depth--;
                    i++;
                }
                else if (c == ' ' && depth <= 0)
                {
                    if (i > start)
                    {
                        list.Add(s.Substring(start, i - start));
                    }
                    start = i + 1;
                }
            }
            if (start < s.Length)
            {
                list.Add(s.Substring(start));
            }
            return list;
        }

        // 日本語どうしは空白なしで、英語が隣り合うときだけ空白を入れてつなぐ
        private static void Join(StringBuilder sb, string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return;
            }
            if (sb.Length > 0)
            {
                string prev = Visible(sb.ToString());
                string next = Visible(part);
                if (prev.Length > 0 && next.Length > 0 && (!IsJapanese(prev[prev.Length - 1]) || !IsJapanese(next[0])))
                {
                    sb.Append(' ');
                }
            }
            sb.Append(part);
        }

        // 本体より後ろの部品は、ゲームと同じく空白でつなぐ（, : - で始まるものは空白なし）
        private static void Append(StringBuilder sb, string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return;
            }
            char c = part[0];
            if (c != ',' && c != ':' && c != '-' && c != '、')
            {
                sb.Append(' ');
            }
            sb.Append(part);
        }

        // 色指定（{{X| と }}、&X ^X）を除いた見た目の文字
        private static string Visible(string s)
        {
            if (s.IndexOf('{') < 0 && s.IndexOf('&') < 0 && s.IndexOf('^') < 0)
            {
                return s;
            }
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    int bar = s.IndexOf('|', i);
                    int close = s.IndexOf("}}", i, StringComparison.Ordinal);
                    if (bar > 0 && (close < 0 || bar < close))
                    {
                        i = bar;
                        continue;
                    }
                }
                if (c == '}' && i + 1 < s.Length && s[i + 1] == '}')
                {
                    i++;
                    continue;
                }
                if ((c == '&' || c == '^') && i + 1 < s.Length)
                {
                    i++;
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // 見た目に英字が2文字以上続く所がある（色の名前は数えない）
        private static bool HasLatin(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            string v = Visible(s);
            int run = 0;
            foreach (char c in v)
            {
                if (c < 128 && char.IsLetter(c))
                {
                    if (++run >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    run = 0;
                }
            }
            return false;
        }

        private static bool IsHiragana(char c)
        {
            return c >= 'ぁ' && c <= 'ゟ';
        }

        private static bool IsJapanese(char c)
        {
            return (c >= '぀' && c <= 'ヿ') || (c >= '㐀' && c <= '鿿') || (c >= '！' && c <= 'ﾟ') || c == '・' || c == 'ー';
        }

        private static void LogOnce(Exception ex)
        {
            if (!errorLogged)
            {
                errorLogged = true;
                Debug.LogError("[QudJPLite] 名前の翻訳エラー: " + ex);
            }
        }

        public static void Fail(Exception ex)
        {
            LogOnce(ex);
        }
    }

    [HarmonyPatch(typeof(DescriptionBuilder), nameof(DescriptionBuilder.ToString))]
    public static class DescriptionBuilderPatch
    {
        public static bool Prefix(DescriptionBuilder __instance, ref string __result)
        {
            try
            {
                string r = NameTranslation.Build(__instance);
                if (r != null)
                {
                    __result = r;
                    return false;
                }
            }
            catch (Exception ex)
            {
                NameTranslation.Fail(ex);
            }
            return true;
        }
    }
}
