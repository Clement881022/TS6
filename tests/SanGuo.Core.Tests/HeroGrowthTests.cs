using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>武將成長：升級、突破（重複份 + 金幣）、將魂溢出、突破模板與裝備（GDD 03 §4、05 §3–4、09）。</summary>
    public class HeroGrowthTests
    {
        private static PlayerProfile Rich(int playerLevel = 60)
        {
            var p = PlayerProfile.CreateNew(0);
            p.Level = playerLevel;
            p.Gold = 10_000_000;
            p.AddMaterial(HeroGrowth.HeroExp, 1_000_000);
            p.Heroes["zhangfei"] = new HeroState { HeroId = "zhangfei" };
            return p;
        }

        // ---- 升級 ----

        [Fact]
        public void LevelUp_ConsumesGoldAndHeroExp()
        {
            var p = Rich(10);
            p.Gold = 1000;
            p.Materials[HeroGrowth.HeroExp] = 500;
            Assert.Equal(GrowthResult.Ok, HeroGrowth.LevelUp(p, "zhangfei"));
            Assert.Equal(2, p.Heroes["zhangfei"].Level);
            Assert.Equal(1000 - 30, p.Gold);                 // 金幣 = 30 × 等級
            Assert.Equal(500 - 50, p.GetMaterial(HeroGrowth.HeroExp)); // 經驗 = 50 × 等級
        }

        [Fact]
        public void LevelUp_Rejections_DoNotConsumeAnything()
        {
            var p = Rich(playerLevel: 1);
            Assert.Equal(GrowthResult.NeedsPlayerLevel, HeroGrowth.LevelUp(p, "zhangfei"));
            Assert.Equal(GrowthResult.UnknownHero, HeroGrowth.LevelUp(p, "nobody"));
            p.Level = 60;
            p.Gold = 0;
            Assert.Equal(GrowthResult.NotEnoughGold, HeroGrowth.LevelUp(p, "zhangfei"));
            p.Gold = 1_000_000;
            p.Materials[HeroGrowth.HeroExp] = 0;
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.LevelUp(p, "zhangfei"));
            Assert.Equal(1, p.Heroes["zhangfei"].Level);
            Assert.Equal(1_000_000, p.Gold);
        }

        [Fact]
        public void LevelUp_CapsAtPlayerLevel()
        {
            var p = Rich(playerLevel: 12);
            while (HeroGrowth.LevelUp(p, "zhangfei") == GrowthResult.Ok) { }
            Assert.Equal(12, p.Heroes["zhangfei"].Level);
        }

        [Fact]
        public void LevelCurve_To40_IsAffordableInAMonthOfDungeons()
        {
            int exp = 0, gold = 0;
            for (int lv = 1; lv < 40; lv++) { exp += HeroGrowth.LevelUpExp(lv); gold += HeroGrowth.LevelUpGold(lv); }
            Assert.Equal(39_000, exp);
            Assert.Equal(23_400, gold);
        }

        // ---- 重複武將、突破、將魂 ----

        [Fact]
        public void Duplicates_StoreAsShardsUntilFiveThenBecomeSouls()
        {
            var p = Rich();
            for (int i = 1; i <= 5; i++)
            {
                var r = HeroGrowth.AddDuplicate(p, "zhangfei");
                Assert.Equal((1, 0), (r.Shards, r.Souls));
            }
            Assert.Equal(5, HeroGrowth.Shards(p, "zhangfei"));
            var overflow = HeroGrowth.AddDuplicate(p, "zhangfei"); // 張飛是 SR
            Assert.Equal((0, 20), (overflow.Shards, overflow.Souls));
            Assert.Equal(20, p.GetMaterial(HeroGrowth.Soul));
            Assert.Equal(5, HeroGrowth.Shards(p, "zhangfei"));
        }

        [Fact]
        public void Duplicates_CountBreakthroughsAlreadyDone()
        {
            var p = Rich();
            p.Heroes["zhangfei"].Stars = 3;
            HeroGrowth.AddDuplicate(p, "zhangfei");
            HeroGrowth.AddDuplicate(p, "zhangfei");
            var third = HeroGrowth.AddDuplicate(p, "zhangfei"); // 已突 3 + 持有 2 = 5
            Assert.True(third.Souls > 0);
        }

        [Theory]
        [InlineData("r_shield", 5)]
        [InlineData("zhangfei", 20)]
        [InlineData("xiahoudun", 60)]
        public void SoulConversion_DependsOnRarity(string heroId, int souls)
        {
            Assert.Equal(souls, HeroGrowth.SoulsPerDuplicate(HeroGrowth.RarityOf(heroId)));
        }

        [Fact]
        public void Breakthrough_ConsumesOneShardAndGold_UpToFiveTimes()
        {
            var p = Rich();
            for (int i = 0; i < 5; i++) HeroGrowth.AddDuplicate(p, "zhangfei");
            long gold = p.Gold;
            for (int star = 1; star <= 5; star++)
            {
                Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "zhangfei"));
                Assert.Equal(star, p.Heroes["zhangfei"].Stars);
            }
            // SR：1500 × (1+2+3+4+5)
            Assert.Equal(gold - 1500 * 15, p.Gold);
            Assert.Equal(0, HeroGrowth.Shards(p, "zhangfei"));
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.Breakthrough(p, "zhangfei"));
        }

        [Fact]
        public void Breakthrough_Rejections_DoNotConsume()
        {
            var p = Rich();
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.Breakthrough(p, "zhangfei"));
            HeroGrowth.AddDuplicate(p, "zhangfei");
            p.Gold = 100;
            Assert.Equal(GrowthResult.NotEnoughGold, HeroGrowth.Breakthrough(p, "zhangfei"));
            Assert.Equal(1, HeroGrowth.Shards(p, "zhangfei"));
            Assert.Equal(100, p.Gold);
            Assert.Equal(GrowthResult.UnknownHero, HeroGrowth.Breakthrough(p, "nobody"));
        }

        [Theory]
        [InlineData(Rarity.R, 1, 500)]
        [InlineData(Rarity.SR, 2, 3000)]
        [InlineData(Rarity.UR, 5, 20000)]
        public void BreakthroughGold_ScalesWithRarityAndStep(Rarity rarity, int step, int gold)
        {
            Assert.Equal(gold, HeroGrowth.BreakthroughGold(rarity, step));
        }

        // ---- 突破模板 ----

        [Fact]
        public void Template_FiveStepsFollowGdd_AndMainStatsSumToPlus20Percent()
        {
            var sword = HeroRoster.MilitiaSword(); // 戰士：攻擊 / 生命
            var fx = Breakthroughs.For(sword);
            Assert.Equal(5, fx.Count);
            Assert.Equal(new[] { BreakthroughKind.StatBonus, BreakthroughKind.UpgradeCard, BreakthroughKind.StatBonus, BreakthroughKind.StatBonus, BreakthroughKind.UpgradeCard },
                fx.Select(e => e.Kind).ToArray());
            Assert.Equal(StatKind.Atk, fx[0].Stat);
            Assert.Equal(StatKind.Hp, fx[2].Stat);
            Assert.Equal(StatKind.Atk, fx[3].Stat);

            var full = Breakthroughs.Mods(sword, 5);
            Assert.Equal(1.2, full.Atk, 6);
            Assert.Equal(1.1, full.Hp, 6);
            Assert.Equal(1.0, full.Int, 6);
            Assert.Equal(1.1, Breakthroughs.Mods(sword, 3).Hp, 6);
        }

        [Theory]
        [InlineData(Role.Tank, StatKind.Hp, StatKind.Def)]
        [InlineData(Role.Warrior, StatKind.Atk, StatKind.Hp)]
        [InlineData(Role.Ranger, StatKind.Atk, StatKind.Crit)]
        [InlineData(Role.Mage, StatKind.Int, StatKind.Hp)]
        [InlineData(Role.Strategist, StatKind.Int, StatKind.Hp)]
        [InlineData(Role.Healer, StatKind.Int, StatKind.Hp)]
        public void PrimaryAndSecondaryStats_FollowRole(Role role, StatKind primary, StatKind secondary)
        {
            Assert.Equal(primary, Breakthroughs.PrimaryStat(role));
            Assert.Equal(secondary, Breakthroughs.SecondaryStat(role));
        }

        [Fact]
        public void RangerCritBonus_IsPercentagePoints()
        {
            var archer = HeroRoster.MilitiaArcher();
            var def = HeroGrowth.BuildDef(archer, new HeroState { HeroId = archer.Id, Stars = 3 });
            Assert.Equal(archer.Base.Crit + 10, def.Base.Crit);
        }

        [Fact]
        public void CardUpgrades_ApplyPerCard_AtStarTwoAndFive()
        {
            var sword = HeroRoster.MilitiaSword();
            var d1 = Breakthroughs.ResolveDeck(sword, 1);
            Assert.Equal(sword.Deck.Select(c => c.Id), d1.Select(c => c.Id));

            var d2 = Breakthroughs.ResolveDeck(sword, 2);
            Assert.Contains(d2, c => c.Id == "r_swd_sweep_plus");
            Assert.Contains(d2, c => c.Id == "r_swd_heavy");          // 另一張尚未升級
            Assert.DoesNotContain(d2, c => c.Id == "r_swd_sweep");

            var d5 = Breakthroughs.ResolveDeck(sword, 5);
            Assert.Contains(d5, c => c.Id == "r_swd_heavy_plus");
            Assert.Equal(5, d5.Count);
            Assert.Equal(5, sword.Deck.Count);                         // 原定義不變
            Assert.Contains(sword.Deck, c => c.Id == "r_swd_sweep");
        }

        [Fact]
        public void PairedCards_UpgradeIndependently_TauntExample()
        {
            // GDD 02 §5 範例：二突將 1 張嘲諷升級為「嘲諷＋」，五突再將另 1 張升級。
            var shield = HeroRoster.MilitiaShield();
            var d2 = Breakthroughs.ResolveDeck(shield, 2);
            Assert.Equal(1, d2.Count(c => c.Name == "嘲諷＋"));
            Assert.Equal(1, d2.Count(c => c.Name == "嘲諷"));
            var d5 = Breakthroughs.ResolveDeck(shield, 5);
            Assert.Equal(2, d5.Count(c => c.Name == "嘲諷＋"));
            Assert.Equal(2, d5.First(c => c.Name == "嘲諷＋").Effects[0].Amount); // 持續回合 +1
        }

        [Fact]
        public void Upgrade_AddsAboutHalfCostOfEfficiency()
        {
            var sword = HeroRoster.MilitiaSword();
            var heavy = sword.Deck.First(c => c.Id.EndsWith("_heavy"));
            var up = CardLibrary.Upgrade(heavy);
            Assert.Equal(heavy.Effects[0].Multiplier + 0.33, up.Effects[0].Multiplier, 2); // 0.5 ÷ 1.5
            Assert.Equal(heavy.Cost, up.Cost);
            var sweep = CardLibrary.Upgrade(sword.Deck.First(c => c.Id.EndsWith("_sweep")));
            Assert.Equal(1.11 + 0.22, sweep.Effects[0].Multiplier, 2); // 0.5 ÷ 2.25
        }

        // ---- 數值縮放 ----

        [Fact]
        public void ScaleStats_MultipliesLevelBreakthroughAndEquipment()
        {
            var sword = HeroRoster.MilitiaSword();
            var hero = new HeroState { HeroId = sword.Id, Level = 41, Stars = 5 };
            hero.Equipment["Weapon"] = 5;                               // 攻擊 +50%
            hero.Equipment["Armor"] = 2;                                // 生命 / 防禦 +20%
            var s = HeroGrowth.ScaleStats(sword, hero);
            Assert.Equal((int)System.Math.Round(600 * 1.6 * 1.1 * 1.2), s.Hp);
            Assert.Equal((int)System.Math.Round(120 * 1.6 * 1.2 * 1.5), s.Atk);
            Assert.Equal((int)System.Math.Round(50 * 1.6 * 1.0 * 1.2), s.Def);
            Assert.Equal(sword.Base.Move, s.Move);
            Assert.Equal(sword.Base.Range, s.Range);
        }

        [Fact]
        public void FullMonthBuild_MatchesGddContributionTable()
        {
            // GDD 03 §4.2：40 級、5 突、裝備平均 3.5 階 → 攻擊約 ×2.6、生命約 ×2.4。
            var sword = HeroRoster.MilitiaSword();
            var hero = new HeroState { HeroId = sword.Id, Level = 40, Stars = 5 };
            hero.Equipment["Weapon"] = 4;
            hero.Equipment["Armor"] = 3;
            var s = HeroGrowth.ScaleStats(sword, hero);
            Assert.InRange(s.Atk / (double)sword.Base.Atk, 2.5, 2.7);
            Assert.InRange(s.Hp / (double)sword.Base.Hp, 2.1, 2.5);
        }

        [Fact]
        public void BuildDef_DoesNotMutateOriginal_AndKeepsFiveCards()
        {
            var zf = HeroRoster.ZhangFei();
            int hp = zf.Base.Hp;
            var built = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Level = 20, Stars = 5 });
            Assert.Equal(hp, zf.Base.Hp);
            Assert.True(built.Base.Hp > hp);
            Assert.Equal(5, built.Deck.Count);
        }

        // ---- 裝備 ----

        [Fact]
        public void Equipment_EquipSwapsAndReturnsOldToInventory()
        {
            var p = Rich();
            p.AddMaterial(Equipment.ItemKey(EquipSlot.Weapon, 2), 1);
            p.AddMaterial(Equipment.ItemKey(EquipSlot.Weapon, 4), 1);
            Assert.Equal(EquipResult.Ok, Equipment.Equip(p, "zhangfei", EquipSlot.Weapon, 2));
            Assert.Equal(EquipResult.Ok, Equipment.Equip(p, "zhangfei", EquipSlot.Weapon, 4));
            Assert.Equal(4, p.Heroes["zhangfei"].Equipment["Weapon"]);
            Assert.Equal(1, Equipment.Count(p, EquipSlot.Weapon, 2));
            Assert.Equal(0, Equipment.Count(p, EquipSlot.Weapon, 4));
            Assert.Equal(EquipResult.NotOwned, Equipment.Equip(p, "zhangfei", EquipSlot.Armor, 1));
            Assert.Equal(EquipResult.UnknownHero, Equipment.Equip(p, "nobody", EquipSlot.Weapon, 2));
            Assert.Equal(EquipResult.InvalidTier, Equipment.Equip(p, "zhangfei", EquipSlot.Weapon, 6));

            Assert.Equal(EquipResult.Ok, Equipment.Unequip(p, "zhangfei", EquipSlot.Weapon));
            Assert.Equal(1, Equipment.Count(p, EquipSlot.Weapon, 4));
            Assert.Equal(EquipResult.NothingEquipped, Equipment.Unequip(p, "zhangfei", EquipSlot.Weapon));
        }

        [Fact]
        public void Equipment_DismantleGivesGold()
        {
            var p = Rich();
            p.Gold = 0;
            p.AddMaterial(Equipment.ItemKey(EquipSlot.Armor, 3), 2);
            Assert.Equal(EquipResult.Ok, Equipment.Dismantle(p, EquipSlot.Armor, 3, 2));
            Assert.Equal(Equipment.DismantleGold(3) * 2, p.Gold);
            Assert.Equal(EquipResult.NotOwned, Equipment.Dismantle(p, EquipSlot.Armor, 3, 1));
        }

        [Fact]
        public void Equipment_TierBonusIsLinear10PercentPerTier()
        {
            var empty = new System.Collections.Generic.Dictionary<string, int>();
            Assert.Equal(1.0, Equipment.Mods(Role.Warrior, empty).Atk);
            for (int tier = 1; tier <= 5; tier++)
            {
                var eq = new System.Collections.Generic.Dictionary<string, int> { ["Weapon"] = tier, ["Armor"] = tier };
                Assert.Equal(1 + 0.1 * tier, Equipment.Mods(Role.Warrior, eq).Atk, 6);
                Assert.Equal(1 + 0.1 * tier, Equipment.Mods(Role.Warrior, eq).Hp, 6);
                Assert.Equal(1 + 0.1 * tier, Equipment.Mods(Role.Mage, eq).Int, 6); // 法系武器加謀略
                Assert.Equal(1.0, Equipment.Mods(Role.Mage, eq).Atk);
            }
        }

        [Fact]
        public void Equipment_KeyParsing()
        {
            Assert.True(Equipment.TryParseKey("eq:armor:3", out var slot, out int tier));
            Assert.Equal((EquipSlot.Armor, 3), (slot, tier));
            Assert.False(Equipment.TryParseKey("shard:x", out _, out _));
            Assert.False(Equipment.TryParseKey("eq:weapon:9", out _, out _));
        }
    }
}
