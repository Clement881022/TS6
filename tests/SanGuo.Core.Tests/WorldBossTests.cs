using System.Collections.Generic;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    public class WorldBossTests
    {
        private readonly ITestOutputHelper _out;
        public WorldBossTests(ITestOutputHelper output) { _out = output; }

        // 2026-10-15 12:00（北京時間）與隔月 2026-11-02 12:00。
        private const long Oct = 1792036800, Nov = 1793592000;

        private static PlayerProfile Unlocked(out List<FormationEntry> team, (int Level, int Stars, int Gear)? growth = null)
        {
            var p = PlayerProfile.CreateNew(0);
            p.ClearedStages.Add(WorldBoss.UnlockStage);
            var g = growth ?? CampaignBalanceTests.Growth[6];
            team = new List<FormationEntry>();
            foreach (var (id, lane, row) in new[] { ("zhangfei", 2, 3), ("guanyu", 1, 3), ("handang", 3, 4), ("liubei", 2, 4) })
            {
                var hero = new HeroState { HeroId = id, Level = g.Level, Stars = g.Stars };
                foreach (var slot in Equipment.Slots) hero.Equipment[slot.ToString()] = g.Gear;
                p.Heroes[id] = hero;
                team.Add(new FormationEntry(id, lane, row));
            }
            return p;
        }

        private static StageFinishOutcome PlayAuto(PlayerProfile p, List<FormationEntry> team, long now, ulong seed)
        {
            var start = StageFlow.Start(p, WorldBoss.StageId, now, seed, team);
            Assert.True(start.Ok, start.Code);
            var battle = new Battle(DemoMeta.BuildSetup(WorldBoss.StageId, start.Seed, p, team)!);
            var rec = new ReplayRecorder(battle);
            for (int i = 0; i < 200 && battle.Result == BattleResult.Ongoing; i++) rec.PlayAuto();
            return StageFlow.Finish(p, WorldBoss.StageId, rec.Actions, now);
        }

        [Fact]
        public void Locked_UntilChapter2Cleared()
        {
            var p = Unlocked(out var team);
            p.ClearedStages.Clear();
            Assert.Equal("locked", StageFlow.Start(p, WorldBoss.StageId, Oct, 1, team).Code);
        }

        [Fact]
        public void ThreeAttemptsPerDay_NoStamina_ResetNextDay()
        {
            var p = Unlocked(out var team);
            int stamina = p.Stamina.Get(Oct);
            for (int i = 0; i < WorldBoss.DailyAttempts; i++) Assert.True(StageFlow.Start(p, WorldBoss.StageId, Oct, (ulong)i + 1, team).Ok);
            Assert.Equal("no_attempts", StageFlow.Start(p, WorldBoss.StageId, Oct, 9, team).Code);
            Assert.Equal(stamina, p.Stamina.Get(Oct));
            Assert.Equal(WorldBoss.DailyAttempts, WorldBoss.AttemptsLeft(p, Oct + 86400));
        }

        [Fact]
        public void Score_IsDamageToBoss_AndBestOnlyGoesUp()
        {
            var p = Unlocked(out var team);
            var first = PlayAuto(p, team, Oct, 7);
            Assert.True(first.Ok);
            Assert.True(first.Damage > 0);
            Assert.True(first.NewBest);
            Assert.Equal(first.Damage, p.WorldBoss.Best);
            long best = p.WorldBoss.Best;
            var second = PlayAuto(p, team, Oct, 8);
            Assert.Equal(System.Math.Max(best, second.Damage), p.WorldBoss.Best);
        }

        /// <summary>Boss 要夠硬：1.0 畢業養成（第 6 章末）的自動戰鬥 10 回合內打不死，分數才有差距可比。</summary>
        [Fact]
        public void Boss_SurvivesTenTurnsAgainstEndgameTeam()
        {
            var p = Unlocked(out var team);
            long hp = WorldBoss.Setup(WorldBoss.SeasonOf(Oct), 1).Enemies[0].Def.Base.Hp;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                p.WorldBoss.Used = 0;
                var r = PlayAuto(p, team, Oct, seed);
                _out.WriteLine($"{WorldBoss.BossOf(WorldBoss.SeasonOf(Oct)).Name}：傷害 {r.Damage}");
                Assert.False(r.Won, "Boss 不該被畢業隊伍的自動戰鬥打死");
            }
        }

        [Fact]
        public void BossRotatesBySeason()
        {
            Assert.NotEqual(WorldBoss.BossOf("2026-10").Id, WorldBoss.BossOf("2026-11").Id);
            Assert.Equal(WorldBoss.BossOf("2026-10").Id, WorldBoss.BossOf("2027-04").Id); // 6 隻循環
        }

        [Fact]
        public void SeasonRollover_SettlesRewardByRank()
        {
            var board = new InMemoryWorldBossBoard();
            for (int i = 0; i < 99; i++) board.Submit("2026-10", "other" + i, 1000 + i);
            var p = Unlocked(out _);
            WorldBoss.Roll(p, Oct);
            WorldBoss.Record(p, 999_999);
            board.Submit("2026-10", "me", 999_999);
            int yuanbao = p.Yuanbao;

            WorldBoss.SettlePending(p, board, Nov);
            Assert.Equal("2026-11", p.WorldBoss.Season);
            Assert.Equal(0, p.WorldBoss.Best);
            Assert.Equal(1, p.WorldBoss.LastRank);
            Assert.Equal(100, p.WorldBoss.LastTotal);
            Assert.Equal(yuanbao + 3000, p.Yuanbao);
            Assert.Contains("群雄榜第 1 名", p.WorldBoss.Title);

            WorldBoss.SettlePending(p, board, Nov); // 不重複發
            Assert.Equal(yuanbao + 3000, p.Yuanbao);
        }

        [Fact]
        public void NoScore_NoReward()
        {
            var p = Unlocked(out _);
            WorldBoss.Roll(p, Oct);
            int yuanbao = p.Yuanbao;
            WorldBoss.SettlePending(p, new InMemoryWorldBossBoard(), Nov);
            Assert.Equal(yuanbao, p.Yuanbao);
            Assert.Equal("", p.WorldBoss.LastSeason);
        }

        [Fact]
        public void State_SurvivesSerialization()
        {
            var p = Unlocked(out _);
            WorldBoss.Roll(p, Oct);
            p.WorldBoss.Used = 2;
            WorldBoss.Record(p, 12345);
            p.WorldBoss.Title = "測試稱號";
            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal("2026-10", back.WorldBoss.Season);
            Assert.Equal(12345, back.WorldBoss.Best);
            Assert.Equal(2, back.WorldBoss.Used);
            Assert.Equal(p.WorldBoss.Day, back.WorldBoss.Day);
            Assert.Equal("測試稱號", back.WorldBoss.Title);
        }
    }
}
