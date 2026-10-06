using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家持有的單一武將的成長狀態。</summary>
    public sealed class HeroState
    {
        public string HeroId = "";
        public int Level = 1;
        /// <summary>突破星級 0–5（= 額外抽到的重複武將數）。</summary>
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
        NotEnoughGold,
        NotEnoughMaterial,
    }

    /// <summary>
    /// 武將成長規則：等級、突破、卡牌強化（見 docs/progression.md）。數值皆為建議值。
    /// 突破 = 重複抽到同一名武將（1–5 隻），每一星帶來獨特效果（見 <see cref="BreakthroughTable"/>），
    /// 不提供單純的數值成長。
    /// </summary>
    public static class HeroGrowth
    {
        public const string ExpBook = "exp_book";
        public const string CardMaterial = "card_mat";
        public const int MaxStars = 5;
        public const int MaxCardLevel = 5;

        /// <summary>一隻重複武將折算的碎片（每次突破消耗一隻的份量）。</summary>
        public const int CopyShards = 20;

        public static string ShardKey(string heroId) => "shard:" + heroId;

        public static int LevelUpGold(int level) => 40 * level;
        public static int LevelUpBooks(int level) => level / 2 + 1;

        public static int CardUpgradeMaterial(int cardLevel) => 3 * (cardLevel + 1);
        public static int CardUpgradeGold(int cardLevel) => 200 * (cardLevel + 1);

        /// <summary>屬性倍率（血量 / 攻擊 / 防禦）：每級 +8%；1 級為 1.0，與 Demo 關卡校準一致。</summary>
        public static double StatMultiplier(int level) => 1 + 0.08 * (level - 1);

        /// <summary>卡牌效果倍率加成：每強化 1 級 +15%。</summary>
        public static double CardEffectMultiplier(int cardLevel) => 1 + 0.15 * cardLevel;

        /// <summary>依等級縮放基礎屬性（回傳新物件，不改原資料）。</summary>
        public static Stats ScaleStats(Stats baseStats, HeroState hero)
        {
            var s = baseStats.Clone();
            double m = StatMultiplier(hero.Level);
            s.Hp = (int)Math.Round(s.Hp * m);
            s.Atk = (int)Math.Round(s.Atk * m);
            s.Def = (int)Math.Round(s.Def * m);
            return s;
        }

        /// <summary>武將等級不能超過帳號等級（帳號等級上限即為武將等級上限）。</summary>
        public static GrowthResult LevelUp(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Level >= PlayerLevelCurve.MaxLevel) return GrowthResult.AtCap;
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

        /// <summary>突破：消耗一隻重複武將的碎片（<see cref="CopyShards"/>），解鎖下一星的獨特效果。</summary>
        public static GrowthResult Breakthrough(PlayerProfile p, string heroId)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            if (hero.Stars >= MaxStars) return GrowthResult.AtCap;
            if (p.GetMaterial(ShardKey(heroId)) < CopyShards) return GrowthResult.NotEnoughMaterial;
            p.AddMaterial(ShardKey(heroId), -CopyShards);
            hero.Stars++;
            return GrowthResult.Ok;
        }

        /// <summary>強化卡牌：消耗專用的卡牌強化素材（非金幣）與金幣。</summary>
        public static GrowthResult EnhanceCard(PlayerProfile p, string heroId, string cardId, IEnumerable<string> heroCardIds)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return GrowthResult.UnknownHero;
            bool known = false;
            foreach (var id in heroCardIds)
                if (id == cardId) { known = true; break; }
            if (!known) return GrowthResult.UnknownCard;

            hero.CardLevels.TryGetValue(cardId, out int level);
            if (level >= MaxCardLevel) return GrowthResult.AtCap;
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
