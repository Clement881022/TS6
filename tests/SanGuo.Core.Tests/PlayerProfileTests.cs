using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class PlayerProfileTests
    {
        private const long T0 = 1_000_000;

        [Fact]
        public void Stamina_RegeneratesOnePerInterval_AndStopsAtCap()
        {
            var s = new StaminaClock(10, 60, T0);
            Assert.True(s.TrySpend(6, T0));
            Assert.Equal(4, s.Get(T0));
            Assert.Equal(5, s.Get(T0 + 60));
            Assert.Equal(6, s.Get(T0 + 150));
            Assert.Equal(30, s.SecondsToNext(T0 + 150));
            Assert.Equal(10, s.Get(T0 + 100_000));
        }

        [Fact]
        public void Stamina_PartialProgressIsKeptAcrossSpends()
        {
            var s = new StaminaClock(10, 60, T0);
            s.TrySpend(5, T0);
            // 90 秒後有 1 點，再花 1 點，剩餘 30 秒的進度不能被吃掉。
            Assert.True(s.TrySpend(1, T0 + 90));
            Assert.Equal(5, s.Get(T0 + 90));
            Assert.Equal(6, s.Get(T0 + 120));
        }

        [Fact]
        public void Stamina_SpendFailsWhenInsufficient_WithoutChangingValue()
        {
            var s = new StaminaClock(10, 60, T0);
            s.TrySpend(9, T0);
            Assert.False(s.TrySpend(5, T0));
            Assert.Equal(1, s.Get(T0));
        }

        [Fact]
        public void Stamina_GiftsCanExceedCap_AndDoNotDecay()
        {
            var s = new StaminaClock(10, 60, T0);
            s.Add(40, T0);
            Assert.Equal(50, s.Get(T0 + 10_000));
            s.TrySpend(45, T0 + 10_000);
            Assert.Equal(5, s.Get(T0 + 10_000));
            Assert.Equal(6, s.Get(T0 + 10_060));
        }

        [Fact]
        public void Stamina_ClockRollback_DoesNotGiveFreeStamina()
        {
            var s = new StaminaClock(10, 60, T0);
            s.TrySpend(8, T0 + 1000);
            Assert.Equal(2, s.Get(T0));
            Assert.Equal(2, s.Get(T0 + 30));
            Assert.Equal(3, s.Get(T0 + 60));
        }

        [Fact]
        public void StaminaCap_Is60Plus2PerPlayerLevel()
        {
            Assert.Equal(62, PlayerLevelCurve.StaminaCap(1));
            Assert.Equal(140, PlayerLevelCurve.StaminaCap(40));
            Assert.Equal(180, PlayerLevelCurve.StaminaCap(60));
            Assert.Equal(360, PlayerLevelCurve.StaminaRegenSeconds); // 每 6 分鐘 1 點
            Assert.Equal(62, PlayerProfile.CreateNew(T0).Stamina.Get(T0));
        }

        [Fact]
        public void LevelUp_RaisesStaminaCap_ButDoesNotRefill()
        {
            var p = PlayerProfile.CreateNew(T0);
            p.Stamina.TrySpend(50, T0);
            int gained = p.AddExp(PlayerLevelCurve.ExpToNext(1), T0);
            Assert.Equal(1, gained);
            Assert.Equal(2, p.Level);
            Assert.Equal(64, p.Stamina.Cap);
            Assert.Equal(12, p.Stamina.Get(T0));
        }

        [Fact]
        public void Stage_HasNoPlayerLevelGate_AndCostsTenStamina()
        {
            var p = PlayerProfile.CreateNew(T0);
            var late = DemoMeta.Chapter1Stage(5);
            Assert.Equal(10, late.StaminaCost);
            Assert.Equal(StageEntryResult.Ok, p.TryEnterStage(new StageReward { StageId = "6-10", Chapter = 6 }, T0));
            Assert.Equal(52, p.Stamina.Get(T0));
        }

        [Fact]
        public void Stage_FirstClearGivesYuanbaoOnce()
        {
            var p = PlayerProfile.CreateNew(T0);
            var st = new StageReward { StageId = "1-1", Exp = 10, Gold = 50, FirstClearYuanbao = 100 };
            var first = p.ClaimClear(st, T0);
            var second = p.ClaimClear(st, T0);
            Assert.True(first.FirstClear);
            Assert.False(second.FirstClear);
            Assert.Equal(100, p.Yuanbao);
            Assert.Equal(100, p.Gold);
            Assert.Equal(20, p.Exp);
        }

        [Fact]
        public void Stage_NotEnoughStamina()
        {
            var p = PlayerProfile.CreateNew(T0);
            p.Stamina.TrySpend(55, T0);
            var st = new StageReward { StageId = "1-1", StaminaCost = 8 };
            Assert.Equal(StageEntryResult.NotEnoughStamina, p.TryEnterStage(st, T0));
        }
    }
}
