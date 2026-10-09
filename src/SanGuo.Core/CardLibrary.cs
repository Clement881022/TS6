using System;
using System.Collections.Generic;

namespace SanGuo.Core
{
    public static class CardLibrary
    {
        public const int BasicAttackCount = 3;
        public const int SpecialCount = 2;
        public const int DeckSize = BasicAttackCount + SpecialCount;

        public static Stats RoleStats(Role role)
        {
            switch (role)
            {
                case Role.Tank: return new Stats { Hp = 800, Atk = 80, Int = 50, Def = 100, Dodge = 0, Crit = 5, Move = 1, Range = 1 };
                case Role.Warrior: return new Stats { Hp = 600, Atk = 120, Int = 40, Def = 50, Dodge = 0, Crit = 20, Move = 2, Range = 1 };
                case Role.Ranger: return new Stats { Hp = 450, Atk = 110, Int = 50, Def = 30, Dodge = 10, Crit = 10, Move = 2, Range = 2 };
                case Role.Mage: return new Stats { Hp = 400, Atk = 40, Int = 120, Def = 0, Dodge = 5, Crit = 0, Move = 1, Range = 2 };
                case Role.Strategist: return new Stats { Hp = 450, Atk = 40, Int = 100, Def = 10, Dodge = 5, Crit = 0, Move = 1, Range = 2 };
                default: return new Stats { Hp = 450, Atk = 40, Int = 110, Def = 10, Dodge = 5, Crit = 0, Move = 1, Range = 2 };
            }
        }

        public static double RarityStatFactor(Rarity rarity) => rarity == Rarity.UR ? 1.35 : rarity == Rarity.SR ? 1.15 : 1.0;

        public static Stats RarityStats(Role role, Rarity rarity)
        {
            var s = RoleStats(role);
            double f = RarityStatFactor(rarity);
            s.Hp = (int)Math.Round(s.Hp * f, MidpointRounding.AwayFromZero);
            s.Atk = (int)Math.Round(s.Atk * f, MidpointRounding.AwayFromZero);
            s.Int = (int)Math.Round(s.Int * f, MidpointRounding.AwayFromZero);
            s.Def = (int)Math.Round(s.Def * f, MidpointRounding.AwayFromZero);
            return s;
        }

        public static AttackType AttackTypeOf(Role role) => RoleStats(role).Range > 1 ? AttackType.Ranged : AttackType.Melee;

        public static bool BasicIsMagical(Role role) => role == Role.Mage || role == Role.Strategist || role == Role.Healer;

        private static EffectDef Dmg(double mult, DamageKind kind) => new EffectDef { Type = EffectType.Damage, Multiplier = mult, Kind = kind };
        private static EffectDef Status(StatusType type, double power, int turns, bool self = false) =>
            new EffectDef { Type = EffectType.ApplyStatus, Status = type, Multiplier = power, Amount = turns, OnSelf = self };

        private static CardDef Card(string id, string name, int cost, TargetRule target, Shape shape, bool basic, params EffectDef[] effects) =>
            new CardDef { Id = id, Name = name, Cost = cost, Target = target, Shape = shape, Basic = basic, Effects = new List<EffectDef>(effects) };

        public static double RarityBonus(Rarity rarity) => rarity == Rarity.UR ? 2.0 : rarity == Rarity.SR ? 1.0 : 0.0;

        public const double SingleFactor = 1.5, Area3Factor = 2.25;

        public static double Boost(double mult, double factor, Rarity rarity) =>
            Math.Round(mult + RarityBonus(rarity) / factor, 2);

        private static double Proportional(double value, double baseEfficiency, Rarity rarity) =>
            value * (baseEfficiency + RarityBonus(rarity)) / baseEfficiency;

        public static CardDef BasicAttack(string prefix, Role role)
        {
            var kind = BasicIsMagical(role) ? DamageKind.Magical : DamageKind.Physical;
            return Card(prefix + "_attack", "基本攻擊", 1, TargetRule.Enemy, Shape.Single, true, Dmg(1.0, kind));
        }

