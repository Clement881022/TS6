namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 每日重置的「日」換算。主攻中國大陸：以北京時間（UTC+8）每天 5 點為界（建議值）。
    /// 輸入一律是 Unix 秒，由呼叫端傳入（伺服器以自己的時間為準）。
    /// </summary>
    public static class DailyClock
    {
        public const int UtcOffsetSeconds = 8 * 3600;
        public const int ResetHour = 5;

        /// <summary>第幾個「遊戲日」（跨過每天 5 點才換日）。</summary>
        public static long DayIndex(long now) => FloorDiv(now + UtcOffsetSeconds - ResetHour * 3600, 86400);

        /// <summary>星期幾（週一 = 0 … 週日 = 6），依遊戲日計算。1970-01-01 是星期四。</summary>
        public static int Weekday(long now)
        {
            long w = (DayIndex(now) + 3) % 7;
            return (int)(w < 0 ? w + 7 : w);
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
