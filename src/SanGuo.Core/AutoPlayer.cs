using System.Linq;

namespace SanGuo.Core
{
    /// <summary>自動戰鬥：能出就出；打不到敵人的武將才用移動卡往最近的敵人靠近。</summary>
    public static class AutoPlayer
    {
        /// <summary>
        /// 挑下一個操作：先用移動卡讓夠不到敵人的武將靠近（0 費），再出能出的攻擊 / 技能牌。
        /// 回傳 (null, null) 表示這回合沒事可做。
        /// </summary>
        /// <param name="priority">可選：出牌優先度（越大越先出，同分維持手牌順序；小於 0 = 這回合先不出）；用來模擬「照教學打」的玩家。</param>
        public static (CardInstance? Card, Position? Dest) Pick(Battle battle, System.Func<Battle, CardInstance, int>? priority = null)
        {
            // 移動卡 0 費：夠不到敵人的武將先走位，再出牌。
            foreach (var move in battle.Hand.Where(c => c.Def.Target == TargetRule.MoveDest && battle.CanPlay(c) == PlayResult.Ok))
            {
                var dest = ChooseMove(battle, move.Owner);
                if (dest != null) return (move, dest);
            }

            var playable = battle.Hand
                .Where(c => c.Def.Target != TargetRule.MoveDest && battle.CanPlay(c) == PlayResult.Ok);
            var card = priority == null
                ? playable.FirstOrDefault()
                : playable.Where(c => priority(battle, c) >= 0).OrderByDescending(c => priority(battle, c)).FirstOrDefault();
            return (card, null);
        }

        /// <summary>移動目的地：範圍內已有敵人就不動；否則走到離最近敵人最近的格子（同距離取步數少）。</summary>
        public static Position? ChooseMove(Battle battle, Unit hero)
        {
            var foes = battle.AliveUnits(hero.Side == Side.Player ? Side.Enemy : Side.Player);
            if (foes.Count == 0) return null;
            if (foes.Any(f => Position.Distance(hero.Pos, f.Pos) <= hero.AttackRange)) return null;

            var nearest = foes.OrderBy(f => Position.Distance(hero.Pos, f.Pos)).ThenBy(f => f.Hp).First();
            Position? best = null;
            int bestDist = Position.Distance(hero.Pos, nearest.Pos);
            int bestSteps = int.MaxValue;
            foreach (var kv in battle.ReachableTiles(hero))
            {
                if (kv.Value == 0) continue;
                // 遠程到達射程後不必再貼近：以「到射程所需」為準，距離小於射程視為同樣好。
                int d = System.Math.Max(Position.Distance(kv.Key, nearest.Pos), hero.AttackRange);
                int cur = System.Math.Max(bestDist, hero.AttackRange);
                if (d < cur || (d == cur && best != null && kv.Value < bestSteps))
                {
                    best = kv.Key;
                    bestDist = Position.Distance(kv.Key, nearest.Pos);
                    bestSteps = kv.Value;
                }
            }
            return best;
        }

        public static void PlayTurn(Battle battle, System.Func<Battle, CardInstance, int>? priority = null)
        {
            while (battle.Result == BattleResult.Ongoing)
            {
                var (card, dest) = Pick(battle, priority);
                if (card == null) break;
                battle.PlayCard(card, null, dest);
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
