namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 關卡星級（建議值，待確認）：★ 通關；★★ 無武將陣亡；★★★ 在回合門檻內通關（門檻 0 = 不限，只看是否存活）。
    /// 掃蕩要求三星，所以三星 = 「穩穩打過」的證明。
    /// </summary>
    public static class StarRating
    {
        public static int Rate(bool won, int heroDeaths, int turns, int turnPar)
        {
            if (!won) return 0;
            int stars = 1;
            if (heroDeaths == 0) stars++;
            if (stars == 2 && (turnPar <= 0 || turns <= turnPar)) stars++;
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
