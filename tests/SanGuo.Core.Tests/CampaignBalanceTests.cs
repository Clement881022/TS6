using System.Collections.Generic;
using System.Linq;
using SanGuo.Core.Meta;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    public class CampaignBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public CampaignBalanceTests(ITestOutputHelper output) { _out = output; }

        private static readonly (string Id, int Lane, int Row)[] Team =
        {
            ("zhangfei", 2, 3), ("guanyu", 1, 3), ("lvbu", 3, 3), ("liubei", 2, 4),
        };

        public static readonly (int Level, int Stars, double Gear)[] Growth =
        {
            (3, 0, 0), (20, 0, 1.5), (28, 1, 2.0), (34, 1, 2.3), (36, 2, 2.7), (38, 3, 3.0), (42, 5, 4.0),
        };

        public static void SetGear(HeroState hero, double avg)
        {
            int baseTier = (int)System.Math.Floor(avg), higher = (int)System.Math.Round((avg - baseTier) * 3);
            for (int i = 0; i < Equipment.Slots.Length; i++)
            {
                int tier = baseTier + (i < higher ? 1 : 0);
                if (tier > 0) hero.Equipment[Equipment.Slots[i].ToString()] = System.Math.Min(tier, Equipment.MaxTier);
            }
        }

        private static PlayerProfile Profile((int Level, int Stars, double Gear) g, out List<FormationEntry> team)
        {
            var p = PlayerProfile.CreateNew(0);
            team = new List<FormationEntry>();
            foreach (var (id, lane, row) in Team)
            {
                var hero = new HeroState { HeroId = id, Level = g.Level, Stars = g.Stars };
                SetGear(hero, g.Gear);
                p.Heroes[id] = hero;
                team.Add(new FormationEntry(id, lane, row));
            }
            return p;
        }

        public static double WinRate(string stage, (int Level, int Stars, double Gear) g, int runs = 40)
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

        public static string Detail(string stage, (int Level, int Stars, double Gear) g, int runs = 20)
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
            Assert.True(grown >= 30, $"{id} 照預期養成勝率 {grown}% 應 ≥ 30%");
            Assert.True(behind <= 20, $"{id} 停在上一章養成勝率 {behind}% 應 ≤ 20%");
        }

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
