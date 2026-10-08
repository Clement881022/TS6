using System.Collections.Generic;
using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    /// <summary>
    /// 開放編隊的關卡 / 素材副本的難度梯度：養成越深越能過，才有「抽卡 → 養成 → 變強」的回饋。
    /// 用自動戰鬥量測（人類玩家會比自動打得更好），範圍給寬，只鎖大方向。完整的戰力門檻曲線於章節內容完成後校準。
    /// </summary>
    public class OpenStageBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public OpenStageBalanceTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Team = { "zhangfei", "guanyu", "r_archer", "liubei" };

        private double WinRate(string stage, string[] heroes, int level, int stars = 0, int runs = 60)
        {
            var p = PlayerProfile.CreateNew(0);
            var cells = new[] { (1, 3), (2, 3), (3, 3), (2, 4) };
            var team = new List<FormationEntry>();
            for (int i = 0; i < heroes.Length; i++)
            {
                p.Heroes[heroes[i]] = new HeroState { HeroId = heroes[i], Level = level, Stars = stars };
                team.Add(new FormationEntry(heroes[i], cells[i].Item1, cells[i].Item2));
            }
            int wins = 0;
            for (ulong seed = 1; seed <= (ulong)runs; seed++)
            {
                var battle = new Battle(DemoMeta.BuildSetup(stage, seed, p, team)!);
                for (int i = 0; i < 60 && battle.Result == BattleResult.Ongoing; i++) AutoPlayer.PlayTurn(battle);
                if (battle.Result == BattleResult.Won) wins++;
            }
            double rate = 100.0 * wins / runs;
            _out.WriteLine($"{stage} Lv{level} ★{stars}: {rate:F0}%");
            return rate;
        }

        [Fact]
        public void Report()
        {
            foreach (var stage in new[] { "res_1", "res_2", "res_3", "res_4", "res_5" })
                foreach (var (lv, st) in new[] { (1, 0), (10, 0), (20, 2), (30, 3), (40, 5) })
                    WinRate(stage, Team, lv, st, 30);
        }

        [Fact]
        public void GrowthHelps_OnTheThirdDungeon()
        {
            double fresh = WinRate("res_3", Team, 1);
            double grown = WinRate("res_3", Team, 40, 5);
            Assert.True(grown > fresh, $"養成後 {grown}% 應高於新手 {fresh}%");
        }

        [Fact]
        public void FirstDungeon_IsClearableByAMidLevelTeam()
        {
            Assert.True(WinRate("res_1", Team, 20, 2) >= 70);
        }
    }
}
