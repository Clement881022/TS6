namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 帳號等級曲線與主線章節等級門檻（建議值，見 docs/days-1-7.md）。
    /// 主線靠等級門檻踩煞車：快的玩家被擋住，轉去資源副本與養成。
    /// </summary>
    public static class PlayerLevelCurve
    {
        public const int MaxLevel = 60;

        /// <summary>升到下一級所需經驗；已滿級回傳 0。</summary>
        public static int ExpToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return 60 + 40 * (level - 1);
        }

        /// <summary>章節（1 起算）需要的帳號等級；超出表則往後每章 +4。</summary>
        public static int RequiredLevelForChapter(int chapter)
        {
            int[] table = { 1, 5, 9, 13, 17, 21 };
            if (chapter < 1) return 1;
            if (chapter <= table.Length) return table[chapter - 1];
            return table[table.Length - 1] + 4 * (chapter - table.Length);
        }
    }
}
