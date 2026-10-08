using System.Collections.Generic;
using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>第零章（盜匪教學）：首通發將、敵人已換成盜匪、劇情資料完整。</summary>
    public class Chapter0Tests
    {
        private static ReplayRecorder Play(BattleSetup setup)
        {
            var rec = new ReplayRecorder(new Battle(setup));
            for (int i = 0; i < 100 && rec.Battle.Result == BattleResult.Ongoing; i++) rec.PlayAuto();
            return rec;
        }

        [Fact]
        public void FirstClearOfStages1To3_GrantsLiuBeiZhangFeiGuanYu_InOrder()
        {
            Assert.Equal("liubei", DemoMeta.Stage(0, 1).FirstClearHero);
            Assert.Equal("zhangfei", DemoMeta.Stage(0, 2).FirstClearHero);
            Assert.Equal("guanyu", DemoMeta.Stage(0, 3).FirstClearHero);
            for (int level = 4; level <= DemoContent.ChapterLevelCount; level++)
                Assert.Equal("", DemoMeta.Stage(0, level).FirstClearHero);
        }

        [Fact]
        public void ClearingStage1_GrantsLiuBeiOnce_AndRepeatClearTurnsHimIntoShards()
        {
            long now = 1_700_000_000;
            var p = PlayerProfile.CreateNew(now);
            var stage = DemoMeta.Stage(0, 1);
            Assert.DoesNotContain("liubei", p.Heroes.Keys);

            var first = p.ClaimClear(stage, now, 1);
            Assert.True(first.FirstClear);
            Assert.Equal("liubei", first.HeroGained);
            Assert.Contains("liubei", p.Heroes.Keys);

            var again = p.ClaimClear(stage, now, 1);
            Assert.False(again.FirstClear);
            Assert.Equal("", again.HeroGained);
        }

        [Fact]
        public void StageFlow_ReportsGrantedHeroOnFirstClear()
        {
            long now = 1_700_000_000;
            var p = PlayerProfile.CreateNew(now);
            p.Level = 3;
            var start = StageFlow.Start(p, "0-1", now, 11);
            Assert.True(start.Ok);
            var rec = Play(DemoMeta.BuildSetup("0-1", start.Seed, p)!);
            var done = StageFlow.Finish(p, "0-1", rec.Actions, now);
            Assert.True(done.Ok && done.Won);
            Assert.Equal("liubei", done.HeroGained);
            Assert.Contains("liubei", p.Heroes.Keys);
        }

        [Fact]
        public void TutorialEnemies_AreBanditsNotYellowTurbans()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                var setup = DemoContent.Level(level);
                Assert.All(setup.Enemies, e => Assert.StartsWith("bandit_", e.Def.Id));
            }
            foreach (var id in new[] { "res_gold", "res_exp", "res_card" })
                Assert.All(DemoMeta.DungeonSetup(id, 1).Enemies, e => Assert.StartsWith("bandit_", e.Def.Id));
        }

        [Fact]
        public void EveryLevel_HasStoryBeforeAndAfter_WithNamedSpeakers()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                foreach (var scene in new[] { DemoStory.Before(level), DemoStory.After(level) })
                {
                    Assert.NotEmpty(scene);
                    Assert.All(scene, l => Assert.False(string.IsNullOrWhiteSpace(l.Text)));
                    Assert.All(scene, l => Assert.True(l.Portrait == "" || l.Speaker != ""));
                }
            }
        }

        [Fact]
        public void StoryPortraits_AreKnownCharacters()
        {
            var known = new HashSet<string>(DemoContent.Roster().Select(h => h.Id)) { "r_villager" };
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                foreach (var l in DemoStory.Before(level).Concat(DemoStory.After(level)).Where(l => l.Portrait != ""))
                    Assert.Contains(l.Portrait, known);
        }

        [Fact]
        public void Intro_MeetsLiuBeiBeforeAnyBattle_AndTheMascotNeverNarratesTheStory()
        {
            var intro = DemoStory.Intro();
            Assert.NotEmpty(intro);
            Assert.Contains(intro, l => l.Speaker == "劉備");
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                Assert.DoesNotContain(intro.Concat(DemoStory.Before(level)).Concat(DemoStory.After(level)),
                    l => l.Speaker == "巴豆妖" || l.Portrait == "badou");
        }

        [Fact]
        public void PartyOfThree_DrivesTheStory_MoreThanGuestsCombined()
        {
            var all = new List<StoryLine>(DemoStory.Intro());
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                all.AddRange(DemoStory.Before(level));
                all.AddRange(DemoStory.After(level));
            }
            int Count(params string[] who) => all.Count(l => who.Contains(l.Speaker));
            int party = Count("劉備", "關羽", "張飛");
            int guests = Count("張世平");
            Assert.True(party > guests * 3, $"主角團 {party} 句、客串 {guests} 句");
            Assert.True(Count("劉備") >= 20 && Count("關羽") >= 12 && Count("張飛") >= 12);
            Assert.True(Count(DemoStory.Protagonist) >= 40);
        }

        [Fact]
        public void HeroesJoinStoryAfterTheirStages()
        {
            Assert.Contains(DemoStory.After(1), l => l.Text.Contains("劉備加入"));
            Assert.Contains(DemoStory.After(2), l => l.Text.Contains("張飛加入"));
            Assert.Contains(DemoStory.After(3), l => l.Text.Contains("關羽加入"));
        }
    }
}
