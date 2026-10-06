using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class HeroGrowthTests
    {
        private static readonly string[] Cards = { "atk", "skill" };

        private static PlayerProfile Rich(int playerLevel = 60)
        {
            var p = PlayerProfile.CreateNew(0);
            p.Level = playerLevel;
            p.Gold = 10_000_000;
            p.AddMaterial(HeroGrowth.ExpBook, 100_000);
            p.AddMaterial(HeroGrowth.CardMaterial, 100_000);
            p.AddMaterial(HeroGrowth.ShardKey("h"), 100_000);
            p.Heroes["h"] = new HeroState { HeroId = "h" };
            return p;
        }

        [Fact]
        public void LevelUp_ConsumesGoldAndBooks()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Level = 10;
            p.Gold = 1000;
            p.AddMaterial(HeroGrowth.ExpBook, 5);
            p.Heroes["h"] = new HeroState { HeroId = "h" };
            Assert.Equal(GrowthResult.Ok, HeroGrowth.LevelUp(p, "h"));
            Assert.Equal(2, p.Heroes["h"].Level);
            Assert.Equal(1000 - HeroGrowth.LevelUpGold(1), p.Gold);
            Assert.Equal(5 - HeroGrowth.LevelUpBooks(1), p.GetMaterial(HeroGrowth.ExpBook));
        }

        [Fact]
        public void LevelUp_Rejections_DoNotConsumeAnything()
        {
            var p = Rich(playerLevel: 1);
            Assert.Equal(GrowthResult.NeedsPlayerLevel, HeroGrowth.LevelUp(p, "h"));
            Assert.Equal(GrowthResult.UnknownHero, HeroGrowth.LevelUp(p, "nobody"));
            p.Level = 60;
            p.Gold = 0;
            Assert.Equal(GrowthResult.NotEnoughGold, HeroGrowth.LevelUp(p, "h"));
            p.Gold = 1_000_000;
            p.Materials[HeroGrowth.ExpBook] = 0;
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.LevelUp(p, "h"));
            Assert.Equal(1, p.Heroes["h"].Level);
            Assert.Equal(1_000_000, p.Gold);
        }

        [Fact]
        public void LevelUp_StopsAtStarCap_ThenBreakthroughRaisesIt()
        {
            var p = Rich();
            int cap0 = HeroGrowth.LevelCap(0);
            while (HeroGrowth.LevelUp(p, "h") == GrowthResult.Ok) { }
            Assert.Equal(cap0, p.Heroes["h"].Level);
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.LevelUp(p, "h"));
            Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(GrowthResult.Ok, HeroGrowth.LevelUp(p, "h"));
        }

        [Fact]
        public void Breakthrough_RequiresFullLevel_AndShards()
        {
            var p = Rich();
            Assert.Equal(GrowthResult.NeedsFullLevel, HeroGrowth.Breakthrough(p, "h"));
            p.Heroes["h"].Level = HeroGrowth.LevelCap(0);
            p.Materials[HeroGrowth.ShardKey("h")] = HeroGrowth.BreakthroughShards(0) - 1;
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.Breakthrough(p, "h"));
            p.AddMaterial(HeroGrowth.ShardKey("h"), 1);
            Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(1, p.Heroes["h"].Stars);
            Assert.Equal(0, p.GetMaterial(HeroGrowth.ShardKey("h")));
        }

        [Fact]
        public void Breakthrough_StopsAtMaxStars()
        {
            var p = Rich();
            var h = p.Heroes["h"];
            for (int i = 0; i < HeroGrowth.MaxStars; i++)
            {
                h.Level = HeroGrowth.LevelCap(h.Stars);
                Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
            }
            Assert.Equal(HeroGrowth.MaxStars, h.Stars);
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(PlayerLevelCurve.MaxLevel, HeroGrowth.LevelCap(HeroGrowth.MaxStars));
        }

        [Fact]
        public void CardEnhance_UsesDedicatedMaterial_AndStarGatedCap()
        {
            var p = Rich();
            Assert.Equal(GrowthResult.Ok, HeroGrowth.EnhanceCard(p, "h", "atk", Cards));
            Assert.Equal(1, p.Heroes["h"].CardLevels["atk"]);
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.EnhanceCard(p, "h", "atk", Cards)); // 0 星上限 1
            Assert.Equal(GrowthResult.UnknownCard, HeroGrowth.EnhanceCard(p, "h", "nope", Cards));
            p.Heroes["h"].Stars = 4;
            for (int i = 0; i < 10; i++) HeroGrowth.EnhanceCard(p, "h", "atk", Cards);
            Assert.Equal(HeroGrowth.MaxCardLevel, p.Heroes["h"].CardLevels["atk"]);
        }

        [Fact]
        public void CardEnhance_DoesNotUseGoldAsMaterial()
        {
            var p = Rich();
            p.Materials[HeroGrowth.CardMaterial] = 0;
            long gold = p.Gold;
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.EnhanceCard(p, "h", "atk", Cards));
            Assert.Equal(gold, p.Gold);
        }

        [Fact]
        public void StatScaling_IsIdentityAtLevel1Star0_AndGrows()
        {
            var b = new Stats { Hp = 1000, Atk = 100, Def = 50, Speed = 2, Crit = 5 };
            var s0 = HeroGrowth.ScaleStats(b, new HeroState());
            Assert.Equal(1000, s0.Hp);
            Assert.Equal(100, s0.Atk);
            var s1 = HeroGrowth.ScaleStats(b, new HeroState { Level = 11, Stars = 1 });
            Assert.Equal(1980, s1.Hp);  // (1+0.8)*1.1
            Assert.Equal(2, s1.Speed);  // 速度 / 爆擊不隨成長
            Assert.Equal(5, s1.Crit);
            Assert.Equal(1000, b.Hp);   // 原資料不被修改
        }

        [Fact]
        public void Gacha_DuplicateShards_FeedBreakthrough()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Yuanbao = 100_000;
            var pool = new GachaPool { Id = "p", UrRateBp = 10000, UrHeroes = { "h" } };
            for (int i = 0; i < 3; i++) Gacha.Pull(p, pool, 1, new Rng((ulong)i + 1));
            Assert.True(p.Heroes.ContainsKey("h"));
            Assert.Equal(2 * Gacha.DuplicateShards(Rarity.UR), p.GetMaterial(HeroGrowth.ShardKey("h")));
        }
    }
}
