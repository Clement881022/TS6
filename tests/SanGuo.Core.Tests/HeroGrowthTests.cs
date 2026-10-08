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
        public void StatScaling_IsIdentityAtLevel1_NoTable()
        {
            var b = new Stats { Hp = 1000, Atk = 100, Def = 50, Move = 2, Crit = 5 };
            Assert.Equal(1000, HeroGrowth.ScaleStats(b, new HeroState()).Hp);
            var s = HeroGrowth.ScaleStats(b, new HeroState { Level = 11, Stars = 5 }); // 沒給突破表：星級不加數值
            Assert.Equal(1150, s.Hp);   // 1 + 0.015×10
            Assert.Equal(2, s.Move);
            Assert.Equal(1000, b.Hp);
        }

        [Fact]
        public void StatScaling_AppliesBreakthroughStatBonuses_Cumulatively()
        {
            var table = DemoBreakthroughs.Create();
            var b = new Stats { Hp = 1000, Atk = 100, Def = 100, Move = 2 };
            var s1 = HeroGrowth.ScaleStats(b, new HeroState { HeroId = "zhangfei", Stars = 1 }, table);
            Assert.Equal(1100, s1.Hp);              // 1★ 血量 +10%
            Assert.Equal(100, s1.Atk);
            var s3 = HeroGrowth.ScaleStats(b, new HeroState { HeroId = "zhangfei", Stars = 3 }, table);
            Assert.Equal(1100, s3.Hp);              // 2★ 是特殊效果，不加屬性
            Assert.Equal(110, s3.Atk);              // 3★ 攻擊 +10%
            var s4 = HeroGrowth.ScaleStats(b, new HeroState { HeroId = "zhangfei", Stars = 4 }, table);
            Assert.Equal(1250, s4.Hp);              // 血量加成相加：+10% +15%
            Assert.Equal(125, s4.Atk);              // +10% +15%
            Assert.Equal(115, s4.Def);              // 防禦只有 4★ +15%
            var s5 = HeroGrowth.ScaleStats(b, new HeroState { HeroId = "zhangfei", Stars = 5 }, table);
            Assert.Equal(s4.Hp, s5.Hp);             // 5★ 是特殊效果，不再加屬性
            Assert.Equal(2, s5.Move);
        }

        // ---- 突破：屬性與特殊效果混搭 ----

        private static HeroDef ZhangFeiLike() => new HeroDef
        {
            Id = "zhangfei",
            Deck =
            {
                new CardDef { Id = "zf_attack" }, new CardDef { Id = "zf_taunt" },
                new CardDef { Id = "zf_attack" }, new CardDef { Id = "zf_taunt2" },
            },
        };

        [Fact]
        public void EveryHeroGetsFiveStars_SpecialsOnlyAtDesignedSlots_StatsElsewhere()
        {
            var table = DemoBreakthroughs.Create();
            var effects = table.Get("zhangfei");
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, effects.Select(e => e.Stars));
            Assert.Equal(
                new[] { BreakthroughKind.StatBonus, BreakthroughKind.UpgradeCard, BreakthroughKind.StatBonus,
                        BreakthroughKind.StatBonus, BreakthroughKind.UpgradeCard },
                effects.Select(e => e.Kind));
            Assert.All(effects, e => Assert.False(string.IsNullOrEmpty(e.Description)));
        }

        [Fact]
        public void Register_WithoutAnySpecial_FallsBackToStatTemplate()
        {
            var table = new BreakthroughTable();
            table.RegisterStatOnly("plain");
            var effects = table.Get("plain");
            Assert.Equal(5, effects.Count);
            Assert.All(effects, e => Assert.Equal(BreakthroughKind.StatBonus, e.Kind));
            Assert.Empty(table.Get("nobody"));
            Assert.Equal((0, 0, 0), table.StatBonusPct("nobody", 5));
        }

        [Fact]
        public void Register_PartialSpecials_FillsFallbackAtMissingSpecialSlot()
        {
            var table = new BreakthroughTable();
            table.Register("h", new BreakthroughEffect
            {
                Stars = 2, Kind = BreakthroughKind.AddCard, NewCard = new CardDef { Id = "x" }, Description = "x",
            });
            // 5★ 沒設計特殊效果 → 補全屬性 +10%
            // 血量 10(1★)+15(4★)+10(5★)、攻擊 10(3★)+15+10、防禦 15(4★)+10(5★)
            Assert.Equal((35, 35, 25), table.StatBonusPct("h", 5));
        }

        [Fact]
        public void Deck_Unchanged_BeforeSpecialStar_AndOriginalNeverMutated()
        {
            var table = DemoBreakthroughs.Create();
            var hero = ZhangFeiLike();
            Assert.Equal(hero.Deck.Select(c => c.Id), table.ResolveDeck(hero, 1).Select(c => c.Id));
            table.ResolveDeck(hero, 5);
            Assert.Equal("zf_taunt", hero.Deck[1].Id);
        }

        [Fact]
        public void Deck_UpgradeReplacesOnlyTheTargetCard_AtItsStar()
        {
            var table = DemoBreakthroughs.Create();
            var hero = ZhangFeiLike();
            var s2 = table.ResolveDeck(hero, 2).Select(c => c.Id).ToList();
            Assert.Equal(new[] { "zf_attack", "zf_taunt_plus", "zf_attack", "zf_taunt2" }, s2); // 二突只升級其中一張嘲諷
            Assert.Equal(s2, table.ResolveDeck(hero, 4).Select(c => c.Id).ToList());
            var s5 = table.ResolveDeck(hero, 5).Select(c => c.Id).ToList();
            Assert.Equal(new[] { "zf_attack", "zf_taunt_plus", "zf_attack", "zf_taunt2_plus" }, s5); // 五突升級另一張
        }

        [Fact]
        public void AddCard_AppendsToDeck_AndPassivesAreListed()
        {
            var table = new BreakthroughTable();
            table.Register("h",
                new BreakthroughEffect
                {
                    Stars = 2, Kind = BreakthroughKind.AddCard, Description = "新增牌",
                    NewCard = new CardDef { Id = "extra" },
                },
                new BreakthroughEffect
                {
                    Stars = 5, Kind = BreakthroughKind.Passive, PassiveId = "p1", Description = "被動",
                });
            var hero = new HeroDef { Id = "h", Deck = { new CardDef { Id = "a" } } };
            Assert.Equal(new[] { "a" }, table.ResolveDeck(hero, 1).Select(c => c.Id));
            Assert.Equal(new[] { "a", "extra" }, table.ResolveDeck(hero, 2).Select(c => c.Id));
            Assert.Empty(table.ActivePassives("h", 4));
            Assert.Equal(new[] { "p1" }, table.ActivePassives("h", 5));
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
