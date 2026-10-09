using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed partial class Battle
    {
        public PlayResult PlayCard(CardInstance card, Position? target = null, Unit? mover = null)
        {
            var check = CanPlay(card);
            if (check != PlayResult.Ok) return check;

            Unit owner;
            List<Unit> targets;
            if (card.Def.Target == TargetRule.MoveDest)
            {
                if (mover == null || !Units.Contains(mover) || !CanMoveUnit(mover)) return PlayResult.InvalidMover;
                if (target == null) return PlayResult.NoTarget;
                var d = target.Value;
                if (d == mover.Pos || !ReachableTiles(mover).ContainsKey(d)) return PlayResult.OutOfRange;
                _moveDest = d;
                owner = mover;
                targets = new List<Unit> { mover };
            }
            else
            {
                owner = card.Owner!;
                var resolved = ResolveTargets(owner, card.Def, target);
                if (resolved == null) return target != null ? PlayResult.OutOfRange : PlayResult.NoTarget;
                targets = resolved;
            }

            Cost -= card.Def.Cost;
            Hand.Remove(card);
            Emit(EventType.CardPlayed, owner.Id, -1, card.Def.Cost, card.Def.Name);

            var aliveBefore = targets.Where(t => t.Side != owner.Side && t.Alive).ToList();
            foreach (var effect in card.Def.Effects.Where(e => e.Type != EffectType.Damage).Concat(card.Def.Effects.Where(e => e.Type == EffectType.Damage)))
            {
                var affected = effect.OnSelf ? new List<Unit> { owner } : effect.OnAllies ? AliveUnits(owner.Side) : targets;
                ResolveEffect(owner, effect, affected);
            }
            _moveDest = null;
            if (card.Def.KillRefund > 0 && aliveBefore.Any(t => !t.Alive))
            {
                GainCost(owner, card.Def.KillRefund, "kill");
            }

            DiscardPile.Add(card);
            CheckEnd();
            return PlayResult.Ok;
        }

        public void EndTurn()
        {
            if (Result != BattleResult.Ongoing) return;

            foreach (var healer in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.WuQin))
            {
                var low = AliveUnits(Side.Player).Where(u => u.Hp < u.MaxHp).OrderBy(u => (double)u.Hp / u.MaxHp).FirstOrDefault();
                if (low == null) continue;
                HealUnit(healer, low, Passives.WuQinHeal);
                Emit(EventType.PassiveTriggered, healer.Id, low.Id, 0, Passives.Name(PassiveKind.WuQin));
            }
            TickBurn(Side.Player);
            if (CheckEnd()) return;

            RunEnemyPhase();
            if (Result != BattleResult.Ongoing) return;

            TickBurn(Side.Enemy);
            if (CheckEnd()) return;

            EndRound();
            if ((Setup.Objective == Objective.Escort || Setup.Objective == Objective.Defend)
                && Setup.SurviveTurns > 0 && Turn >= Setup.SurviveTurns)
            {
                Result = BattleResult.Won;
                Emit(EventType.BattleEnd, -1, -1, 0, Result.ToString());
                return;
            }
            if (Setup.TurnLimit > 0 && Turn >= Setup.TurnLimit)
            {
                Result = BattleResult.Lost;
                Emit(EventType.BattleEnd, -1, -1, 0, "turn limit");
                return;
            }
            StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            Turn++;
            Cost = Math.Min(Cost + Setup.CostPerTurn, Setup.CostCap);
            Emit(EventType.TurnStart, -1, -1, Turn, "");

            int draw = Turn == 1 ? _firstTurnDraw : Setup.DrawPerTurn;
            foreach (var u in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.JuZhong))
                if (Turn >= 2) { draw++; Emit(EventType.PassiveTriggered, u.Id, -1, 1, Passives.Name(u.Passive)); }
            for (int i = 0; i < draw && DrawOne(); i++) { }

            if (AliveUnits(Side.Enemy).Any(e => e.Statuses.ContainsKey(StatusType.Taunt)))
                foreach (var u in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.WanRenDi))
                {
                    u.Buffs.Add(new Buff { Type = StatusType.DefUp, Power = Passives.WanRenDiDef, Turns = 1 });
                    Emit(EventType.PassiveTriggered, u.Id, u.Id, Passives.WanRenDiDef, Passives.Name(u.Passive));
                }
        }

        private void RunEnemyPhase()
        {
            foreach (var enemy in AliveUnits(Side.Enemy))
            {
                if (!enemy.Alive) continue;

                if (enemy.Charging)
                {
                    enemy.ChargeLeft--;
                    if (enemy.ChargeLeft <= 0) ReleaseCharge(enemy);
                }
                else
                {
                    var intent = GetIntent(enemy);
                    switch (intent.Type)
                    {
                        case Intent.Kind.Charge:
                            enemy.Charging = true;
                            enemy.ChargeLeft = Math.Max(1, enemy.ChargeTurns);
                            Emit(EventType.EnemyCharge, enemy.Id, -1, enemy.ChargeLeft, "");
                            break;
                        case Intent.Kind.Attack:
                            if (intent.MoveTo != null) RelocateEnemy(enemy, intent.MoveTo.Value);
                            Emit(EventType.EnemyAttack, enemy.Id, intent.Target!.Id, 0, "");
                            DealAttackDamage(enemy, intent.Target!, enemy.Magical ? DamageKind.Magical : DamageKind.Physical, enemy.AttackMultiplier);
                            enemy.IdleActions++;
                            break;
                        case Intent.Kind.Move:
                            RelocateEnemy(enemy, intent.MoveTo!.Value);
                            enemy.IdleActions++;
                            break;
                    }
                }
                if (CheckEnd()) return;
            }
        }

        private void ReleaseCharge(Unit enemy)
        {
            enemy.Charging = false;
            enemy.IdleActions = 0;
            var kind = enemy.Magical ? DamageKind.Magical : DamageKind.Physical;
            foreach (var hero in AliveUnits(Side.Player))
            {
                if (!hero.Alive) continue;
                Emit(EventType.EnemyAttack, enemy.Id, hero.Id, 1, "big");
                DealAttackDamage(enemy, hero, kind, enemy.ChargePower);
            }
        }

        private void EndRound()
        {
            foreach (var unit in Units.Where(u => u.Alive))
            {
                foreach (var b in unit.Buffs) b.Turns--;
                unit.Buffs.RemoveAll(b => b.Turns <= 0);
                foreach (var b in unit.DefBreaks) b.Turns--;
                unit.DefBreaks.RemoveAll(b => b.Turns <= 0);
                if (unit.Statuses.TryGetValue(StatusType.Taunt, out var taunt) && --taunt.Turns <= 0)
                    unit.Statuses.Remove(StatusType.Taunt);
            }
        }

        private void TickBurn(Side side)
        {
            foreach (var unit in AliveUnits(side))
            {
                if (!unit.Alive || !unit.Statuses.TryGetValue(StatusType.Burn, out var burn)) continue;
                int dmg = burn.Power;
                burn.Power = (int)Math.Floor(burn.Power * (1.0 - BurnDecay));
                if (burn.Power <= 0) unit.Statuses.Remove(StatusType.Burn);
                if (dmg > 0) ApplyDamage(null, unit, dmg, "Burn");
            }
        }
    }
}
