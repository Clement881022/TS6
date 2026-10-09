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

        /// <summary>遊戲週序號（每週一重置；每週任務用）。</summary>
        public static long WeekIndex(long now) => FloorDiv(DayIndex(now) + 3, 7);

        /// <summary>遊戲日所屬的月份（yyyy-MM），將魂商店等每月重置用。</summary>
        public static string MonthKey(long now)
        {
            // days since 1970-01-01 → 公曆年月（Howard Hinnant 的 civil_from_days）
            long z = DayIndex(now) + 719468;
            long era = FloorDiv(z, 146097);
            long doe = z - era * 146097;
            long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
            long y = yoe + era * 400;
            long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
            long mp = (5 * doy + 2) / 153;
            long m = mp < 10 ? mp + 3 : mp - 9;
            if (m <= 2) y++;
            return $"{y:0000}-{m:00}";
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
