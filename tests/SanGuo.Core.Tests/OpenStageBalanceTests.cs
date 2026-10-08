using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    /// <summary>
    /// 素材副本的難度梯度：第 N 階於第 N−1 章通關後解鎖，以 <see cref="CampaignBalanceTests.Growth"/> 的章末養成量測。
    /// 剛解鎖時要打得過、晚一章再來應該穩過（才能放心掃蕩），而停在更早的養成則多半過不了。
    /// 用自動戰鬥量測（人類玩家會比自動打得更好），範圍給寬，只鎖大方向。
    /// </summary>
    public class OpenStageBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public OpenStageBalanceTests(ITestOutputHelper output) { _out = output; }

        /// <summary>第 N 階解鎖時的養成序號（第 1 階在第零章中段解鎖，以第零章末計）。</summary>
        private static int UnlockGrowth(int tier) => tier == 1 ? 0 : tier - 1;

        private double WinRate(int tier, int growth, int runs = 30) =>
            CampaignBalanceTests.WinRate(DemoResourceDungeons.IdOf(tier), CampaignBalanceTests.Growth[growth], runs);

        [Fact]
        public void Report()
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                string line = $"{DemoResourceDungeons.IdOf(tier)} Lv{DemoMeta.DungeonEnemyLevels[tier - 1]}（第 {UnlockGrowth(tier)} 章末解鎖）:";
                for (int g = 0; g < CampaignBalanceTests.Growth.Length; g++) line += $" 第{g}章末 {WinRate(tier, g, 20):F0}%";
                _out.WriteLine(line);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Dungeon_IsClearableOnUnlock_AndSafeOneChapterLater(int tier)
        {
            int g = UnlockGrowth(tier);
            double onUnlock = WinRate(tier, g), later = WinRate(tier, g + 1);
            _out.WriteLine($"res_{tier}: 解鎖時 {onUnlock:F0}%、晚一章 {later:F0}%");
            Assert.True(onUnlock >= 45, $"第 {tier} 階剛解鎖時勝率 {onUnlock:F0}% 太低");
            Assert.True(later >= 85, $"第 {tier} 階晚一章勝率 {later:F0}% 太低，不適合掃蕩");
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Dungeon_NeedsGrowth(int tier)
        {
            double behind = WinRate(tier, UnlockGrowth(tier) - 1), onUnlock = WinRate(tier, UnlockGrowth(tier));
            Assert.True(behind < onUnlock, $"第 {tier} 階：停在上一章養成 {behind:F0}% 應低於解鎖時 {onUnlock:F0}%");
        }
    }
}
