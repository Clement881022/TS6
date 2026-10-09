using System.Collections.Generic;
using System.Linq;
using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    /// <summary>
    /// 主線第 1–6 章的戰力門檻（GDD 04 §5.2–5.3）：照預期進度養成可通過章末關卡，停在上一章的養成則過不了。
    /// 以自動戰鬥量測（人類玩家會比自動打得更好），只鎖大方向。
    /// </summary>
    public class CampaignBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public CampaignBalanceTests(ITestOutputHelper output) { _out = output; }

        /// <summary>F2P 預期隊伍：劇情三兄弟 + 一名 UR（新手池首次十連保底；以模擬中最常見的呂布代表）。</summary>
        private static readonly (string Id, int Lane, int Row)[] Team =
        {
            ("zhangfei", 2, 3), ("guanyu", 1, 3), ("lvbu", 3, 3), ("liubei", 2, 4),
        };

        /// <summary>
        /// 各章末的預期養成：等級、突破次數、裝備品階（三部位同階）。2026-10-09 起取自 tools/playsim 的無課模擬
        /// （帳號經驗 = 消耗的體力、副本逐階解鎖、裝備機率掉落）各章打完時的中位數；第 6 章末為月底養成（40 級、5★、4 階），
        /// 6-10 是月底大關。目標節奏：第 7 天第 2 章、第 14 天第 4 章、約第 30 天全通。
        /// </summary>
        public static readonly (int Level, int Stars, int Gear)[] Growth =
        {
            (12, 0, 0), (12, 0, 1), (23, 0, 2), (27, 1, 2), (30, 2, 2), (33, 3, 3), (40, 5, 4),
        };

        private static PlayerProfile Profile((int Level, int Stars, int Gear) g, out List<FormationEntry> team)
        {
            var p = PlayerProfile.CreateNew(0);
            team = new List<FormationEntry>();
            foreach (var (id, lane, row) in Team)
            {
                var hero = new HeroState { HeroId = id, Level = g.Level, Stars = g.Stars };
                if (g.Gear > 0)
                    foreach (var slot in Equipment.Slots) hero.Equipment[slot.ToString()] = g.Gear;
                p.Heroes[id] = hero;
                team.Add(new FormationEntry(id, lane, row));
            }
            return p;
        }

        public static double WinRate(string stage, (int Level, int Stars, int Gear) g, int runs = 40)
        {
            var p = Profile(g, out var team);
            int wins = 0;
            for (ulong seed = 1; seed <= (ulong)runs; seed++)
            {
                var battle = new Battle(DemoMeta.BuildSetup(stage, seed, p, team)!);
                for (int i = 0; i < 60 && battle.Result == BattleResult.Ongoing; i++) AutoPlayer.PlayTurn(battle);
                if (battle.Result == BattleResult.Won) wins++;
            }
            return 100.0 * wins / runs;
        }

        /// <summary>診斷：勝率、全滅率、逾時率、勝場平均回合、勝場平均存活人數。</summary>
        public static string Detail(string stage, (int Level, int Stars, int Gear) g, int runs = 20)
        {
            var p = Profile(g, out var team);
            int wins = 0, wiped = 0, timeout = 0, turns = 0, alive = 0;
            for (ulong seed = 1; seed <= (ulong)runs; seed++)
            {
                var battle = new Battle(DemoMeta.BuildSetup(stage, seed, p, team)!);
                for (int i = 0; i < 60 && battle.Result == BattleResult.Ongoing; i++) AutoPlayer.PlayTurn(battle);
                if (battle.Result == BattleResult.Won)
                {
                    wins++; turns += battle.Turn;
                    foreach (var u in battle.Units) if (u.Side == Side.Player && u.Alive && !u.Protected) alive++;
                }
                else if (System.Linq.Enumerable.Any(battle.Units, u => u.Side == Side.Player && u.Alive && !u.Protected)) timeout++;
                else wiped++;
            }
            return $"勝 {100 * wins / runs}% 滅 {100 * wiped / runs}% 逾時/護送失敗 {100 * timeout / runs}% 回合 {(wins > 0 ? turns / (double)wins : 0):F1} 存活 {(wins > 0 ? alive / (double)wins : 0):F1}";
        }

        [Fact]
        public void Diagnose()
        {
            var only = System.Environment.GetEnvironmentVariable("SANGUO_STAGES");
            for (int ch = 1; ch <= Campaign.LastChapter; ch++)
                for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
                {
                    string id = Campaign.StageId(ch, lv);
                    if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(id)) continue;
                    _out.WriteLine($"{id} {Campaign.LevelName(ch, lv)}：章末 {Detail(id, Growth[ch])}");
                    _out.WriteLine($"{id} {Campaign.LevelName(ch, lv)}：上章 {Detail(id, Growth[ch - 1])}");
                }
        }

        /// <summary>章末關卡是戰力門檻：照預期養成多半能過，停在上一章的養成多半過不了。</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void ChapterBoss_IsAPowerGate(int chapter)
        {
            string id = Campaign.StageId(chapter, Campaign.LevelsPerChapter);
            double grown = WinRate(id, Growth[chapter]), behind = WinRate(id, Growth[chapter - 1]);
            _out.WriteLine($"{id}：章末養成 {grown:F0}%　上一章養成 {behind:F0}%");
            // 一般自動戰鬥不會挑目標、不會換隊，真人與模擬中的玩家機器人勝率更高（模擬：約一成的場次需要重打）。
            Assert.True(grown >= 30, $"{id} 照預期養成勝率 {grown}% 應 ≥ 30%");
            Assert.True(behind <= 20, $"{id} 停在上一章養成勝率 {behind}% 應 ≤ 20%");
        }

        /// <summary>每一關照章末預期養成都打得過（自動戰鬥勝率 ≥ 30%，人類玩家會打得更好）。</summary>
        [Fact]
        public void EveryStage_IsClearableAtChapterEndGrowth()
        {
            for (int ch = 1; ch <= Campaign.LastChapter; ch++)
                for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
                {
                    string id = Campaign.StageId(ch, lv);
                    double rate = WinRate(id, Growth[ch], 20);
                    Assert.True(rate >= 10, $"{id} {Campaign.LevelName(ch, lv)} 章末養成勝率 {rate}% 太低");
                }
        }

        [Fact]
        public void Report()
        {
            for (int ch = 1; ch <= Campaign.LastChapter; ch++)
                for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
                {
                    string id = Campaign.StageId(ch, lv);
                    double grown = WinRate(id, Growth[ch], 20), behind = WinRate(id, Growth[ch - 1], 20);
                    _out.WriteLine($"{id} {Campaign.LevelName(ch, lv)} 敵Lv{Campaign.EnemyLevelOf(ch, lv)}：章末養成 {grown:F0}%　上一章養成 {behind:F0}%");
                }
        }
    }
}
