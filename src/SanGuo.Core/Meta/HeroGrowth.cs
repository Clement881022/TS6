using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家持有的單一武將的成長狀態。</summary>
    public sealed class HeroState
    {
        public string HeroId = "";
        public int Level = 1;
        /// <summary>已突破次數 0–5。</summary>
        public int Stars;
        /// <summary>身上的裝備：部位名稱（Weapon / Armor / Accessory）→ 品階。</summary>
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

    /// <summary>重複武將的處理結果：存為突破用的重複份，或（已滿突時）轉為將魂。</summary>
    public struct DuplicateOutcome
    {
        public int Shards;
        public int Souls;
    }

    /// <summary>
    /// 武將成長規則（GDD 03 §4、05 §3–4）：等級（武將經驗 + 金幣）、突破（重複武將 + 金幣）、裝備。
    /// 成長曲線的數值為待決事項的暫定值。
    /// </summary>
    public static class HeroGrowth
    {
        /// <summary>武將經驗（升級專用）與將魂（滿突後溢出的重複武將轉成）的素材鍵。</summary>
        public const string HeroExp = "hero_exp";
        public const string Soul = "soul";
        public const int MaxStars = Breakthroughs.MaxStars;

        public static string ShardKey(string heroId) => "shard:" + heroId;

        public static Rarity RarityOf(string heroId) => HeroRoster.Find(heroId)?.Rarity ?? Rarity.R;

        // ---- 曲線（暫定值）----

        /// <summary>升到下一級所需的武將經驗與金幣（level = 目前等級）。</summary>
        public static int LevelUpExp(int level) => 50 * level;
        public static int LevelUpGold(int level) => 30 * level;

        /// <summary>第 nextStar 次突破的金幣：稀有度基數 × 突破次數。</summary>
        public static int BreakthroughGold(Rarity rarity, int nextStar) =>
            (rarity == Rarity.UR ? 4000 : rarity == Rarity.SR ? 1500 : 500) * nextStar;

        /// <summary>滿突後再取得的重複武將轉成的將魂：R 5、SR 20、UR 60。</summary>
        public static int SoulsPerDuplicate(Rarity rarity) => rarity == Rarity.UR ? 60 : rarity == Rarity.SR ? 20 : 5;

        /// <summary>屬性倍率（生命、攻擊、謀略、防禦）：每級 +1.5%（線性，以 1 級為基準）。</summary>
        public static double StatMultiplier(int level) => Battle.HeroLevelFactor(level);

        // ---- 重複武將 ----

        public static int Shards(PlayerProfile p, string heroId) => p.GetMaterial(ShardKey(heroId));

        /// <summary>
        /// 取得一份重複武將（抽到、劇情贈送）：「已突次數 + 持有重複份」未達 5 時存為重複份，否則轉為將魂。
        /// 武將本體須已在 <see cref="PlayerProfile.Heroes"/> 中。
        /// </summary>
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

        // ---- 入戰數值 ----

        /// <summary>
        /// 依等級、突破與裝備縮放基礎屬性：生命、攻擊、謀略、防禦 = 基礎 × 等級倍率 × 突破倍率 × 裝備倍率；
        /// 爆擊率與閃避為點數相加。回傳新物件，不改原資料。
        /// </summary>
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

        /// <summary>把玩家的成長狀態套到武將定義上，回傳可直接入戰的新 <see cref="HeroDef"/>（不改原資料）：屬性縮放完成、套牌依突破換成升級版。</summary>
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

        /// <summary>產生入戰用的武將格。屬性已在 <see cref="BuildDef"/> 縮放完，所以戰鬥內的等級固定為 1，避免重複成長。</summary>
        public static HeroSlot BuildSlot(HeroDef def, HeroState hero, Position pos) =>
            new HeroSlot(BuildDef(def, hero), pos);

        // ---- 操作 ----

        /// <summary>武將等級不能超過帳號等級（帳號等級上限即為武將等級上限）。</summary>
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

        /// <summary>突破：消耗 1 份重複武將與金幣，解鎖下一階的效果。</summary>
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
