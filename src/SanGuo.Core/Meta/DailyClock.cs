namespace SanGuo.Core.Meta
{
    public static class DailyClock
    {
        public const int UtcOffsetSeconds = 8 * 3600;
        public const int ResetHour = 5;

        public static long DayIndex(long now) => FloorDiv(now + UtcOffsetSeconds - ResetHour * 3600, 86400);

        public static int Weekday(long now)
        {
            long w = (DayIndex(now) + 3) % 7;
            return (int)(w < 0 ? w + 7 : w);
        }

        public static long WeekIndex(long now) => FloorDiv(DayIndex(now) + 3, 7);

        public static string MonthKey(long now)
        {
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
