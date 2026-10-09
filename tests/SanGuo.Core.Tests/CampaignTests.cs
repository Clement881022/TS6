using System.Collections.Generic;
using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class CampaignTests
    {
        [Fact]
        public void EveryStage_BuildsAndHasAUniqueParsableId()
        {
            var ids = new HashSet<string>();
            for (int ch = Campaign.FirstChapter; ch <= Campaign.LastChapter; ch++)
            {
                Assert.Equal(Campaign.LevelsPerChapter, Campaign.LevelNames(ch).Length);
                for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
                {
                    string id = Campaign.StageId(ch, lv);
                    Assert.True(ids.Add(id));
                    Assert.True(Campaign.TryParse(id, out int c, out int l));
                    Assert.Equal((ch, lv), (c, l));
                    Assert.NotNull(DemoMeta.FindStage(id));
                    Assert.True(Campaign.TurnPar(ch, lv) > 0);

                    var setup = Campaign.Setup(ch, lv);
                    Assert.NotEmpty(setup.Enemies);
                    Assert.All(setup.Enemies, e => Assert.False(FormationRules.InFormationZone(e.Pos.Lane, e.Pos.Row), $"{id} 敵人站在入場區"));
                    if (ch > 0)
                    {
                        Assert.All(setup.Heroes, h => Assert.True(h.IsProtected));
                        Assert.Equal(setup.Objective == Objective.Escort || setup.Objective == Objective.Defend, setup.Heroes.Count == 1);
                        if (setup.Objective == Objective.KillTarget) Assert.Contains(setup.Enemies, e => e.IsObjective);
                    }
                }
            }
            Assert.Equal(70, ids.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void WrittenChapters_HaveStoryForEveryStage(int chapter)
        {
            for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
            {
                var before = CampaignStory.Before(chapter, lv);
                var after = CampaignStory.After(chapter, lv);
                Assert.NotEmpty(before);
                Assert.NotEmpty(after);
                Assert.All(before.Concat(after), line => Assert.False(string.IsNullOrWhiteSpace(line.Text)));
            }
        }

        [Fact]
        public void StoryHeroes_GetOneDuplicatePerChapterEnd_FromChapter2()
        {
            var p = Meta.PlayerProfile.CreateNew(0);
            for (int c = Campaign.FirstChapter; c <= Campaign.LastChapter; c++)
                for (int l = 1; l <= Campaign.LevelsPerChapter; l++)
                {
                    var clear = p.ClaimClear(Meta.DemoMeta.Stage(c, l), 0, 3);
                    bool expected = c >= 2 && l == Campaign.LevelsPerChapter;
                    Assert.Equal(expected ? Meta.DemoMeta.StoryHeroes.Length : 0, clear.DuplicatesGained.Count);
                }
            foreach (var id in Meta.DemoMeta.StoryHeroes) Assert.Equal(Meta.HeroGrowth.MaxStars, Meta.HeroGrowth.Shards(p, id));
            Assert.Empty(p.ClaimClear(Meta.DemoMeta.Stage(2, 10), 0, 3).DuplicatesGained);
        }

        [Theory]
        [InlineData("1-0")]
        [InlineData("7-1")]
        [InlineData("1-11")]
        [InlineData("01-1")]
        [InlineData("res_1")]
        [InlineData("")]
        public void TryParse_RejectsNonCampaignIds(string id) => Assert.False(Campaign.TryParse(id, out _, out _));

        [Fact]
        public void Unlock_FollowsThePreviousStageAcrossChapters()
        {
            var cleared = new HashSet<string>();
            Assert.True(Campaign.IsUnlocked(cleared, 0, 1));
            Assert.False(Campaign.IsUnlocked(cleared, 1, 1));
            cleared.Add("0-10");
            Assert.True(Campaign.IsUnlocked(cleared, 1, 1));
            Assert.False(Campaign.IsUnlocked(cleared, 1, 2));

            Assert.True(Campaign.Next(0, 10, out int nc, out int nl));
            Assert.Equal((1, 1), (nc, nl));
            Assert.False(Campaign.Next(6, 10, out _, out _));
            Assert.Equal((0, 1), Campaign.Frontier(new HashSet<string>()));
            Assert.Equal((1, 1), Campaign.Frontier(new HashSet<string>(Enumerable.Range(1, 10).Select(l => $"0-{l}"))));
        }

        [Fact]
        public void EnemyLevels_RiseToEachChapterEnd()
        {
            for (int ch = 1; ch <= Campaign.LastChapter; ch++)
            {
                Assert.Equal(Campaign.ChapterEndLevel[ch], Campaign.EnemyLevelOf(ch, Campaign.LevelsPerChapter));
                for (int lv = 2; lv <= Campaign.LevelsPerChapter; lv++)
                    Assert.True(Campaign.EnemyLevelOf(ch, lv) >= Campaign.EnemyLevelOf(ch, lv - 1));
            }
        }

        [Fact]
        public void ProtectedNpc_StaysWhenTheFormationIsApplied()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Heroes["zhangfei"] = new HeroState { HeroId = "zhangfei" };
            var setup = DemoMeta.BuildSetup("1-3", 1, p, new List<FormationEntry> { new FormationEntry("zhangfei", 2, 3) })!;
            Assert.Equal(2, setup.Heroes.Count);
            Assert.True(setup.Heroes[0].IsProtected);
            Assert.Equal("zhangfei", setup.Heroes[1].Def.Id);
        }

        [Fact]
        public void Boss_EntersPhaseTwoOnceBelowHalfHp()
        {
            int switched = 0;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var p = PlayerProfile.CreateNew(0);
                var team = new List<FormationEntry>();
                foreach (var (id, lane, row) in new[] { ("zhangfei", 2, 3), ("guanyu", 1, 3), ("handang", 3, 4), ("liubei", 2, 4) })
                {
                    p.Heroes[id] = new HeroState { HeroId = id, Level = 25, Stars = 1 };
                    team.Add(new FormationEntry(id, lane, row));
                }
                var battle = new Battle(DemoMeta.BuildSetup("1-10", seed, p, team)!);
                var boss = battle.Units.Single(u => u.Tier == EnemyTier.Boss);
                Assert.Equal(1, boss.Phase);
                AutoPlayer.RunToEnd(battle);

                var phases = battle.Events.Where(e => e.Type == EventType.EnemyPhase && e.Source == boss.Id).ToList();
                Assert.True(phases.Count <= 1);
                if (phases.Count == 1)
                {
                    switched++;
                    Assert.Equal(2, boss.Phase);
                    Assert.Equal(boss.Enemy!.Phase2ChargeInterval, boss.ChargeInterval);
                    Assert.Equal(boss.Enemy.Phase2ChargePower, boss.ChargePower);
                }
            }
            Assert.True(switched > 0, "應至少有一場打到 Boss 半血以下");
        }

        [Fact]
        public void Version2Save_MigratesChapterZeroIds()
        {
            var root = (Dictionary<string, object?>)MiniJson.Parse(ProfileSerializer.ToJson(PlayerProfile.CreateNew(0)))!;
            root["version"] = 2L;
            root["clearedStages"] = new List<object?> { "1-1", "1-2", "1-3", "res_1" };
            root["stageStars"] = new Dictionary<string, object?> { ["1-3"] = 3L };
            root["pendingStageId"] = "1-4";

            var p = ProfileSerializer.FromObject(root);
            Assert.Equal(new HashSet<string> { "0-1", "0-2", "0-3", "res_1" }, p.ClearedStages);
            Assert.Equal(3, p.StageStars["0-3"]);
            Assert.False(p.StageStars.ContainsKey("1-3"));
            Assert.Equal("0-4", p.PendingStageId);

            var fresh = PlayerProfile.CreateNew(0);
            fresh.ClearedStages.Add("1-1");
            Assert.Contains("1-1", ProfileSerializer.FromJson(ProfileSerializer.ToJson(fresh)).ClearedStages);
        }
    }
}
