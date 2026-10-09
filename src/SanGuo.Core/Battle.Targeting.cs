using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed partial class Battle
    {
        private static readonly int[] DLane = { 1, -1, 0, 0 };
        private static readonly int[] DRow = { 0, 0, 1, -1 };

        public Dictionary<Position, int> ReachableTiles(Unit unit)
        {
            int max = unit.Stats.Move;
            var dist = new Dictionary<Position, int> { [unit.Pos] = 0 };
            var queue = new Queue<Position>();
            queue.Enqueue(unit.Pos);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                int d = dist[p];
                if (d >= max) continue;
                for (int i = 0; i < 4; i++)
                {
                    var n = new Position(p.Lane + DLane[i], p.Row + DRow[i]);
                    if (!InBounds(n) || dist.ContainsKey(n) || _board[n.Lane, n.Row] != null) continue;
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        public List<Unit>? ResolveTargets(Unit owner, CardDef def, Position? chosen = null)
        {
            Side own = owner.Side;
            Side foe = own == Side.Player ? Side.Enemy : Side.Player;
            int range = CardRange(owner, def);
            switch (def.Target)
            {
                case TargetRule.Self:
                    return new List<Unit> { owner };
                case TargetRule.AllAllies:
                    return NonEmpty(AliveUnits(own));
                case TargetRule.AllEnemies:
                    return NonEmpty(AliveUnits(foe));
                case TargetRule.Ally:
                {
                    if (chosen != null)
                    {
                        var picked = UnitAt(own, chosen.Value);
                        if (picked == null || !picked.Alive || Position.Distance(owner.Pos, picked.Pos) > range) return null;
                        return new List<Unit> { picked };
                    }
                    Unit? best = null;
                    foreach (var u in AliveUnits(own))
                    {
                        if (Position.Distance(owner.Pos, u.Pos) > range) continue;
                        if (best == null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp) best = u;
                    }
                    return best == null ? null : new List<Unit> { best };
                }
                case TargetRule.Enemy:
                {
                    Position center;
                    if (chosen != null)
                    {
                        if (!InBounds(chosen.Value) || Position.Distance(owner.Pos, chosen.Value) > range) return null;
                        center = chosen.Value;
                    }
                    else
                    {
                        var inRange = AliveUnits(foe).Where(u => Position.Distance(owner.Pos, u.Pos) <= range).ToList();
                        if (inRange.Count == 0) return null;
                        center = inRange.OrderBy(u => Position.Distance(owner.Pos, u.Pos)).ThenBy(u => u.Hp).ThenBy(u => u.Pos.Lane).First().Pos;
                    }
                    var result = new List<Unit>();
                    foreach (var cell in Targeting.ExpandShape(center, def.Shape, Setup.Lanes, Setup.Rows))
                    {
                        var u = UnitAt(foe, cell);
                        if (u != null && u.Alive) result.Add(u);
                    }
                    return result.Count == 0 ? null : result;
                }
                default:
                    return null;
            }
        }

        public static int CardRange(Unit owner, CardDef def) => def.Unlimited ? 99 : owner.AttackRange;

        public Intent GetIntent(Unit enemy)
        {
            if (!enemy.Alive) return new Intent { Type = Intent.Kind.None };
            if (enemy.Charging) return new Intent { Type = Intent.Kind.Charging };
            if (WillStartCharge(enemy)) return new Intent { Type = Intent.Kind.Charge };

            var heroes = AliveUnits(Side.Player);
            if (heroes.Count == 0) return new Intent { Type = Intent.Kind.None };
            return PlanAttack(enemy, TauntCandidates(enemy, heroes));
        }

        private static bool WillStartCharge(Unit enemy) =>
            enemy.ChargeTurns > 0 && !enemy.Charging && enemy.IdleActions >= enemy.ChargeInterval;

        private List<Unit> TauntCandidates(Unit enemy, List<Unit> heroes)
        {
            if (enemy.Statuses.TryGetValue(StatusType.Taunt, out var taunt))
            {
                var source = heroes.FirstOrDefault(h => h.Id == taunt.SourceId);
                if (source != null) return new List<Unit> { source };
            }
            return heroes;
        }

        private Intent PlanAttack(Unit enemy, List<Unit> candidates)
        {
            int range = enemy.AttackRange;
            var reach = ReachableTiles(enemy);

            Unit? best = null;
            Position bestTile = enemy.Pos;
            foreach (var h in OrderTargets(enemy, candidates))
            {
                Position? tile = null;
                int steps = int.MaxValue;
                foreach (var kv in reach)
                {
                    if (Position.Distance(kv.Key, h.Pos) > range) continue;
                    if (kv.Value < steps || (kv.Value == steps && TileBefore(kv.Key, tile!.Value)))
                    {
                        tile = kv.Key;
                        steps = kv.Value;
                    }
                }
                if (tile == null) continue;
                best = h;
                bestTile = tile.Value;
                break;
            }
            if (best != null)
                return new Intent { Type = Intent.Kind.Attack, Target = best, MoveTo = bestTile == enemy.Pos ? (Position?)null : bestTile };

            var nearest = candidates.OrderBy(h => Position.Distance(enemy.Pos, h.Pos)).ThenBy(h => h.Hp).First();
            Position dest = enemy.Pos;
            int destDist = Position.Distance(enemy.Pos, nearest.Pos);
            int destSteps = 0;
            foreach (var kv in reach)
            {
                int d = Position.Distance(kv.Key, nearest.Pos);
                if (d < destDist || (d == destDist && kv.Value < destSteps))
                {
                    dest = kv.Key;
                    destDist = d;
                    destSteps = kv.Value;
                }
            }
            if (dest == enemy.Pos) return new Intent { Type = Intent.Kind.None };
            return new Intent { Type = Intent.Kind.Move, MoveTo = dest };
        }

        private static IEnumerable<Unit> OrderTargets(Unit enemy, List<Unit> candidates) =>
            enemy.AttackRange > 1
                ? candidates.OrderByDescending(h => h.Pos.Row).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane)
                : candidates.OrderBy(h => Position.Distance(enemy.Pos, h.Pos)).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane);

        private static bool TileBefore(Position a, Position b) => a.Row != b.Row ? a.Row > b.Row : a.Lane < b.Lane;
    }
}
