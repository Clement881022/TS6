using System.Collections.Generic;
using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    /// <summary>
    /// 開放編隊的關卡 / 副本（第 9、10 關與資源副本）的難度梯度：
    /// 剛抽完的新手隊伍打不贏、養成後打得贏，才有「抽卡 → 養成 → 變強」的回饋。
    /// 用自動戰鬥量測（人類玩家會比自動打得更好），範圍給寬，只鎖大方向。
    /// </summary>
    public class OpenStageBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public OpenStageBalanceTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Fresh = { "zhangfei", "r_shield", "r_archer", "r_healer" }; // 10 連抽後的典型隊伍：1 UR + 3 R
        private static readonly string[] Strong = { "zhangfei", "guanyu", "r_archer", "liubei" };

        private double WinRate(string stage, string[] heroes, int level, int runs = 100)
        {
            var p = PlayerProfile.CreateNew(0);
            var cells = new[] { (1, 3), (2, 3), (3, 3), (2, 4) };
            var team = new List<FormationEntry>();
            for (int i = 0; i < heroes.Length; i++)
            {
                p.Heroes[heroes[i]] = new HeroState { HeroId = heroes[i], Level = level };
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
            _out.WriteLine($"{stage} Lv{level} {string.Join("+", heroes)}: {rate:F0}%");
            return rate;
        }

        [Fact]
        public void GoldDungeon_IsEasyForEveryone_SoNewPlayersCanFarmGold()
        {
            Assert.True(WinRate("res_gold", Fresh, 1) >= 95);
        }

        // 注意：GDD 04 §5 的戰力門檻曲線（敵人等級隨章節提升）於階段 3 才套用到各關；這裡只鎖「養成有用」的大方向。
        [Fact]
        public void GrowthHelps_OnTheHardestDungeon()
        {
            double lv1 = WinRate("res_card", Strong, 1);
            double lv20 = WinRate("res_card", Strong, 20);
            Assert.True(lv20 > lv1, $"Lv20 {lv20}% 應高於 Lv1 {lv1}%");
            Assert.True(lv20 >= 70);
        }

        [Fact]
        public void StrongTeam_ClearsEveryStage_WhenGrown()
        {
            foreach (var stage in new[] { "res_gold", "res_exp", "res_card", "1-9", "1-10" })
                Assert.True(WinRate(stage, Strong, 20) >= 70, stage);
        }

        [Fact]
        public void ExpDungeon_IsReachableForFreshTeam_SoBooksCanBeFarmed()
        {
            Assert.True(WinRate("res_exp", Fresh, 1) >= 10); // 養成素材的來源：新手隊伍偶爾能贏（人類打得比自動好）
        }
    }
}
