using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家持有的單一武將的成長狀態。</summary>
    public sealed class HeroState
    {
        public string HeroId = "";
        public int Level = 1;
        /// <summary>突破星級 0–5。</summary>
        public int Stars;
        /// <summary>各卡牌的強化等級（未強化的卡不在表中）。</summary>
        public Dictionary<string, int> CardLevels = new Dictionary<string, int>();
    }

    public enum GrowthResult
    {
        Ok,
        UnknownHero,
        UnknownCard,
        AtCap,
        NeedsPlayerLevel,
        NeedsFullLevel,
        NotEnoughGold,
        NotEnoughMaterial,
    }

    /// <summary>
    /// 武將成長規則：等級、突破（星級）、卡牌強化（見 docs/progression.md）。
    /// 所有數值皆為建議值，集中在這裡方便之後改成資料驅動。
    /// </summary>
    public static class HeroGrowth
    {
        public const string ExpBook = "exp_book";
        public const string CardMaterial = "card_mat";
        public const int MaxStars = 5;
        public const int MaxCardLevel = 5;

        public static string ShardKey(string heroId) => "shard:" + heroId;

        /// <summary>武將等級上限：隨星級提高，五星 = 帳號等級上限。</summary>
        public static int LevelCap(int stars) => 30 + 6 * Math.Min(stars, MaxStars);

        /// <summary>卡牌強化等級上限：1 + 星級。</summary>
        public static int CardLevelCap(int stars) => Math.Min(MaxCardLevel, 1 + stars);

        public static int LevelUpGold(int level) => 40 * level;
        public static int LevelUpBooks(int level) => level / 2 + 1;

        /// <summary>突破（stars → stars+1）所需碎片。</summary>
        public static int BreakthroughShards(int stars) => new[] { 20, 40, 80, 120, 200 }[stars];
        public static int BreakthroughGold(int stars) => 1000 * (stars + 1);

        public static int CardUpgradeMaterial(int cardLevel) => 3 * (cardLevel + 1);
        public static int CardUpgradeGold(int cardLevel) => 200 * (cardLevel + 1);

        /// <summary>屬性倍率（血量 / 攻擊 / 防禦）：每級 +8%、每星 +10%；1 級 0 星為 1.0，與 Demo 關卡校準一致。</summary>
        public static double StatMultiplier(int level, int stars) =>
            (1 + 0.08 * (level - 1)) * (1 + 0.10 * stars);

        /// <summary>卡牌效果倍率加成：每強化 1 級 +15%。</summary>
        public static double CardEffectMultiplier(int cardLevel) => 1 + 0.15 * cardLevel;

        /// <summary>依成長狀態縮放基礎屬性（回傳新物件，不改原資料）。</summary>
        public static Stats ScaleStats(Stats baseStats, HeroState hero)
        {
            var s = baseStats.Clone();
            double m = StatMultiplier(hero.Level, hero.Stars);
            s.Hp = (int)Math.Round(s.Hp * m);
            s.Atk = (int)Math.Round(s.Atk * m);
            s.Def = (int)Math.Round(s.Def * m);
            return s;
        }

        public static GrowthResult LevelUp(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Level >= LevelCap(hero.Stars)) return GrowthResult.AtCap;
            if (hero.Level >= p.Level) return GrowthResult.NeedsPlayerLevel;
            int gold = LevelUpGold(hero.Level);
            int books = LevelUpBooks(hero.Level);
            if (p.Gold < gold) return GrowthResult.NotEnoughGold;
            if (p.GetMaterial(ExpBook) < books) return GrowthResult.NotEnoughMaterial;
            p.Gold -= gold;
            p.AddMaterial(ExpBook, -books);
            hero.Level++;
            return GrowthResult.Ok;
        }

        /// <summary>突破：需先升到目前等級上限，消耗該武將碎片與金幣。</summary>
        public static GrowthResult Breakthrough(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Stars >= MaxStars) return GrowthResult.AtCap;
            if (hero.Level < LevelCap(hero.Stars)) return GrowthResult.NeedsFullLevel;
            int shards = BreakthroughShards(hero.Stars);
            int gold = BreakthroughGold(hero.Stars);
            if (p.Gold < gold) return GrowthResult.NotEnoughGold;
            if (p.GetMaterial(ShardKey(heroId)) < shards) return GrowthResult.NotEnoughMaterial;
            p.Gold -= gold;
            p.AddMaterial(ShardKey(heroId), -shards);
            hero.Stars++;
            return GrowthResult.Ok;
        }

        /// <summary>強化卡牌：消耗卡牌強化素材（非金幣）與金幣，上限由星級決定。</summary>
        public static GrowthResult EnhanceCard(PlayerProfile p, string heroId, string cardId, IEnumerable<string> heroCardIds)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            bool known = false;
            foreach (var id in heroCardIds)
                if (id == cardId) { known = true; break; }
            if (!known) return GrowthResult.UnknownCard;

            hero.CardLevels.TryGetValue(cardId, out int level);
            if (level >= CardLevelCap(hero.Stars)) return GrowthResult.AtCap;
            int mat = CardUpgradeMaterial(level);
            int gold = CardUpgradeGold(level);
            if (p.Gold < gold) return GrowthResult.NotEnoughGold;
            if (p.GetMaterial(CardMaterial) < mat) return GrowthResult.NotEnoughMaterial;
            p.Gold -= gold;
            p.AddMaterial(CardMaterial, -mat);
            hero.CardLevels[cardId] = level + 1;
            return GrowthResult.Ok;
        }
    }
}
