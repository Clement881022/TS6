using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum BreakthroughKind
    {
        StatBonus,
        UpgradeCard,
        UnlockPassive,
    }

    public enum StatKind { Hp, Atk, Int, Def, Crit }

    public sealed class BreakthroughEffect
    {
        public int Stars;
        public BreakthroughKind Kind;
        public StatKind Stat;
        public int Value;
        public string TargetCardId = "";
        public PassiveKind Passive;
        public CardDef? NewCard;
        public string Description = "";
    }

    public struct StatMods
    {
        public double Hp, Atk, Int, Def;
        public int Crit, Dodge;

        public static StatMods Identity => new StatMods { Hp = 1, Atk = 1, Int = 1, Def = 1 };
    }

    public static class Breakthroughs
    {
        public const int StatStepPercent = 10;
        public const int MaxStars = 5;

        public static StatKind PrimaryStat(Role role)
        {
            switch (role)
            {
                case Role.Tank: return StatKind.Hp;
                case Role.Warrior: return StatKind.Atk;
                case Role.Ranger: return StatKind.Atk;
                default: return StatKind.Int;
            }
        }

        public static StatKind SecondaryStat(Role role)
        {
            switch (role)
            {
                case Role.Tank: return StatKind.Def;
                case Role.Ranger: return StatKind.Crit;
                default: return StatKind.Hp;
            }
        }

        public static string StatName(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Hp: return "生命";
                case StatKind.Atk: return "攻擊";
                case StatKind.Int: return "謀略";
                case StatKind.Def: return "防禦";
                default: return "爆擊率";
            }
        }

        public static List<BreakthroughEffect> For(HeroDef hero)
        {
            var specials = hero.Deck.Where(c => !c.Basic).Select(c => c).GroupBy(c => c.Id).Select(g => g.First()).ToList();
            var primary = PrimaryStat(hero.Role);
            var secondary = SecondaryStat(hero.Role);
            var fifth = hero.Passive != PassiveKind.None
                ? new BreakthroughEffect
                {
                    Stars = 5, Kind = BreakthroughKind.UnlockPassive, Passive = hero.Passive,
                    Description = $"被動「{Passives.Name(hero.Passive)}」：{Passives.Description(hero.Passive)}",
                }
                : Upgrade(5, specials, 1);
            var list = new List<BreakthroughEffect>
            {
                Stat(1, primary), Upgrade(2, specials, 0), Stat(3, secondary), Stat(4, primary), fifth,
            };
            return list;
        }

        private static BreakthroughEffect Stat(int stars, StatKind stat) => new BreakthroughEffect
        {
            Stars = stars, Kind = BreakthroughKind.StatBonus, Stat = stat, Value = StatStepPercent,
            Description = stat == StatKind.Crit ? $"爆擊率 +{StatStepPercent}%" : $"{StatName(stat)} +{StatStepPercent}%",
        };

        private static BreakthroughEffect Upgrade(int stars, List<CardDef> specials, int index)
        {
            if (index >= specials.Count)
                return new BreakthroughEffect { Stars = stars, Kind = BreakthroughKind.StatBonus, Stat = StatKind.Hp, Value = StatStepPercent, Description = "生命 +10%" };
            var target = specials[index];
            var upgraded = CardLibrary.Upgrade(target);
            return new BreakthroughEffect
            {
                Stars = stars, Kind = BreakthroughKind.UpgradeCard, TargetCardId = target.Id, NewCard = upgraded,
                Description = $"{target.Name} → {upgraded.Name}",
            };
        }

        public static IEnumerable<BreakthroughEffect> Unlocked(HeroDef hero, int stars) =>
            For(hero).Where(e => e.Stars <= stars);

        public static List<CardDef> ResolveDeck(HeroDef hero, int stars)
        {
            var deck = new List<CardDef>(hero.Deck);
            foreach (var e in Unlocked(hero, stars))
            {
                if (e.Kind != BreakthroughKind.UpgradeCard || e.NewCard == null) continue;
                int i = deck.FindIndex(c => c.Id == e.TargetCardId);
                if (i >= 0) deck[i] = e.NewCard;
            }
            return deck;
        }

        public static StatMods Mods(HeroDef hero, int stars)
        {
            double hp = 0, atk = 0, intl = 0, def = 0;
            int crit = 0;
            foreach (var e in Unlocked(hero, stars).Where(e => e.Kind == BreakthroughKind.StatBonus))
            {
                switch (e.Stat)
                {
                    case StatKind.Hp: hp += e.Value / 100.0; break;
                    case StatKind.Atk: atk += e.Value / 100.0; break;
                    case StatKind.Int: intl += e.Value / 100.0; break;
                    case StatKind.Def: def += e.Value / 100.0; break;
                    case StatKind.Crit: crit += e.Value; break;
                }
            }
            return new StatMods { Hp = 1 + hp, Atk = 1 + atk, Int = 1 + intl, Def = 1 + def, Crit = crit };
        }
    }
}
