using System.Linq;

namespace SanGuo.Core
{
    /// <summary>自動戰鬥：由左至右，能出就出（移動需要玩家選格，自動模式不使用）。</summary>
    public static class AutoPlayer
    {
        /// <param name="priority">可選：出牌優先度（越大越先出，同分維持手牌順序；小於 0 = 這回合先不出）；用來模擬「照教學打」的玩家。</param>
        public static void PlayTurn(Battle battle, System.Func<Battle, CardInstance, int>? priority = null)
        {
            while (battle.Result == BattleResult.Ongoing)
            {
                var playable = battle.Hand.Where(c => battle.CanPlay(c) == PlayResult.Ok);
                var card = priority == null
                    ? playable.FirstOrDefault()
                    : playable.Where(c => priority(battle, c) >= 0).OrderByDescending(c => priority(battle, c)).FirstOrDefault();
                if (card == null) break;
                battle.PlayCard(card);
            }
            battle.EndTurn();
        }

        /// <summary>自動打到分出勝負或達到回合上限；回傳結果。</summary>
        public static BattleResult RunToEnd(Battle battle, int maxTurns = 200, System.Func<Battle, CardInstance, int>? priority = null)
        {
            int guard = 0;
            while (battle.Result == BattleResult.Ongoing && guard++ < maxTurns)
                PlayTurn(battle, priority);
            return battle.Result;
        }
    }
}
