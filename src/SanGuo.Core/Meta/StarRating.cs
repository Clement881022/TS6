namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 關卡星級（GDD 04 §4）：每顆星對應一個條件——通關、我方全員存活、在限定回合內達成通關條件（0 = 不設限）。
    /// 掃蕩要求三星，所以三星 = 「穩穩打過」的證明。
    /// </summary>
    public static class StarRating
    {
        public static int Rate(bool won, int heroDeaths, int turns, int turnPar)
        {
            if (!won) return 0;
            int stars = 1;
            if (heroDeaths == 0) stars++;
            if (turnPar <= 0 || turns <= turnPar) stars++;
            return stars;
        }

        /// <summary>從戰鬥結果評星。</summary>
        public static int Rate(Battle battle, int turnPar)
        {
            if (battle.Result != BattleResult.Won) return 0;
            int deaths = 0;
            foreach (var u in battle.Units)
                if (u.Side == Side.Player && !u.Alive) deaths++;
            return Rate(true, deaths, battle.Turn, turnPar);
        }
    }
}
