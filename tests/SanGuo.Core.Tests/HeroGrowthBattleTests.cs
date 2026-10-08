using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class HeroGrowthBattleTests
    {
        [Fact]
        public void BuildDef_ScalesStatsByLevelAndStars_WithoutMutatingOriginal()
        {
            var zf = HeroRoster.ZhangFei();
            int baseHp = zf.Base.Hp;
            var state = new HeroState { HeroId = "zhangfei", Level = 41, Stars = 1 };
            var table = DemoBreakthroughs.Create();

            var built = HeroGrowth.BuildDef(zf, state, table);

            // 等級 41：每級 +1.5%（×1.6）；1★：血量再 +10%（相乘於等級倍率）
            Assert.Equal((int)System.Math.Round(baseHp * 1.6 * 1.10), built.Base.Hp);
            Assert.Equal(baseHp, zf.Base.Hp);
        }

        [Fact]
        public void BuildDef_AppliesBreakthroughCardUpgrade_PerCard()
        {
            var zf = HeroRoster.ZhangFei();
            var table = DemoBreakthroughs.Create();

            var one = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Stars = 1 }, table);
            Assert.Contains(one.Deck, c => c.Id == "zf_taunt");
            Assert.Contains(one.Deck, c => c.Id == "zf_taunt2");

            // 二突只升級其中一張嘲諷，另一張不變。
            var two = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Stars = 2 }, table);
            Assert.DoesNotContain(two.Deck, c => c.Id == "zf_taunt");
            Assert.Contains(two.Deck, c => c.Id == "zf_taunt_plus");
            Assert.Contains(two.Deck, c => c.Id == "zf_taunt2");

            var five = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Stars = 5 }, table);
            Assert.DoesNotContain(five.Deck, c => c.Id == "zf_taunt2");
            Assert.Contains(five.Deck, c => c.Id == "zf_taunt2_plus");
        }

        [Fact]
        public void BuildDef_CardEnhancement_ScalesDamageOnlyForEnhancedCard()
        {
            var gy = HeroRoster.GuanYu();
            var state = new HeroState { HeroId = "guanyu" };
            state.CardLevels["gy_attack"] = 2;

            var built = HeroGrowth.BuildDef(gy, state);

            var attack = built.Deck.First(c => c.Id == "gy_attack");
            Assert.Equal(1.0 * HeroGrowth.CardEffectMultiplier(2), attack.Effects[0].Multiplier, 6);
            var orig = gy.Deck.First(c => c.Id == "gy_attack");
            Assert.Equal(1.0, orig.Effects[0].Multiplier, 6);
            var heavy = built.Deck.First(c => c.Id == "gy_heavy");
            Assert.Equal(gy.Deck.First(c => c.Id == "gy_heavy").Effects[0].Multiplier, heavy.Effects[0].Multiplier, 6);
        }

        [Fact]
        public void BuildSlot_InBattle_UnitHasScaledHp_AndNoDoubleLevelScaling()
        {
            var zf = HeroRoster.ZhangFei();
            var state = new HeroState { HeroId = "zhangfei", Level = 10 };
            var setup = new BattleSetup { NoRandomness = true };
            setup.Heroes.Add(HeroGrowth.BuildSlot(zf, state, new Position(2, 3)));
            setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), new Position(2, 1)));

            var battle = new Battle(setup);

            var unit = battle.Units.First(u => u.Side == Side.Player);
            Assert.Equal((int)System.Math.Round(zf.Base.Hp * HeroGrowth.StatMultiplier(10)), unit.MaxHp);
        }
    }
}
