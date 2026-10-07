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
            var zf = DemoContent.ZhangFei();
            int baseHp = zf.Base.Hp;
            var state = new HeroState { HeroId = "zhangfei", Level = 6, Stars = 1 };
            var table = DemoBreakthroughs.Create();

            var built = HeroGrowth.BuildDef(zf, state, table);

            // 等級 6：+40%；1★：血量再 +10%（相加於等級倍率之外）
            Assert.Equal((int)System.Math.Round(baseHp * 1.4 * 1.10), built.Base.Hp);
            Assert.Equal(baseHp, zf.Base.Hp);
        }

        [Fact]
        public void BuildDef_AppliesBreakthroughCardUpgrade()
        {
            var zf = DemoContent.ZhangFei();
            var table = DemoBreakthroughs.Create();

            var one = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Stars = 1 }, table);
            Assert.Contains(one.Deck, c => c.Id == "zf_taunt");

            var two = HeroGrowth.BuildDef(zf, new HeroState { HeroId = "zhangfei", Stars = 2 }, table);
            Assert.DoesNotContain(two.Deck, c => c.Id == "zf_taunt");
            Assert.Contains(two.Deck, c => c.Id == "zf_taunt_1");
        }

        [Fact]
        public void BuildDef_CardEnhancement_ScalesDamageOnlyForEnhancedCard()
        {
            var zf = DemoContent.ZhangFei();
            var state = new HeroState { HeroId = "zhangfei" };
            state.CardLevels["zf_attack"] = 2;

            var built = HeroGrowth.BuildDef(zf, state);

            var attack = built.Deck.First(c => c.Id == "zf_attack");
            Assert.Equal(1.0 * HeroGrowth.CardEffectMultiplier(2), attack.Effects[0].Multiplier, 6);
            var orig = zf.Deck.First(c => c.Id == "zf_attack");
            Assert.Equal(1.0, orig.Effects[0].Multiplier, 6);
            var stance = built.Deck.First(c => c.Id == "zf_stance");
            Assert.Equal(1.0, stance.Effects[0].Multiplier, 6); // 防禦姿態沒被強化，倍率不變
        }

        [Fact]
        public void BuildSlot_InBattle_UnitHasScaledHp_AndNoDoubleLevelScaling()
        {
            var zf = DemoContent.ZhangFei();
            var state = new HeroState { HeroId = "zhangfei", Level = 10 };
            var setup = new BattleSetup { NoRandomness = true };
            setup.Heroes.Add(HeroGrowth.BuildSlot(zf, state, new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(2, 0)));

            var battle = new Battle(setup);

            var unit = battle.Units.First(u => u.Side == Side.Player);
            Assert.Equal((int)System.Math.Round(zf.Base.Hp * HeroGrowth.StatMultiplier(10)), unit.MaxHp);
        }
    }
}
