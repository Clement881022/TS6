using System;
using System.Collections.Generic;
using SanGuo.Core.Content;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家的世界 Boss 進度（存檔的一部分）。</summary>
    public sealed class WorldBossState
    {
        /// <summary>目前賽季（<see cref="DailyClock.MonthKey"/>）與本季單場最高傷害。</summary>
        public string Season = "";
        public long Best;
        /// <summary>今天（<see cref="DailyClock.DayIndex"/>）已用的挑戰次數。</summary>
        public long Day = long.MinValue;
        public int Used;
        /// <summary>進行中那一場開打時的賽季：跨月才結算時，用它重建同一隻 Boss，成績也不算進新賽季。</summary>
        public string FightSeason = "";
        /// <summary>換季時尚未結算的上一季成績（伺服器查排名後由 <see cref="WorldBoss.Settle"/> 發獎並清空）。</summary>
        public string PendingSeason = "";
        public long PendingBest;
        /// <summary>最近一次結算的結果（顯示用）。</summary>
        public string LastSeason = "";
        public int LastRank;
        public int LastTotal;
        public int LastReward;
        /// <summary>最近一次賽季取得的稱號（前 10 名；沒有則空字串）。</summary>
        public string Title = "";
    }

    public enum WorldBossEntry { Ok, Locked, NoAttempts }

    /// <summary>
    /// 世界 Boss（GDD 05 §8）：1.0 的終局 PvE，以單場最高傷害排名。
    /// 已定案（2026-10-09）：每天 3 次、不扣體力；排名看本季單場最高傷害；一季（一個月）一隻 Boss，用章末 Boss 的強化版。
    /// 暫定（待確認）：第 2 章通關後開放、10 回合、Boss 40 級生命 ×6（1.0 畢業隊伍自動戰鬥約打掉兩到三成）、輪替順序與賽季獎勵區間（見 <see cref="RewardFor"/>）。
    /// 戰鬥由玩家編隊，開打與結算沿用 <see cref="StageFlow"/>（伺服器重播驗證，分數由伺服器算）。
    /// </summary>
    public static class WorldBoss
    {
        public const string StageId = "world_boss";
        public const int DailyAttempts = 3;
        public const int TurnLimit = 10;
        public const string UnlockStage = "2-10";
        public const int BossLevel = 40;
        /// <summary>
        /// Boss 生命倍率。2026-10-09 由 ×6 提高到 ×30：模擬發現月底的術士隊能直接打死 ×6 的 Boss，頂端分數封頂同分，
        /// 課金與配隊差距都量不出來；改成實際上打不死，10 回合內打越多越高分。
        /// </summary>
        public const int HpMultiplier = 30;

        /// <summary>
        /// 每日固定種子（2026-10-09）：同一天所有玩家打的 Boss 牌序與亂數都相同，比的是隊伍與打法而不是運氣；
        /// 伺服器開打時用它取代隨機種子。
        /// </summary>
        /// <summary>
        /// 世界 Boss 的燃燒抗性（企劃 2026-10-09：燃燒打 Boss 強是好的，先不壓；機制先做好，之後用它限制燃燒效率）。
        /// </summary>
        public const double BurnResist = 0.0;

        public static ulong DailySeed(long now)
        {
            ulong x = (ulong)DailyClock.DayIndex(now) * 0x9E3779B97F4A7C15UL + 0x5EED_B055UL;
            x ^= x >> 31; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 29;
            return x & 0x7FFFFFFFFFFFFFFF;
        }

        /// <summary>Boss 輪替（依賽季月份循環）：章末 Boss 的強化版。</summary>
        private static readonly Func<EnemyDef>[] Rotation =
        {
            Enemies.DongZhuo, Enemies.LvBu, Enemies.HuaXiong, Enemies.ZhangJiao, Enemies.LiRu, Enemies.BoCai,
        };

        public static string SeasonOf(long now) => DailyClock.MonthKey(now);

        /// <summary>賽季顯示名稱：「2026 年 10 月」。</summary>
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

        /// <summary>世界 Boss 的戰鬥設定（編隊前的樣子；玩家編隊在 <see cref="FormationRules.Apply"/> 套入）。擊倒 Boss 即勝利，否則打滿回合結束。</summary>
        public static BattleSetup Setup(string season, ulong seed)
        {
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true, TurnLimit = TurnLimit, Objective = Objective.KillTarget };
            setup.Enemies.Add(new EnemySlot(BossOf(season), new Position(2, 1), BossLevel) { IsObjective = true });
            return setup;
        }

        /// <summary>這場對 Boss 造成的傷害（護盾不算）。</summary>
        public static long Score(Battle battle)
        {
            foreach (var u in battle.Units)
                if (u.Side == Side.Enemy && u.IsObjective) return u.Stats.Hp - Math.Max(0, u.Hp);
            return 0;
        }

        public static bool IsUnlocked(PlayerProfile p) => p.ClearedStages.Contains(UnlockStage);

        /// <summary>換季與換日：上一季有成績就留待結算；每天重置挑戰次數。</summary>
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

        /// <summary>開打：檢查開放與次數，成功就用掉一次。</summary>
        public static WorldBossEntry TryEnter(PlayerProfile p, long now)
        {
            if (!IsUnlocked(p)) return WorldBossEntry.Locked;
            if (AttemptsLeft(p, now) <= 0) return WorldBossEntry.NoAttempts;
            p.WorldBoss.Used++;
            p.WorldBoss.FightSeason = p.WorldBoss.Season;
            return WorldBossEntry.Ok;
        }

        /// <summary>記錄一場成績；回傳是否刷新本季最佳。</summary>
        public static bool Record(PlayerProfile p, long damage)
        {
            if (damage <= p.WorldBoss.Best) return false;
            p.WorldBoss.Best = damage;
            return true;
        }

        /// <summary>
        /// 賽季獎勵（暫定）：依排名百分位給元寶，前 10 名另給稱號。
        /// 前 1% 3000、前 10% 2000、前 30% 1500、前 60% 1000，其餘有出戰的 600。
        /// </summary>
        public static (int Yuanbao, string Title) RewardFor(int rank, int total, string season)
        {
            if (rank <= 0 || total <= 0) return (0, "");
            double pct = (double)rank / total;
            int yuanbao = pct <= 0.01 || rank == 1 ? 3000 : pct <= 0.10 ? 2000 : pct <= 0.30 ? 1500 : pct <= 0.60 ? 1000 : 600;
            string title = rank <= 10 ? $"{SeasonName(season)}　群雄榜第 {rank} 名" : "";
            return (yuanbao, title);
        }

        /// <summary>換季後第一次存取時呼叫：有待結算的上一季就查排名並發獎。</summary>
        public static void SettlePending(PlayerProfile p, IWorldBossBoard board, long now)
        {
            Roll(p, now);
            var s = p.WorldBoss;
            if (s.PendingSeason == "") return;
            var (rank, total) = board.RankOf(s.PendingSeason, s.PendingBest);
            Settle(p, rank, total);
        }

        /// <summary>結算上一季（呼叫端查好排名）：發元寶、記稱號與結果。</summary>
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

    /// <summary>排行榜（伺服器以資料庫實作；單機版只有自己一人）。</summary>
    public interface IWorldBossBoard
    {
        /// <summary>寫入本季最佳（較低的分數不覆蓋）。</summary>
        void Submit(string season, string accountId, long best);
        /// <summary>分數在該季的名次（同分同名次，1 起算）與參賽人數。</summary>
        (int Rank, int Total) RankOf(string season, long score);
        List<(string AccountId, long Best)> Top(string season, int count);
    }

    /// <summary>記憶體排行榜（測試、單機版）。</summary>
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
