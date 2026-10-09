using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public sealed class HeroState
    {
        public string HeroId = "";
        public int Level = 1;
        public int Stars;
        public Dictionary<string, int> Equipment = new Dictionary<string, int>();
    }

    public enum GrowthResult
    {
        Ok,
        UnknownHero,
        AtCap,
        NeedsPlayerLevel,
        NotEnoughGold,
        NotEnoughMaterial,
    }

    public struct DuplicateOutcome
    {
        public int Shards;
        public int Souls;
    }

    public static class HeroGrowth
    {
        public const string HeroExp = "hero_exp";
        public const string Soul = "soul";
        public const int MaxStars = Breakthroughs.MaxStars;

        public static string ShardKey(string heroId) => "shard:" + heroId;

        public static Rarity RarityOf(string heroId) => HeroRoster.Find(heroId)?.Rarity ?? Rarity.R;

        public static int LevelUpExp(int level) => 50 * level;
        public static int LevelUpGold(int level) => 30 * level;

        public static int BreakthroughGold(Rarity rarity, int nextStar) =>
            (rarity == Rarity.UR ? 4000 : rarity == Rarity.SR ? 1500 : 500) * nextStar;

        public static int SoulsPerDuplicate(Rarity rarity) => rarity == Rarity.UR ? 60 : rarity == Rarity.SR ? 20 : 5;

        public static double StatMultiplier(int level) => Battle.HeroLevelFactor(level);

        public static int Shards(PlayerProfile p, string heroId) => p.GetMaterial(ShardKey(heroId));

        public static DuplicateOutcome AddDuplicate(PlayerProfile p, string heroId)
        {
            p.Heroes.TryGetValue(heroId, out var hero);
            int stars = hero?.Stars ?? 0;
            if (stars + Shards(p, heroId) < MaxStars)
            {
                p.AddMaterial(ShardKey(heroId), 1);
                return new DuplicateOutcome { Shards = 1 };
            }
            int souls = SoulsPerDuplicate(RarityOf(heroId));
            p.AddMaterial(Soul, souls);
            return new DuplicateOutcome { Souls = souls };
        }

        public static Stats ScaleStats(HeroDef def, HeroState hero)
        {
            var s = def.Base.Clone();
            double level = StatMultiplier(hero.Level);
            var b = Breakthroughs.Mods(def, hero.Stars);
            var e = Equipment.Mods(def.Role, hero.Equipment);
            s.Hp = (int)Math.Round(s.Hp * level * b.Hp * e.Hp);
            s.Atk = (int)Math.Round(s.Atk * level * b.Atk * e.Atk);
            s.Int = (int)Math.Round(s.Int * level * b.Int * e.Int);
            s.Def = (int)Math.Round(s.Def * level * b.Def * e.Def);
            s.Crit += b.Crit + e.Crit;
            s.Dodge += b.Dodge + e.Dodge;
            return s;
        }

        public static HeroDef BuildDef(HeroDef def, HeroState hero) => new HeroDef
        {
            Id = def.Id,
            Name = def.Name,
            Role = def.Role,
            Rarity = def.Rarity,
            AttackType = def.AttackType,
            Base = ScaleStats(def, hero),
            Deck = Breakthroughs.ResolveDeck(def, hero.Stars),
            Passive = def.Passive,
            Focus = def.Focus,
            ActivePassive = Breakthroughs.Unlocked(def, hero.Stars).Any(e => e.Kind == BreakthroughKind.UnlockPassive) ? def.Passive : PassiveKind.None,
        };

        public static HeroSlot BuildSlot(HeroDef def, HeroState hero, Position pos) =>
            new HeroSlot(BuildDef(def, hero), pos);

        public static GrowthResult LevelUp(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Level >= PlayerLevelCurve.MaxLevel) return GrowthResult.AtCap;
            if (hero.Level >= p.Level) return GrowthResult.NeedsPlayerLevel;
            int gold = LevelUpGold(hero.Level);
            int exp = LevelUpExp(hero.Level);
            if (p.Gold < gold) return GrowthResult.NotEnoughGold;
            if (p.GetMaterial(HeroExp) < exp) return GrowthResult.NotEnoughMaterial;
            p.Gold -= gold;
            p.AddMaterial(HeroExp, -exp);
            hero.Level++;
            return GrowthResult.Ok;
        }

        public static GrowthResult Breakthrough(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Stars >= MaxStars) return GrowthResult.AtCap;
            if (Shards(p, heroId) < 1) return GrowthResult.NotEnoughMaterial;
            int gold = BreakthroughGold(RarityOf(heroId), hero.Stars + 1);
            if (p.Gold < gold) return GrowthResult.NotEnoughGold;
            p.Gold -= gold;
            p.AddMaterial(ShardKey(heroId), -1);
            hero.Stars++;
            return GrowthResult.Ok;
        }
    }
}
