using System.Collections.Generic;
using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class HardStageTests
    {
        [Fact]
        public void StageIds_RoundTrip_AndDoNotCollideWithNormalStages()
        {
            Assert.Equal("H2-7", HardStages.StageId(2, 7));
            Assert.True(HardStages.TryParse("H2-7", out int c, out int l));
            Assert.Equal((2, 7), (c, l));
            Assert.False(HardStages.TryParse("2-7", out _, out _));
            Assert.False(Campaign.TryParse("H2-7", out _, out _));
            Assert.False(HardStages.TryParse("H0-3", out _, out _));
            Assert.Equal("H2-7", DemoMeta.FindStage("H2-7")!.StageId);
        }

        [Fact]
        public void Unlock_NeedsMainCampaignClear_AndPreviousHardClear()
        {
            var cleared = new HashSet<string> { "1-1", "1-2" };
            Assert.False(HardStages.IsUnlocked(cleared, 1, 1));
            cleared.Add(HardStages.UnlockStage);
            Assert.True(HardStages.IsUnlocked(cleared, 1, 1));
            Assert.False(HardStages.IsUnlocked(cleared, 1, 2));
            cleared.Add("H1-1");
            Assert.True(HardStages.IsUnlocked(cleared, 1, 2));
        }

        [Fact]
        public void HardEnemies_AreStrongerThanNormal_AndConditionsApply()
        {
            var normal = DemoMeta.OpenLevel(1, 3, 1);
            var hard = HardStages.Setup(1, 3, 1);
            Assert.Equal(normal.Enemies.Count, hard.Enemies.Count);
            Assert.All(hard.Enemies.Zip(normal.Enemies, (h, n) => h.Level - n.Level), d => Assert.True(d > 0));
            Assert.True(hard.TurnLimit > 0);
            Assert.NotNull(HardStages.BannedRole(2, 6));
            Assert.Null(HardStages.BannedRole(2, 5));
            Assert.True(HardStages.EnemyLevelOf(6, 10) > HardStages.EnemyLevelOf(1, 1));
        }

        [Fact]
        public void BannedRole_RejectsTheFormation()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Heroes["liubei"] = new HeroState { HeroId = "liubei" };
            p.Heroes["zhangfei"] = new HeroState { HeroId = "zhangfei" };
            p.ClearedStages.UnionWith(new[] { HardStages.UnlockStage, "H1-5" });
            var banned = HardStages.BannedRole(1, 6);
            Assert.Equal(Role.Healer, banned);
            var withHealer = new List<FormationEntry> { new FormationEntry("liubei", 2, 4), new FormationEntry("zhangfei", 2, 3) };
            Assert.Equal("banned_role", StageFlow.Start(p, "H1-6", 0, 1, withHealer).Code);
            var ok = StageFlow.Start(p, "H1-6", 0, 1, new List<FormationEntry> { new FormationEntry("zhangfei", 2, 3) });
            Assert.True(ok.Ok);
        }

        [Fact]
        public void Locked_HardStage_CannotStart()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Heroes["zhangfei"] = new HeroState { HeroId = "zhangfei" };
            var team = new List<FormationEntry> { new FormationEntry("zhangfei", 2, 3) };
            Assert.Equal("hard_locked", StageFlow.Start(p, "H1-1", 0, 1, team).Code);
        }

        [Fact]
        public void Rewards_GiveSoulsOnEveryClear_AndExtraOnFirstClear()
        {
            var p = PlayerProfile.CreateNew(0);
            var stage = HardStages.Stage(1, 1);
            p.ClaimClear(stage, 0, 3);
            Assert.Equal(HardStages.FirstClearSouls + HardStages.RepeatSouls, p.GetMaterial(HeroGrowth.Soul));
            Assert.Equal(stage.FirstClearYuanbao, p.Yuanbao);
            p.Stamina.Add(100, 0);
            Assert.Equal(SweepResult.Ok, p.TrySweep(stage, 2, 0, out _));
            Assert.Equal(HardStages.FirstClearSouls + HardStages.RepeatSouls * 3, p.GetMaterial(HeroGrowth.Soul));
        }
    }
}
