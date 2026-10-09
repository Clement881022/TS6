using System.Linq;

namespace SanGuo.Core
{
    public static class AutoPlayer
    {
        public static (CardInstance? Card, Position? Target, Unit? Mover) Pick(Battle battle, System.Func<Battle, CardInstance, int>? priority = null)
        {
            foreach (var move in battle.Hand.Where(c => c.Def.Target == TargetRule.MoveDest && battle.CanPlay(c) == PlayResult.Ok))
            {
                foreach (var hero in battle.AliveUnits(Side.Player))
                {
                    if (hero.Protected || !battle.CanMoveUnit(hero)) continue;
                    var dest = ChooseMove(battle, hero);
                    if (dest != null) return (move, dest, hero);
                }
            }

            var playable = battle.Hand.Where(c => c.Def.Target != TargetRule.MoveDest && battle.CanPlay(c) == PlayResult.Ok
                && (c.Def.Target != TargetRule.Enemy || battle.ResolveTargets(c.Owner!, c.Def) != null));
            var card = priority == null
                ? playable.FirstOrDefault()
                : playable.Where(c => priority(battle, c) >= 0).OrderByDescending(c => priority(battle, c)).FirstOrDefault();
            return (card, null, null);
        }

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
                var (card, target, mover) = Pick(battle, priority);
                if (card == null) break;
                battle.PlayCard(card, target, mover);
            }
            battle.EndTurn();
        }

        public static BattleResult RunToEnd(Battle battle, int maxTurns = 200, System.Func<Battle, CardInstance, int>? priority = null)
        {
            int guard = 0;
            while (battle.Result == BattleResult.Ongoing && guard++ < maxTurns)
                PlayTurn(battle, priority);
            return battle.Result;
        }
    }
}
