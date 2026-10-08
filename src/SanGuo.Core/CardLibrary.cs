using System;
using System.Collections.Generic;

namespace SanGuo.Core
{
    /// <summary>
    /// 卡牌與職業基準資料（GDD 02 §3 標竿卡組、03 §2.1 職業預設、08 §5 屬性基準）。
    /// 每名武將固定 5 張專屬卡：3 張基本攻擊 + 2 張職業特殊卡；同職業共用同一組特殊卡，稀有度只提高特殊卡的效價。
    /// </summary>
    public static class CardLibrary
    {
        public const int BasicAttackCount = 3;
        public const int SpecialCount = 2;
        public const int DeckSize = BasicAttackCount + SpecialCount;

        // ---- 職業基準 ----

        /// <summary>職業的 1 級基準屬性（R 級；稀有度不改變基礎屬性）。</summary>
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

        public static AttackType AttackTypeOf(Role role) => RoleStats(role).Range > 1 ? AttackType.Ranged : AttackType.Melee;

        /// <summary>職業的基本攻擊是否為法術（謀略 ×1.0）。</summary>
        public static bool BasicIsMagical(Role role) => role == Role.Mage || role == Role.Strategist || role == Role.Healer;

        // ---- 效果建構 ----

        private static EffectDef Dmg(double mult, DamageKind kind) => new EffectDef { Type = EffectType.Damage, Multiplier = mult, Kind = kind };
        private static EffectDef Status(StatusType type, double power, int turns, bool self = false) =>
            new EffectDef { Type = EffectType.ApplyStatus, Status = type, Multiplier = power, Amount = turns, OnSelf = self };

        private static CardDef Card(string id, string name, int cost, TargetRule target, Shape shape, bool basic, params EffectDef[] effects) =>
            new CardDef { Id = id, Name = name, Cost = cost, Target = target, Shape = shape, Basic = basic, Effects = new List<EffectDef>(effects) };

        // ---- 效價（GDD 02 §2）----

        /// <summary>稀有度提供的額外效價：SR +1 費、UR +2 費。</summary>
        public static double RarityBonus(Rarity rarity) => rarity == Rarity.UR ? 2.0 : rarity == Rarity.SR ? 1.0 : 0.0;

        /// <summary>每 1 倍率對應的效價。單體傷害 / 治療 / 燃燒 1.5；橫排 / 豎排 3 格傷害、護盾 2.25（佔模 ×1.5 再乘基準 1.5）。</summary>
        public const double SingleFactor = 1.5, Area3Factor = 2.25;

        /// <summary>依稀有度提高倍率：倍率 + 額外效價 ÷ 每倍率效價（保持出牌費用不變）。</summary>
        public static double Boost(double mult, double factor, Rarity rarity) =>
            Math.Round(mult + RarityBonus(rarity) / factor, 2);

        /// <summary>依稀有度按比例提高數值（用於嘲諷回合數、增益強度等無法直接換算倍率的效果）。</summary>
        private static double Proportional(double value, double baseEfficiency, Rarity rarity) =>
            value * (baseEfficiency + RarityBonus(rarity)) / baseEfficiency;

        // ---- 卡牌 ----

        public static CardDef BasicAttack(string prefix, Role role)
        {
            var kind = BasicIsMagical(role) ? DamageKind.Magical : DamageKind.Physical;
            return Card(prefix + "_attack", "基本攻擊", 1, TargetRule.Enemy, Shape.Single, true, Dmg(1.0, kind));
        }

        /// <summary>職業的 2 張特殊卡（R 級為 GDD 標竿；SR / UR 依額外效價提高強度）。</summary>
        public static List<CardDef> RoleSpecials(string p, Role role, Rarity rarity)
        {
            switch (role)
            {
                case Role.Tank:
                {
                    // 嘲諷 ×2：1 費，使全場敵人以該單位為目標 1 回合（效價 1.5）。
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
                    // 破甲箭 ×2：2 費，物攻 ×1.5 並破甲 25%（破甲效價約 0.25）。
                    var pierce = Card(p + "_pierce", "破甲箭", 2, TargetRule.Enemy, Shape.Single, false,
                        Dmg(Boost(1.5, SingleFactor, rarity), DamageKind.Physical), Status(StatusType.ArmorBreak, 0.25, 2));
                    return new List<CardDef> { pierce, Clone(pierce, p + "_pierce2") };
                }
                case Role.Mage:
                {
                    // 火攻 ×2：2 費，單體，施加燃燒層數 = 謀略 ×1.67。
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
                    // 群體鼓舞 ×2：1 費，全體物攻 +25%，2 回合（效價 1.5）。
                    double pct = Math.Round(Proportional(0.25, 1.5, rarity), 2);
                    var rally = Card(p + "_rally", "群體鼓舞", 1, TargetRule.AllAllies, Shape.All, false, Status(StatusType.AtkUp, pct, 2));
                    return new List<CardDef> { rally, Clone(rally, p + "_rally2") };
                }
            }
        }

        private static CardDef Clone(CardDef c, string id) =>
            new CardDef
            {
                Id = id, Name = c.Name, Cost = c.Cost, Target = c.Target, Shape = c.Shape, Basic = c.Basic,
                Effects = c.Effects.ConvertAll(e => e.Clone()),
            };

        /// <summary>
        /// 組出武將的固定套牌：3 張基本攻擊 + 2 張職業特殊卡。
        /// 標竿卡組中成對出現的特殊卡（嘲諷 ×2、破甲箭 ×2、火攻 ×2、群體鼓舞 ×2）以第二個 id（..2 結尾）區分，讓升級可逐張生效。
        /// </summary>
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
