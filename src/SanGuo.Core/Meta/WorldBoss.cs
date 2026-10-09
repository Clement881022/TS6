using System;
using System.Collections.Generic;
using SanGuo.Core.Content;

namespace SanGuo.Core.Meta
{
    public sealed class WorldBossState
    {
        public string Season = "";
        public long Best;
        public long Day = long.MinValue;
        public int Used;
        public string FightSeason = "";
        public string PendingSeason = "";
        public long PendingBest;
        public string LastSeason = "";
        public int LastRank;
        public int LastTotal;
        public int LastReward;
        public string Title = "";
    }

    public enum WorldBossEntry { Ok, Locked, NoAttempts }

    public static class WorldBoss
    {
        public const string StageId = "world_boss";
        public const int DailyAttempts = 3;
        public const int TurnLimit = 10;
        public const string UnlockStage = "2-10";
        public const int BossLevel = 40;
        public const int HpMultiplier = 30;

        public const double BurnResist = 0.0;

        public static ulong DailySeed(long now)
        {
            ulong x = (ulong)DailyClock.DayIndex(now) * 0x9E3779B97F4A7C15UL + 0x5EED_B055UL;
            x ^= x >> 31; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 29;
            return x & 0x7FFFFFFFFFFFFFFF;
        }

        private static readonly Func<EnemyDef>[] Rotation =
        {
            Enemies.DongZhuo, Enemies.LvBu, Enemies.HuaXiong, Enemies.ZhangJiao, Enemies.LiRu, Enemies.BoCai,
        };

        public static string SeasonOf(long now) => DailyClock.MonthKey(now);

        public static string SeasonName(string season) =>
            season.Length == 7 ? $"{season.Substring(0, 4)} 年 {int.Parse(season.Substring(5))} 月" : season;

        public static EnemyDef BossOf(string season)
        {
            int index = 0;
            if (season.Length == 7 && int.TryParse(season.Substring(0, 4), out int y) && int.TryParse(season.Substring(5), out int m))
                index = (y * 12 + m - 1) % Rotation.Length;
            var def = Rotation[index]();
            def.Base.Hp *= HpMultiplier;
            def.BurnResist = BurnResist;
            return def;
        }

        public static BattleSetup Setup(string season, ulong seed)
        {
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true, TurnLimit = TurnLimit, Objective = Objective.KillTarget };
            setup.Enemies.Add(new EnemySlot(BossOf(season), new Position(2, 1), BossLevel) { IsObjective = true });
            return setup;
        }

        public static long Score(Battle battle)
        {
            foreach (var u in battle.Units)
                if (u.Side == Side.Enemy && u.IsObjective) return u.Stats.Hp - Math.Max(0, u.Hp);
            return 0;
        }

        public static bool IsUnlocked(PlayerProfile p) => p.ClearedStages.Contains(UnlockStage);

        public static void Roll(PlayerProfile p, long now)
        {
            var s = p.WorldBoss;
            string season = SeasonOf(now);
            if (s.Season != season)
            {
                if (s.Season != "" && s.Best > 0 && s.PendingSeason == "")
                {
                    s.PendingSeason = s.Season;
                    s.PendingBest = s.Best;
                }
                s.Season = season;
                s.Best = 0;
            }
            long day = DailyClock.DayIndex(now);
            if (s.Day != day)
            {
                s.Day = day;
                s.Used = 0;
            }
        }

        public static int AttemptsLeft(PlayerProfile p, long now)
        {
            Roll(p, now);
            return Math.Max(0, DailyAttempts - p.WorldBoss.Used);
        }

        public static WorldBossEntry TryEnter(PlayerProfile p, long now)
        {
            if (!IsUnlocked(p)) return WorldBossEntry.Locked;
            if (AttemptsLeft(p, now) <= 0) return WorldBossEntry.NoAttempts;
            p.WorldBoss.Used++;
            p.WorldBoss.FightSeason = p.WorldBoss.Season;
            return WorldBossEntry.Ok;
        }

        public static bool Record(PlayerProfile p, long damage)
        {
            if (damage <= p.WorldBoss.Best) return false;
            p.WorldBoss.Best = damage;
            return true;
        }

        public static (int Yuanbao, string Title) RewardFor(int rank, int total, string season)
        {
            if (rank <= 0 || total <= 0) return (0, "");
            double pct = (double)rank / total;
            int yuanbao = pct <= 0.01 || rank == 1 ? 3000 : pct <= 0.10 ? 2000 : pct <= 0.30 ? 1500 : pct <= 0.60 ? 1000 : 600;
            string title = rank <= 10 ? $"{SeasonName(season)}　群雄榜第 {rank} 名" : "";
            return (yuanbao, title);
        }

        public static void SettlePending(PlayerProfile p, IWorldBossBoard board, long now)
        {
            Roll(p, now);
            var s = p.WorldBoss;
            if (s.PendingSeason == "") return;
            var (rank, total) = board.RankOf(s.PendingSeason, s.PendingBest);
            Settle(p, rank, total);
        }

        public static void Settle(PlayerProfile p, int rank, int total)
        {
            var s = p.WorldBoss;
            if (s.PendingSeason == "") return;
            var (yuanbao, title) = RewardFor(rank, total, s.PendingSeason);
            p.Yuanbao += yuanbao;
            if (title != "") s.Title = title;
            s.LastSeason = s.PendingSeason;
            s.LastRank = rank;
            s.LastTotal = total;
            s.LastReward = yuanbao;
            s.PendingSeason = "";
            s.PendingBest = 0;
        }
    }

    public interface IWorldBossBoard
    {
        void Submit(string season, string accountId, long best);
        (int Rank, int Total) RankOf(string season, long score);
        List<(string AccountId, long Best)> Top(string season, int count);
    }

    public sealed class InMemoryWorldBossBoard : IWorldBossBoard
    {
        private readonly Dictionary<string, Dictionary<string, long>> _seasons = new Dictionary<string, Dictionary<string, long>>();

        public void Submit(string season, string accountId, long best)
        {
            lock (_seasons)
            {
                if (!_seasons.TryGetValue(season, out var board)) _seasons[season] = board = new Dictionary<string, long>();
                if (!board.TryGetValue(accountId, out long old) || best > old) board[accountId] = best;
            }
        }

        public (int Rank, int Total) RankOf(string season, long score)
        {
            lock (_seasons)
            {
                if (!_seasons.TryGetValue(season, out var board) || board.Count == 0) return (1, 1);
                int higher = 0;
                foreach (var v in board.Values) if (v > score) higher++;
                return (higher + 1, board.Count);
            }
        }

        public List<(string AccountId, long Best)> Top(string season, int count)
        {
            var list = new List<(string, long)>();
            lock (_seasons)
            {
                if (_seasons.TryGetValue(season, out var board))
                    foreach (var kv in board) list.Add((kv.Key, kv.Value));
            }
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }
    }
}
