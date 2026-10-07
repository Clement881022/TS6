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
    /// 突破 = 重複抽到同一名武將（1–5 隻），每一星給屬性加成或特殊效果（見 <see cref="BreakthroughTable"/>，兩者混搭）。
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

        /// <summary>
        /// 依等級與突破的屬性加成縮放基礎屬性（血量 / 攻擊 / 防禦）：等級倍率 × (1 + 突破加成%)。
        /// 回傳新物件，不改原資料；沒給突破表時只算等級。
        /// </summary>
        public static Stats ScaleStats(Stats baseStats, HeroState hero, BreakthroughTable? table = null)
        {
            var s = baseStats.Clone();
            double m = StatMultiplier(hero.Level);
            var (hp, atk, def) = table == null ? (0, 0, 0) : table.StatBonusPct(hero.HeroId, hero.Stars);
            s.Hp = (int)Math.Round(s.Hp * m * (1 + hp / 100.0));
            s.Atk = (int)Math.Round(s.Atk * m * (1 + atk / 100.0));
            s.Def = (int)Math.Round(s.Def * m * (1 + def / 100.0));
            s.Int = (int)Math.Round(s.Int * m * (1 + atk / 100.0));
            return s;
        }

        /// <summary>
        /// 把玩家的成長狀態套到武將定義上，回傳可直接入戰的新 <see cref="HeroDef"/>（不改原資料）：
        /// 屬性依等級與突破縮放、套牌依突破換成強化版或加牌、卡牌依強化等級放大傷害 / 治療 / 護甲倍率。
        /// 被動尚未接入戰鬥核心，這裡不處理。
        /// </summary>
        public static HeroDef BuildDef(HeroDef def, HeroState hero, BreakthroughTable? table = null)
        {
            var deckSource = table == null ? def.Deck : table.ResolveDeck(def, hero.Stars);
            var deck = new List<CardDef>(deckSource.Count);
            foreach (var card in deckSource)
            {
                hero.CardLevels.TryGetValue(card.Id, out int level);
                deck.Add(level > 0 ? EnhanceCardDef(card, level) : card);
            }
            return new HeroDef
            {
                Id = def.Id,
                Name = def.Name,
                Role = def.Role,
                Rarity = def.Rarity,
                AttackType = def.AttackType,
                Base = ScaleStats(def.Base, hero, table),
                Deck = deck,
            };
        }

        /// <summary>
        /// 產生入戰用的武將格。屬性已在 <see cref="BuildDef"/> 縮放完，所以戰鬥內的等級固定為 1，避免重複成長。
        /// </summary>
        public static HeroSlot BuildSlot(HeroDef def, HeroState hero, Position pos, BreakthroughTable? table = null) =>
            new HeroSlot(BuildDef(def, hero, table), pos);

        private static CardDef EnhanceCardDef(CardDef card, int cardLevel)
        {
            double m = CardEffectMultiplier(cardLevel);
            var effects = new List<EffectDef>(card.Effects.Count);
            foreach (var e in card.Effects)
            {
                var copy = (EffectDef)e.Clone();
                if (e.Type == EffectType.Damage || e.Type == EffectType.Heal || e.Type == EffectType.Armor)
                    copy.Multiplier = e.Multiplier * m;
                effects.Add(copy);
            }
            return new CardDef
            {
                Id = card.Id,
                Name = card.Name,
                Basic = card.Basic,
                Cost = card.Cost,
                Keywords = card.Keywords,
                Target = card.Target,
                Range = card.Range,
                Shape = card.Shape,
                Effects = effects,
            };
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

        /// <summary>突破：消耗一隻重複武將的碎片（<see cref="CopyShards"/>），解鎖下一星的效果。</summary>
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
