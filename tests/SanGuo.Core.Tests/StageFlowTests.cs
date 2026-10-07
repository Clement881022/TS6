using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class StageFlowTests
    {
        private static long Sunday()
        {
            long now = 1_700_000_000;
            while (DailyClock.Weekday(now) != 6) now += 86400;
            return now;
        }

        private static PlayerProfile Player(long now, int level = 3)
        {
            var p = PlayerProfile.CreateNew(now);
            p.Level = level;
            return p;
        }

        private static ReplayRecorder Play(BattleSetup setup)
        {
            var rec = new ReplayRecorder(new Battle(setup));
            for (int i = 0; i < 100 && rec.Battle.Result == BattleResult.Ongoing; i++) rec.PlayAuto();
            return rec;
        }

        [Fact]
        public void Dungeon_StartSpendsStaminaAndWinGrantsRewardAndUnlocksSweep()
        {
            long now = Sunday();
            var p = Player(now);
            var start = StageFlow.Start(p, "res_gold", now, 7);
            Assert.True(start.Ok);
            Assert.Equal(110, p.Stamina.Get(now));
            Assert.Equal("res_gold", p.PendingStageId);

            var rec = Play(DemoMeta.BuildSetup("res_gold", start.Seed)!);
            int goldBefore = p.Gold;
            var done = StageFlow.Finish(p, "res_gold", rec.Actions, now);
            Assert.True(done.Ok && done.Won);
            Assert.Equal(4000, done.Gold);
            Assert.Equal(goldBefore + 4000, p.Gold);
            Assert.Contains("res_gold", p.ClearedStages);
            Assert.Equal("", p.PendingStageId);

            // 通關後可掃蕩，且今日次數已用掉 1 次。
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TrySweep(p, DemoMeta.FindDungeon("res_gold")!, 2, now));
            Assert.Equal(DungeonEntryResult.LimitReached, ResourceDungeons.TrySweep(p, DemoMeta.FindDungeon("res_gold")!, 1, now));
        }

        [Fact]
        public void Dungeon_RespectsLevelRequirement()
        {
            long now = Sunday();
            var p = Player(now, level: 1);
            var start = StageFlow.Start(p, "res_gold", now, 7);
            Assert.False(start.Ok);
            Assert.Equal("LevelTooLow", start.Code);
            Assert.Equal("", p.PendingStageId);
            Assert.Equal(120, p.Stamina.Get(now));
        }

        [Fact]
        public void Dungeon_InvalidReplay_GivesNothingAndClearsPending()
        {
            long now = Sunday();
            var p = Player(now);
            StageFlow.Start(p, "res_gold", now, 7);
            var done = StageFlow.Finish(p, "res_gold", new[] { ReplayAction.Play(99999) }, now);
            Assert.False(done.Ok);
            Assert.True(done.Persist);
            Assert.Equal("", p.PendingStageId);
            Assert.DoesNotContain("res_gold", p.ClearedStages);
        }

        [Fact]
        public void MainStage_FlowStillGradesStars()
        {
            long now = Sunday();
            var p = Player(now);
            var start = StageFlow.Start(p, "1-1", now, 11);
            Assert.True(start.Ok);
            var rec = Play(DemoMeta.BuildSetup("1-1", start.Seed)!);
            var done = StageFlow.Finish(p, "1-1", rec.Actions, now);
            Assert.True(done.Won);
            Assert.InRange(done.Stars, 1, 3);
            Assert.True(done.FirstClear);
            Assert.Contains("1-1", p.ClearedStages);
        }

        [Fact]
        public void Finish_WithoutMatchingPending_IsRejected()
        {
            long now = Sunday();
            var p = Player(now);
            Assert.Equal("no_pending_stage", StageFlow.Finish(p, "1-1", new ReplayAction[0], now).Code);
            StageFlow.Start(p, "1-1", now, 3);
            Assert.Equal("no_pending_stage", StageFlow.Finish(p, "1-2", new ReplayAction[0], now).Code);
        }
    }
}