        public static List<CardDef> RoleSpecials(string p, Role role, Rarity rarity)
        {
            switch (role)
            {
                case Role.Tank:
                {
                    int turns = (int)Math.Ceiling(Proportional(1, 1.5, rarity) - 1e-9);
                    var taunt = Card(p + "_taunt", "嘲諷", 1, TargetRule.AllEnemies, Shape.All, false, Status(StatusType.Taunt, 0, turns));
                    return new List<CardDef> { taunt, Clone(taunt, p + "_taunt2") };
                }
                case Role.Warrior:
                    return new List<CardDef>
                    {
                        Card(p + "_sweep", "橫斬", 2, TargetRule.Enemy, Shape.Row3, false, Dmg(Boost(1.11, Area3Factor, rarity), DamageKind.Physical)),
                        Card(p + "_heavy", "重斬", 2, TargetRule.Enemy, Shape.Single, false, Dmg(Boost(1.67, SingleFactor, rarity), DamageKind.Physical)),
                    };
                case Role.Ranger:
                {
                    var pierce = Card(p + "_pierce", "破甲箭", 2, TargetRule.Enemy, Shape.Single, false,
                        Dmg(Boost(1.5, SingleFactor, rarity), DamageKind.Physical), Status(StatusType.ArmorBreak, 0.25, 2));
                    return new List<CardDef> { pierce, Clone(pierce, p + "_pierce2") };
                }
                case Role.Mage:
                {
                    var fire = Card(p + "_fire", "火攻", 2, TargetRule.Enemy, Shape.Single, false,
                        Status(StatusType.Burn, Boost(1.67, SingleFactor, rarity), 0));
                    return new List<CardDef> { fire, Clone(fire, p + "_fire2") };
                }
                case Role.Healer:
                    return new List<CardDef>
                    {
                        Card(p + "_heal", "治療", 1, TargetRule.Ally, Shape.Single, false,
                            new EffectDef { Type = EffectType.Heal, Multiplier = Boost(1.0, SingleFactor, rarity) }),
                        Card(p + "_barrier", "屏障", 1, TargetRule.Ally, Shape.Single, false,
                            new EffectDef { Type = EffectType.Shield, Multiplier = Boost(0.67, Area3Factor, rarity) }),
                    };
                default:
                {
                    double pct = Math.Round(Proportional(0.25, 1.5, rarity), 2);
                    var rally = Card(p + "_rally", "群體鼓舞", 1, TargetRule.AllAllies, Shape.All, false, Status(StatusType.AtkUp, pct, 2));
                    return new List<CardDef> { rally, Clone(rally, p + "_rally2") };
                }
            }
        }

        public static CardDef Upgrade(CardDef card)
        {
            const double gain = 0.5;
            var up = Clone(card, card.Id + "_plus");
            up.Name = card.Name + "＋";
            double areaFactor = card.Shape == Shape.Row3 || card.Shape == Shape.Column3 ? Area3Factor
                : card.Shape == Shape.Cross ? SingleFactor * 2 : card.Shape == Shape.All ? SingleFactor * 3 : SingleFactor;
            foreach (var e in up.Effects)
            {
                bool done = true;
                switch (e.Type)
                {
                    case EffectType.Damage:
                    case EffectType.Heal:
                        e.Multiplier = Math.Round(e.Multiplier + gain / areaFactor, 2);
                        break;
                    case EffectType.Shield:
                        e.Multiplier = Math.Round(e.Multiplier + gain / (areaFactor * 1.5), 2);
                        break;
                    case EffectType.ApplyStatus:
                        if (e.Status == StatusType.Burn) e.Multiplier = Math.Round(e.Multiplier + gain / SingleFactor, 2);
                        else if (e.Status == StatusType.Taunt) e.Amount += 1;
                        else if (e.Status == StatusType.AtkUp || e.Status == StatusType.IntUp)
                            e.Multiplier = Math.Round(e.Multiplier * (1.5 + gain) / 1.5, 2);
                        else if (e.Status == StatusType.DefUp || e.Status == StatusType.CritUp || e.Status == StatusType.DodgeUp)
                            e.Multiplier += 25;
                        else done = false;
                        break;
                    case EffectType.GainCost:
                    case EffectType.Draw:
                        e.Amount += 1;
                        break;
                    default:
                        done = false;
                        break;
                }
                if (done) break;
            }
            return up;
        }

        private static CardDef Clone(CardDef c, string id) =>
            new CardDef
            {
                Id = id, Name = c.Name, Cost = c.Cost, Target = c.Target, Shape = c.Shape, Basic = c.Basic,
                Unlimited = c.Unlimited, KillRefund = c.KillRefund,
                Effects = c.Effects.ConvertAll(e => e.Clone()),
            };

        public static List<CardDef> BuildDeck(string prefix, Role role, Rarity rarity)
        {
            var deck = new List<CardDef>(DeckSize);
            var attack = BasicAttack(prefix, role);
            for (int i = 0; i < BasicAttackCount; i++) deck.Add(attack);
            deck.AddRange(RoleSpecials(prefix, role, rarity));
            return deck;
        }
    }
}
