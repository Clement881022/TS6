namespace SanGuo.Core.Meta
{
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
