using System.Collections.Generic;
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

        /// <summary>發四名武將給玩家並回傳站好位的編隊。</summary>
        private static List<FormationEntry> Team(PlayerProfile p)
        {
            var ids = new[] { "zhangfei", "guanyu", "huangzhong", "liubei" };
            foreach (var id in ids) p.Heroes[id] = new HeroState { HeroId = id };
            return new List<FormationEntry>
            {
                new FormationEntry("zhangfei", 1, 3), new FormationEntry("guanyu", 2, 3),
                new FormationEntry("huangzhong", 2, 4), new FormationEntry("liubei", 3, 4),
            };
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
            var team = Team(p);
            var start = StageFlow.Start(p, "res_gold", now, 7, team);
            Assert.True(start.Ok);
            Assert.Equal(110, p.Stamina.Get(now));
            Assert.Equal("res_gold", p.PendingStageId);

            var rec = Play(DemoMeta.BuildSetup("res_gold", start.Seed, p, team)!);
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
            var start = StageFlow.Start(p, "res_gold", now, 7, Team(p));
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
            StageFlow.Start(p, "res_gold", now, 7, Team(p));
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

        [Fact]
        public void OpenStage_RequiresValidFormation_AndSpendsNothingWhenInvalid()
        {
            long now = Sunday();
            var p = Player(now);
            var team = Team(p);
            int stamina = p.Stamina.Get(now);

            var cases = new List<IReadOnlyList<FormationEntry>?>
            {
                null,
                new List<FormationEntry>(),
                new List<FormationEntry> { new FormationEntry("zhugeliang", 1, 3) },                    // 沒有這名武將
                new List<FormationEntry> { team[0], new FormationEntry("guanyu", 1, 3) },                // 站位重疊
                new List<FormationEntry> { team[0], new FormationEntry("zhangfei", 3, 3) },              // 同一武將上兩次
                new List<FormationEntry> { new FormationEntry("zhangfei", 9, 3) },                      // 站位在場外
                new List<FormationEntry> { new FormationEntry("zhangfei", 0, 3) },                      // 在棋盤內但不在 3x2 列陣區
                new List<FormationEntry> { new FormationEntry("zhangfei", 2, 2) },                      // 敵我之間的中線列
                team.Concat(new[] { new FormationEntry("zhaoyun", 3, 3) }).ToList(),                    // 超過 4 人（且沒有趙雲）
            };
            foreach (var bad in cases)
            {
                var r = StageFlow.Start(p, "res_gold", now, 7, bad);
                Assert.False(r.Ok);
                Assert.Equal("invalid_formation", r.Code);
            }
            Assert.Equal(stamina, p.Stamina.Get(now));
            Assert.Equal("", p.PendingStageId);

            Assert.True(StageFlow.Start(p, "res_gold", now, 7, team).Ok);
        }

        [Fact]
        public void TutorialLevels_IgnoreFormation_AndLevel9PlusUsesIt()
        {
            long now = Sunday();
            var p = Player(now, level: 9);
            Assert.True(StageFlow.Start(p, "1-1", now, 3).Ok); // 教學關不用編隊
            Assert.True(DemoMeta.BuildSetup("1-1", 3)!.FormationLocked);

            Assert.Null(DemoMeta.BuildSetup("1-9", 3));        // 開放編隊的關卡沒給編隊就建不出來
            Assert.Equal("invalid_formation", StageFlow.Start(p, "1-9", now, 3).Code);

            var team = Team(p);
            var setup = DemoMeta.BuildSetup("1-9", 3, p, team)!;
            Assert.False(setup.FormationLocked);
            Assert.True(setup.AutoAllowed);
            Assert.Empty(setup.ScriptedDraw);
            Assert.Equal(new[] { "zhangfei", "guanyu", "huangzhong", "liubei" }, setup.Heroes.Select(h => h.Def.Id).ToArray());
            Assert.Equal(3, setup.Enemies.Count); // 敵人沿用第 9 關配置
        }

        [Fact]
        public void Formation_AppliesHeroGrowthToBattle_AndSurvivesSaveLoad()
        {
            long now = Sunday();
            var p = Player(now);
            var team = Team(p);
            var baseAtk = DemoMeta.BuildSetup("res_gold", 1, p, team)!.Heroes[0].Def.Base.Atk;

            p.Heroes["zhangfei"].Level = 5;
            var grown = DemoMeta.BuildSetup("res_gold", 1, p, team)!.Heroes[0].Def.Base.Atk;
            Assert.True(grown > baseAtk);

            // 進行中的編隊要存得下來（伺服器重啟後仍能結算）。
            Assert.True(StageFlow.Start(p, "res_gold", now, 7, team).Ok);
            var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal(team.Select(e => (e.HeroId, e.Lane, e.Row)), loaded.PendingFormation.Select(e => (e.HeroId, e.Lane, e.Row)));
            var rec = Play(DemoMeta.BuildSetup("res_gold", 7, p, team)!);
            Assert.True(StageFlow.Finish(loaded, "res_gold", rec.Actions, now).Won);
        }
    }
}
