using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed partial class Battle
    {
        private void ResolveEffect(Unit owner, EffectDef effect, List<Unit> affected)
        {
            switch (effect.Type)
            {
                case EffectType.Damage:
                    foreach (var t in affected.ToList())
                        if (t.Alive) DealAttackDamage(owner, t, effect.Kind, effect.Multiplier * ConditionalMultiplier(effect, t));
                    break;
                case EffectType.Heal:
                    foreach (var t in affected)
                        if (t.Alive) HealUnit(owner, t, effect.Multiplier);
                    break;
                case EffectType.Shield:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        int amount = DamageCalc.Scale(owner.EffectiveInt, effect.Multiplier);
                        t.Shield += amount;
                        Emit(EventType.Shield, owner.Id, t.Id, amount, "");
                    }
                    break;
                case EffectType.ApplyStatus:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        ApplyStatus(owner, t, effect);
                    }
                    break;
                case EffectType.Draw:
                    int drawn = 0;
                    for (int i = 0; i < effect.Amount && DrawOne(); i++) drawn++;
                    Emit(EventType.Draw, owner.Id, -1, drawn, "");
                    break;
                case EffectType.Move:
                    if (_moveDest != null && owner.Alive) MoveUnit(owner, _moveDest.Value, EventType.Move);
                    break;
                case EffectType.GainCost:
                    GainCost(owner, effect.Amount, "");
                    break;
            }
        }

        private void ApplyStatus(Unit owner, Unit target, EffectDef effect)
        {
            int value = 0;
            switch (effect.Status)
            {
                case StatusType.Burn:
                {
                    value = DamageCalc.Scale(owner.EffectiveInt, effect.Multiplier * (1.0 - Math.Min(1.0, Math.Max(0.0, target.BurnResist))));
                    if (value <= 0) return;
                    AddBurn(target, value);
                    break;
                }
                case StatusType.ArmorBreak:
                    target.DefBreaks.Add(new DefBreak
                    {
                        Percent = Math.Min(0.95, Math.Max(0.0, effect.Multiplier)),
                        Turns = effect.Amount,
                    });
                    value = (int)Math.Round(effect.Multiplier * 100);
                    break;
                case StatusType.Taunt:
                    target.Statuses[StatusType.Taunt] = new StatusState { Turns = effect.Amount, SourceId = owner.Id };
                    if (target.Charging)
                    {
                        target.Charging = false;
                        target.ChargeLeft = 0;
                        target.IdleActions = target.ChargeInterval;
                        Emit(EventType.EnemyChargeBreak, owner.Id, target.Id, 0, "");
                    }
                    break;
                case StatusType.AtkUp:
                case StatusType.IntUp:
                    value = (int)Math.Round(effect.Multiplier * 100, MidpointRounding.AwayFromZero);
                    target.Buffs.Add(new Buff { Type = effect.Status, Power = value, Turns = effect.Amount });
                    break;
                case StatusType.DefUp:
                case StatusType.DodgeUp:
                case StatusType.CritUp:
                    value = (int)Math.Round(effect.Multiplier, MidpointRounding.AwayFromZero);
                    target.Buffs.Add(new Buff { Type = effect.Status, Power = value, Turns = effect.Amount });
                    break;
            }
            Emit(EventType.StatusApplied, owner.Id, target.Id, effect.Status == StatusType.Burn ? value : effect.Amount, effect.Status.ToString());
        }

        private static double ConditionalMultiplier(EffectDef effect, Unit target)
        {
            double m = 1.0;
            if (effect.BonusPerDebuff > 0)
            {
                int debuffs = (target.BurnStacks > 0 ? 1 : 0) + (target.DefBreaks.Count > 0 ? 1 : 0) + (target.Statuses.ContainsKey(StatusType.Taunt) ? 1 : 0);
                m *= 1.0 + effect.BonusPerDebuff * debuffs;
            }
            if (effect.EliteBossMultiplier > 0 && target.Tier != EnemyTier.Normal) m *= effect.EliteBossMultiplier;
            return m;
        }

        private void HealUnit(Unit healer, Unit target, double multiplier)
        {
            double bonus = AliveUnits(target.Side).Any(u => u.Passive == PassiveKind.RenJun) ? 1.0 + Passives.RenJunHeal : 1.0;
            int amount = DamageCalc.Scale(healer.EffectiveInt, multiplier * bonus);
            int healed = Math.Min(amount, target.MaxHp - target.Hp);
            target.Hp += healed;
            Emit(EventType.Heal, healer.Id, target.Id, healed, "");
        }

        private void DealAttackDamage(Unit attacker, Unit target, DamageKind kind, double multiplier)
        {
            if (!Setup.NoRandomness && Rng.Roll(target.EffectiveDodge))
            {
                Emit(EventType.Dodge, attacker.Id, target.Id, 0, "");
                return;
            }
            int dmg;
            bool crit = false;
            if (kind == DamageKind.Magical)
            {
                dmg = DamageCalc.Magical(attacker.EffectiveInt, multiplier);
            }
            else
            {
                crit = !Setup.NoRandomness && Rng.Roll(attacker.EffectiveCrit);
                double def = target.EffectiveDef;
                if (crit && attacker.Passive == PassiveKind.MeiRan) def *= 1.0 - Passives.MeiRanIgnoreDef;
                dmg = DamageCalc.Physical(attacker.EffectiveAtk, multiplier, def, crit, attacker.Stats.CritDmg);
            }
            ApplyDamage(attacker, target, dmg, crit ? "crit" : "");
        }

        private void ApplyDamage(Unit? source, Unit target, int dmg, string text)
        {
            if (target.Passive == PassiveKind.ChangBan && TauntingSomeone(target))
                dmg = Math.Max(1, (int)Math.Round(dmg * (1.0 - Passives.ChangBanReduce), MidpointRounding.AwayFromZero));
            int absorbed = Math.Min(target.Shield, dmg);
            target.Shield -= absorbed;
            target.Hp -= dmg - absorbed;
            Emit(EventType.Damage, source?.Id ?? -1, target.Id, dmg, text);
            if (target.Hp <= 0)
            {
                Kill(target);
                OnKilled(source, target, dmg, text);
                return;
            }
            CheckPhase(target);
            if (target.Passive == PassiveKind.GangLie && !target.PassiveFired && target.Hp * 2 < target.MaxHp)
            {
                target.PassiveFired = true;
                target.Buffs.Add(new Buff { Type = StatusType.DefUp, Power = Passives.GangLieDef, Turns = Passives.GangLieTurns });
                Emit(EventType.PassiveTriggered, target.Id, target.Id, Passives.GangLieDef, Passives.Name(target.Passive));
            }
        }

        private bool TauntingSomeone(Unit unit)
        {
            foreach (var u in Units)
                if (u.Alive && u.Side != unit.Side && u.Statuses.TryGetValue(StatusType.Taunt, out var t) && t.SourceId == unit.Id) return true;
            return false;
        }

        private void OnKilled(Unit? source, Unit victim, int dmg, string text)
        {
            if (victim.Side == Side.Enemy)
            {
                int burn = victim.BurnStacks;
                var taiping = AliveUnits(Side.Player).FirstOrDefault(u => u.Passive == PassiveKind.TaiPing);
                if (burn > 0 && taiping != null)
                {
                    int share = (int)Math.Floor(burn * Passives.TaiPingShare);
                    var adjacent = AliveUnits(Side.Enemy).Where(e => Position.Distance(e.Pos, victim.Pos) == 1).ToList();
                    if (share > 0 && adjacent.Count > 0)
                    {
                        foreach (var adj in adjacent)
                        {
                            AddBurn(adj, share);
                            Emit(EventType.StatusApplied, taiping.Id, adj.Id, share, StatusType.Burn.ToString());
                        }
                        Emit(EventType.PassiveTriggered, taiping.Id, victim.Id, share, Passives.Name(PassiveKind.TaiPing));
                    }
                }
            }
            if (source == null || source.Side != Side.Player || source == victim) return;
            switch (source.Passive)
            {
                case PassiveKind.RenZhongLvBu:
                    source.Buffs.Add(new Buff { Type = StatusType.CritUp, Power = Passives.LvBuCrit, Turns = Passives.LvBuTurns });
                    Emit(EventType.PassiveTriggered, source.Id, source.Id, Passives.LvBuCrit, Passives.Name(source.Passive));
                    break;
                case PassiveKind.BaiMa:
                    if (source.PassiveTurn == Turn) break;
                    source.PassiveTurn = Turn;
                    GainCost(source, 1, "kill");
                    Emit(EventType.PassiveTriggered, source.Id, -1, 1, Passives.Name(source.Passive));
                    break;
                case PassiveKind.WeiZhen:
                    if (text == "splash" || text == "Burn") break;
                    int splash = (int)Math.Round(dmg * Passives.WeiZhenSplash, MidpointRounding.AwayFromZero);
                    var near = AliveUnits(victim.Side).Where(e => Position.Distance(e.Pos, victim.Pos) == 1).ToList();
                    if (splash <= 0 || near.Count == 0) break;
                    Emit(EventType.PassiveTriggered, source.Id, victim.Id, splash, Passives.Name(source.Passive));
                    foreach (var adj in near) if (adj.Alive) ApplyDamage(source, adj, splash, "splash");
                    break;
            }
        }

        private void CheckPhase(Unit unit)
        {
            var def = unit.Enemy;
            if (def == null || def.PhaseHpPercent <= 0 || unit.Phase > 1) return;
            if (unit.Hp * 100 > unit.MaxHp * def.PhaseHpPercent) return;
            unit.Phase = 2;
            if (def.Phase2ChargeTurns > 0) unit.ChargeTurns = def.Phase2ChargeTurns;
            if (def.Phase2ChargeInterval >= 0) unit.ChargeInterval = def.Phase2ChargeInterval;
            if (def.Phase2ChargePower > 0) unit.ChargePower = def.Phase2ChargePower;
            Emit(EventType.EnemyPhase, unit.Id, unit.Id, unit.Phase, "");
        }

        private void Kill(Unit unit)
        {
            unit.Hp = 0;
            unit.Alive = false;
            unit.Charging = false;
            _board[unit.Pos.Lane, unit.Pos.Row] = null;
            Emit(EventType.Death, -1, unit.Id, 0, "");
            if (unit.Side == Side.Player && unit.Hero != null && unit.Hero.Deck.Count > 0) RemoveHeroCards(unit);
        }

        private void RelocateEnemy(Unit enemy, Position dest)
        {
            MoveUnit(enemy, dest, EventType.EnemyMove);
        }

        private void MoveUnit(Unit unit, Position dest, EventType type)
        {
            var from = unit.Pos;
            _board[from.Lane, from.Row] = null;
            _board[dest.Lane, dest.Row] = unit;
            unit.Pos = dest;
            Emit(type, unit.Id, -1, Position.Distance(from, dest), $"{from}->{dest}");
        }

        private void GainCost(Unit source, int amount, string text)
        {
            Cost = Math.Min(Setup.CostCap, Cost + amount);
            Emit(EventType.GainCost, source.Id, -1, amount, text);
        }

        private static void AddBurn(Unit unit, int stacks)
        {
            if (unit.Statuses.TryGetValue(StatusType.Burn, out var burn)) burn.Power += stacks;
            else unit.Statuses[StatusType.Burn] = new StatusState { Power = stacks };
        }
    }
}
