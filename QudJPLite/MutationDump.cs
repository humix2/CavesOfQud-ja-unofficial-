using System;
using System.IO;
using System.Text;
using UnityEngine;
using XRL;
using XRL.World.Parts.Mutation;

namespace QudJPLite
{
    // 翻訳作業用: 全変異の説明文（GetDescription と ランク1〜10 の GetLevelText）をファイルに書き出す。
    // MOD フォルダに dump-mutations.flag があるときだけ、起動時に1回だけ動く（通常は何もしない）。
    [HasModSensitiveStaticCache]
    public static class MutationDump
    {
        [ModSensitiveCacheInit]
        public static void Dump()
        {
            try
            {
                string dir = UITranslation.ModDirectory();
                if (dir == null || !File.Exists(Path.Combine(dir, "dump-mutations.flag")))
                {
                    return;
                }
                MutationFactory.GetCategories(); // 変異一覧は必要になるまで読み込まれないので、先に読み込む
                StringBuilder sb = new StringBuilder();
                int count = 0;
                foreach (MutationEntry entry in MutationFactory.AllMutationEntries())
                {
                    BaseMutation m;
                    try
                    {
                        m = entry.CreateInstance();
                    }
                    catch (Exception ex)
                    {
                        sb.Append("## ERROR ").Append(entry.Name).Append(": ").Append(ex.Message).Append('\n');
                        continue;
                    }
                    if (m == null)
                    {
                        continue;
                    }
                    count++;
                    sb.Append("## ").Append(entry.Name).Append('\n');
                    sb.Append("@desc\n").Append(Safe(() => m.GetDescription())).Append('\n');
                    int max = Math.Max(1, Math.Min(entry.MaxLevel, 15));
                    for (int lv = 1; lv <= max + 1; lv++)
                    {
                        int level = lv;
                        sb.Append("@level ").Append(level).Append('\n').Append(Safe(() => m.GetLevelText(level))).Append('\n');
                    }
                }
                File.WriteAllText(Path.Combine(dir, "mutation-dump.txt"), sb.ToString(), new UTF8Encoding(false));
                Debug.Log("[QudJPLite] 変異の説明文を書き出しました: " + count + " 件");
            }
            catch (Exception ex)
            {
                Debug.LogError("[QudJPLite] 変異の説明文の書き出しエラー: " + ex);
            }
        }

        private static string Safe(Func<string> f)
        {
            try
            {
                return f() ?? "";
            }
            catch (Exception ex)
            {
                return "!ERROR " + ex.GetType().Name;
            }
        }
    }
}
