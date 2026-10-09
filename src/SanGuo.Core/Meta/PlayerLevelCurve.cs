namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 帳號等級曲線與體力上限。主線進度不設玩家等級門檻，改以敵人戰力控制（GDD 04 §5.2）。
    /// </summary>
    public static class PlayerLevelCurve
    {
        public const int MaxLevel = 60;

        /// <summary>
        /// 升到下一級所需經驗；已滿級回傳 0。帳號經驗 = 消耗的體力（企劃 2026-10-09），而升級會補滿體力，
        /// 所以每級所需 = 下一級的體力上限 + 0.27 × 等級²（無條件進位）：永遠大於一次補滿的體力（不會形成「升級 → 補滿 → 再升級」的循環），
        /// 淨需求約 0.27 × 等級²，以每天約 240 點自然回復估算，約在第 1／7／14／30 天達到 12／25／32／40 級（以 tools/playsim 校準）。
        /// </summary>
        public static int ExpToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return StaminaCap(level + 1) + (int)System.Math.Ceiling(ExpCurveFactor * level * level);
        }

        public const double ExpCurveFactor = 0.27;

        /// <summary>體力上限（GDD 05 §5）：60 + 2 × 玩家等級（1 級 62，40 級 140，60 級 180）。</summary>
        public static int StaminaCap(int level) => 60 + 2 * level;

        /// <summary>體力回復間隔：每 6 分鐘 1 點（每天 240 點）。</summary>
        public const int StaminaRegenSeconds = 360;
    }
}
