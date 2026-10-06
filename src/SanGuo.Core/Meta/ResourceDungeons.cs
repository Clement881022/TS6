using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public enum DungeonEntryResult
    {
        Ok,
        NotOpenToday,
        LimitReached,
        LevelTooLow,
        NotEnoughStamina,
        InvalidCount,
        NotCleared,
    }

    /// <summary>資源副本：每日輪替主題、每天固定次數，掉落養成素材（見 docs/progression.md 3）。</summary>
    public sealed class ResourceDungeonDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>開放的星期（週一 = 0 … 週日 = 6）。</summary>
        public List<int> Weekdays = new List<int>();
        public int StaminaCost = 10;
        public int DailyLimit = 3;
        public int MinPlayerLevel = 1;
        public Reward Reward = new Reward();

        public bool IsOpen(long now) => Weekdays.Contains(DailyClock.Weekday(now));
    }

    public static class ResourceDungeons
    {
        public const int MaxSweepCount = 3;

        /// <summary>今天還剩幾次。</summary>
        public static int Remaining(PlayerProfile p, ResourceDungeonDef d, long now)
        {
            p.EnsureDaily(now);
            p.DailyCounters.TryGetValue(d.Id, out int used);
            return d.DailyLimit - used;
        }

        /// <summary>開打前檢查：今日開放、次數、等級、體力；成功才扣體力並計一次。</summary>
        public static DungeonEntryResult TryEnter(PlayerProfile p, ResourceDungeonDef d, long now) =>
            Consume(p, d, 1, now);

        /// <summary>戰鬥勝利：發獎勵，並記為已通關（之後可掃蕩）。</summary>
        public static void ClaimWin(PlayerProfile p, ResourceDungeonDef d, long now)
        {
            p.Grant(d.Reward, now);
            p.ClearedStages.Add(d.Id);
            Quests.Report(p, Quests.Events.ResourceRun, 1, now);
        }

        /// <summary>掃蕩：通關過的副本直接領獎勵，一樣吃每日次數與體力。</summary>
        public static DungeonEntryResult TrySweep(PlayerProfile p, ResourceDungeonDef d, int count, long now)
        {
            if (count < 1 || count > MaxSweepCount) return DungeonEntryResult.InvalidCount;
            if (!p.ClearedStages.Contains(d.Id)) return DungeonEntryResult.NotCleared;
            var r = Consume(p, d, count, now);
            if (r != DungeonEntryResult.Ok) return r;
            p.Grant(d.Reward.Times(count), now);
            Quests.Report(p, Quests.Events.ResourceRun, count, now);
            Quests.Report(p, Quests.Events.Sweep, count, now);
            return DungeonEntryResult.Ok;
        }

        private static DungeonEntryResult Consume(PlayerProfile p, ResourceDungeonDef d, int count, long now)
        {
            p.EnsureDaily(now);
            if (!d.IsOpen(now)) return DungeonEntryResult.NotOpenToday;
            if (p.Level < d.MinPlayerLevel) return DungeonEntryResult.LevelTooLow;
            p.DailyCounters.TryGetValue(d.Id, out int used);
            if (used + count > d.DailyLimit) return DungeonEntryResult.LimitReached;
            if (!p.Stamina.TrySpend(d.StaminaCost * count, now)) return DungeonEntryResult.NotEnoughStamina;
            p.DailyCounters[d.Id] = used + count;
            return DungeonEntryResult.Ok;
        }
    }

    /// <summary>Demo 資源副本（輪替與數值皆為建議值）。</summary>
    public static class DemoResourceDungeons
    {
        public static List<ResourceDungeonDef> Create() => new List<ResourceDungeonDef>
        {
            new ResourceDungeonDef
            {
                Id = "res_gold", Name = "糧倉護衛（金幣）", Weekdays = new List<int> { 0, 2, 4, 6 },
                MinPlayerLevel = 3, Reward = new Reward(gold: 4000),
            },
            new ResourceDungeonDef
            {
                Id = "res_exp", Name = "校場操練（經驗書）", Weekdays = new List<int> { 1, 3, 5, 6 },
                MinPlayerLevel = 3, Reward = new Reward().With(HeroGrowth.ExpBook, 6),
            },
            new ResourceDungeonDef
            {
                Id = "res_card", Name = "兵器鋪（卡牌強化素材）", Weekdays = new List<int> { 0, 3, 5, 6 },
                MinPlayerLevel = 9, Reward = new Reward().With(HeroGrowth.CardMaterial, 4),
            },
        };
    }
}
