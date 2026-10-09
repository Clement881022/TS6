namespace SanGuo.Core.Meta
{
    public static class PlayerLevelCurve
    {
        public const int MaxLevel = 60;

        public static int ExpToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return StaminaCap(level + 1) + (int)System.Math.Ceiling(ExpCurveFactor * level * level);
        }

        public const double ExpCurveFactor = 0.27;

        public static int StaminaCap(int level) => 60 + 2 * level;

        public const int StaminaRegenSeconds = 360;
    }
}
