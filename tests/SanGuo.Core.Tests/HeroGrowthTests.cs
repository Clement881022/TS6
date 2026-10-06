using System.Linq;
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
        public void LevelUp_CapsAtPlayerLevel()
        {
            var p = Rich(playerLevel: 12);
            while (HeroGrowth.LevelUp(p, "h") == GrowthResult.Ok) { }
            Assert.Equal(12, p.Heroes["h"].Level);
        }

        [Fact]
        public void Breakthrough_FiveDuplicatesReachFiveStars_NoGoldNoLevelRequirement()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Heroes["h"] = new HeroState { HeroId = "h" };
            long gold = p.Gold;
            for (int copy = 1; copy <= HeroGrowth.MaxStars; copy++)
            {
                p.AddMaterial(HeroGrowth.ShardKey("h"), Gacha.DuplicateShards(Rarity.UR));
                Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
                Assert.Equal(copy, p.Heroes["h"].Stars);
            }
            p.AddMaterial(HeroGrowth.ShardKey("h"), HeroGrowth.CopyShards);
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(gold, p.Gold);
            Assert.Equal(1, p.Heroes["h"].Level);
        }

        [Fact]
        public void Breakthrough_NeedsAFullCopy()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Heroes["h"] = new HeroState { HeroId = "h" };
            p.AddMaterial(HeroGrowth.ShardKey("h"), HeroGrowth.CopyShards - 1);
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(0, p.Heroes["h"].Stars);
        }

        [Fact]
        public void CardEnhance_UsesDedicatedMaterial_AndCapsAtFive()
        {
            var p = Rich();
            for (int i = 0; i < 10; i++) HeroGrowth.EnhanceCard(p, "h", "atk", Cards);
            Assert.Equal(HeroGrowth.MaxCardLevel, p.Heroes["h"].CardLevels["atk"]);
            Assert.Equal(GrowthResult.AtCap, HeroGrowth.EnhanceCard(p, "h", "atk", Cards));
            Assert.Equal(GrowthResult.UnknownCard, HeroGrowth.EnhanceCard(p, "h", "nope", Cards));
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
        public void StatScaling_IsIdentityAtLevel1_AndIgnoresStars()
        {
            var b = new Stats { Hp = 1000, Atk = 100, Def = 50, Speed = 2, Crit = 5 };
            Assert.Equal(1000, HeroGrowth.ScaleStats(b, new HeroState()).Hp);
            var s = HeroGrowth.ScaleStats(b, new HeroState { Level = 11, Stars = 5 });
            Assert.Equal(1800, s.Hp);   // 1 + 0.08×10，星級不加數值
            Assert.Equal(2, s.Speed);
            Assert.Equal(1000, b.Hp);
        }

        // ---- 突破的獨特效果 ----

        private static HeroDef ZhangFeiLike() => new HeroDef
        {
            Id = "zhangfei",
            Deck =
            {
                new CardDef { Id = "zf_attack" }, new CardDef { Id = "zf_break" }, new CardDef { Id = "zf_taunt" },
                new CardDef { Id = "zf_roar" }, new CardDef { Id = "zf_break" },
            },
        };

        [Fact]
        public void Deck_Unchanged_AtZeroStars_AndOriginalNeverMutated()
        {
            var table = DemoBreakthroughs.Create();
            var hero = ZhangFeiLike();
            var deck = table.ResolveDeck(hero, 0);
            Assert.Equal(hero.Deck.Select(c => c.Id), deck.Select(c => c.Id));
            table.ResolveDeck(hero, 5);
            Assert.Equal("zf_break", hero.Deck[1].Id);
        }

        [Fact]
        public void Deck_UpgradeReplacesEveryCopy_AndAddAppends()
        {
            var table = DemoBreakthroughs.Create();
            var hero = ZhangFeiLike();
            var s1 = table.ResolveDeck(hero, 1).Select(c => c.Id).ToList();
            Assert.Equal(new[] { "zf_attack", "zf_break_1", "zf_taunt", "zf_roar", "zf_break_1" }, s1);
            var s2 = table.ResolveDeck(hero, 2).Select(c => c.Id).ToList();
            Assert.Equal("zf_hold", s2.Last());
            Assert.Equal(6, s2.Count);
        }

        [Fact]
        public void Deck_FiveStars_AllUniqueEffectsApplied()
        {
            var table = DemoBreakthroughs.Create();
            var ids = table.ResolveDeck(ZhangFeiLike(), 5).Select(c => c.Id).ToList();
            Assert.Contains("zf_taunt_1", ids);
            Assert.Contains("zf_roar_1", ids);
            Assert.DoesNotContain("zf_taunt", ids);
            Assert.Equal(new[] { "zf_taunt_guard" }, table.ActivePassives("zhangfei", 5));
            Assert.Empty(table.ActivePassives("zhangfei", 3));
        }

        [Fact]
        public void DemoBreakthroughs_EveryStarHasADescribedEffect_NotAStatBump()
        {
            var table = DemoBreakthroughs.Create();
            var effects = table.Get("zhangfei");
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, effects.Select(e => e.Stars));
            Assert.All(effects, e => Assert.False(string.IsNullOrEmpty(e.Description)));
            Assert.Empty(table.Get("nobody"));
        }

        [Fact]
        public void Gacha_Duplicates_FeedBreakthrough_OneCopyPerStar()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Yuanbao = 100_000;
            var pool = new GachaPool { Id = "p", UrRateBp = 10000, UrHeroes = { "h" } };
            for (int i = 0; i < 3; i++) Gacha.Pull(p, pool, 1, new Rng((ulong)i + 1));
            // 抽 3 次 = 1 隻本體 + 2 隻重複 → 可突破 2 星
            Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(GrowthResult.Ok, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(GrowthResult.NotEnoughMaterial, HeroGrowth.Breakthrough(p, "h"));
            Assert.Equal(2, p.Heroes["h"].Stars);
        }
    }
}
