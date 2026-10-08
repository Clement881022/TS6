namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 帳號等級曲線與體力上限。主線進度不設玩家等級門檻，改以敵人戰力控制（GDD 04 §5.2）。
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

        /// <summary>體力上限（GDD 05 §5）：60 + 2 × 玩家等級（1 級 62，40 級 140，60 級 180）。</summary>
        public static int StaminaCap(int level) => 60 + 2 * level;

        /// <summary>體力回復間隔：每 6 分鐘 1 點（每天 240 點）。</summary>
        public const int StaminaRegenSeconds = 360;
    }
}
