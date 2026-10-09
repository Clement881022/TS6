using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum QuestKind { Daily, SevenDay, Weekly }

    public enum QuestClaimResult
    {
        Ok,
        Unknown,
        NotComplete,
        AlreadyClaimed,
        NotUnlocked,
    }

    public sealed class QuestDef
    {
        public string Id = "";
        public QuestKind Kind;
        /// <summary>七日任務：第幾天開放（1–7）。每日任務忽略。</summary>
        public int Day = 1;
        public string Description = "";
        /// <summary>要累積的事件（見 <see cref="Quests.Events"/>）。</summary>
        public string EventKey = "";
        public int Target = 1;
        /// <summary>七日任務領取後獲得的任務點數。</summary>
        public int Points;
        public Reward Reward = new Reward();
    }

    /// <summary>七日目標的點數里程碑（終極大獎放在最高那一檔）。</summary>
    public sealed class QuestMilestone
    {
        public int Points;
        public Reward Reward = new Reward();
    }

    public sealed class QuestBook
    {
        public List<QuestDef> Quests = new List<QuestDef>();
        public List<QuestMilestone> Milestones = new List<QuestMilestone>();

        public QuestDef? Find(string id) => Quests.FirstOrDefault(q => q.Id == id);
    }

    /// <summary>
    /// 任務與七日目標（見 docs/days-1-7.md 4）。
    /// 遊戲內的動作成功後呼叫 <see cref="Report"/>；有帶時間的動作（關卡結算、掃蕩、資源副本）已自動回報，
    /// 其餘（升級、穿戴裝備、突破、抽卡）由伺服器的 API 層在成功後回報。
    /// </summary>
    public static class Quests
    {
        public const int SevenDays = 7;

        public static class Events
        {
            public const string StageClear = "stage_clear";
            public const string Sweep = "sweep";
            public const string ResourceRun = "resource_run";
            public const string HeroLevelUp = "hero_levelup";
            public const string Equip = "equip";
            public const string Breakthrough = "breakthrough";
            public const string GachaPull = "gacha_pull";
            public const string Login = "login";
            /// <summary>每天第一次登入（每週任務「登入 N 天」）。</summary>
            public const string LoginDay = "login_day";
            /// <summary>挑戰世界 Boss（結算一場）。</summary>
            public const string WorldBossFight = "world_boss";
        }

        /// <summary>今天是帳號的第幾天（建立當天 = 1）。</summary>
        public static int DayNumber(PlayerProfile p, long now) => (int)(DailyClock.DayIndex(now) - p.CreatedDay) + 1;

        /// <summary>回報事件：累積所有對應任務的進度（只累積到目標，七日任務限第 1–7 天且已開放的）。</summary>
        public static void Report(PlayerProfile p, string eventKey, int count, long now, QuestBook? book = null)
        {
            if (count <= 0) return;
            book ??= DemoQuests.Book;
            p.EnsureDaily(now);
            int day = DayNumber(p, now);
            foreach (var q in book.Quests)
            {
                if (q.EventKey != eventKey) continue;
                if (q.Kind == QuestKind.Daily) Bump(p.DailyTaskProgress, q, count);
                else if (q.Kind == QuestKind.Weekly) Bump(p.WeeklyProgress, q, count);
                else if (day >= q.Day && day <= SevenDays) Bump(p.SevenDayProgress, q, count);
            }
        }

        private static void Bump(Dictionary<string, int> progress, QuestDef q, int count)
        {
            progress.TryGetValue(q.Id, out int have);
            progress[q.Id] = System.Math.Min(q.Target, have + count);
        }

        public static int Progress(PlayerProfile p, QuestDef q)
        {
            var dict = q.Kind == QuestKind.Daily ? p.DailyTaskProgress : q.Kind == QuestKind.Weekly ? p.WeeklyProgress : p.SevenDayProgress;
            return dict.TryGetValue(q.Id, out int n) ? n : 0;
        }

        public static QuestClaimResult Claim(PlayerProfile p, string questId, long now, QuestBook? book = null)
        {
            book ??= DemoQuests.Book;
            p.EnsureDaily(now);
            var q = book.Find(questId);
            if (q == null) return QuestClaimResult.Unknown;
            var claimed = Claimed(p, q);
            if (q.Kind == QuestKind.SevenDay && DayNumber(p, now) < q.Day) return QuestClaimResult.NotUnlocked;
            if (claimed.Contains(q.Id)) return QuestClaimResult.AlreadyClaimed;
            if (Progress(p, q) < q.Target) return QuestClaimResult.NotComplete;
            claimed.Add(q.Id);
            p.Grant(q.Reward, now);
            return QuestClaimResult.Ok;
        }

        /// <summary>該任務所屬的已領取集合（每日／每週／七日）。</summary>
        public static HashSet<string> Claimed(PlayerProfile p, QuestDef q) =>
            q.Kind == QuestKind.Daily ? p.DailyTaskClaimed : q.Kind == QuestKind.Weekly ? p.WeeklyClaimed : p.SevenDayClaimed;

        /// <summary>七日任務點數（已領取的任務點數總和）。</summary>
        public static int SevenDayPoints(PlayerProfile p, QuestBook? book = null)
        {
            book ??= DemoQuests.Book;
            return book.Quests.Where(q => q.Kind == QuestKind.SevenDay && p.SevenDayClaimed.Contains(q.Id)).Sum(q => q.Points);
        }

        /// <summary>領取點數里程碑（點數夠就能領，不受第 7 天限制：沒領完的之後仍可補足）。</summary>
        public static QuestClaimResult ClaimMilestone(PlayerProfile p, int points, long now, QuestBook? book = null)
        {
            book ??= DemoQuests.Book;
            var m = book.Milestones.FirstOrDefault(x => x.Points == points);
            if (m == null) return QuestClaimResult.Unknown;
            string key = "milestone:" + points;
            if (p.SevenDayClaimed.Contains(key)) return QuestClaimResult.AlreadyClaimed;
            if (SevenDayPoints(p, book) < points) return QuestClaimResult.NotComplete;
            p.SevenDayClaimed.Add(key);
            p.Grant(m.Reward, now);
            return QuestClaimResult.Ok;
        }
    }

    /// <summary>Demo 任務表（建議值；七日大獎的 UR 尚未決定，暫以張飛佔位）。</summary>
    public static class DemoQuests
    {
        public static readonly QuestBook Book = Create();

        private static QuestDef Daily(string id, string desc, string ev, int target, Reward reward) =>
            new QuestDef { Id = id, Kind = QuestKind.Daily, Description = desc, EventKey = ev, Target = target, Reward = reward };

        /// <summary>每週任務（2026-10-09 企劃定案，暫定約 1000 元寶／週，讓無課每月多約 20 抽）。</summary>
        private static QuestDef Weekly(string id, string desc, string ev, int target, int yuanbao) =>
            new QuestDef { Id = id, Kind = QuestKind.Weekly, Description = desc, EventKey = ev, Target = target, Reward = new Reward(yuanbao: yuanbao) };

        private static QuestDef Seven(string id, int day, string desc, string ev, int target, int points) =>
            new QuestDef
            {
                Id = id, Kind = QuestKind.SevenDay, Day = day, Description = desc, EventKey = ev, Target = target,
                Points = points, Reward = new Reward(yuanbao: 100 * points / 10),
            };

        public static QuestBook Create()
        {
            var book = new QuestBook();
            book.Quests.AddRange(new[]
            {
                Daily("d_login", "登入遊戲", Quests.Events.Login, 1, new Reward(gold: 1000)),
                Daily("d_stage", "通關關卡 3 次", Quests.Events.StageClear, 3, new Reward(yuanbao: 50)),
                Daily("d_res", "挑戰資源副本 2 次", Quests.Events.ResourceRun, 2, new Reward(yuanbao: 50)),
                Daily("d_level", "升級武將 1 次", Quests.Events.HeroLevelUp, 1, new Reward(gold: 2000)),
                Daily("d_gacha", "抽卡 1 次", Quests.Events.GachaPull, 1, new Reward(yuanbao: 50)),

                Weekly("w_login", "本週登入 5 天", Quests.Events.LoginDay, 5, 200),
                Weekly("w_stage", "本週通關關卡 20 次", Quests.Events.StageClear, 20, 300),
                Weekly("w_res", "本週挑戰資源副本 15 次", Quests.Events.ResourceRun, 15, 300),
                Weekly("w_boss", "本週挑戰世界 Boss 10 次", Quests.Events.WorldBossFight, 10, 200),

                Seven("s1_level", 1, "升級武將 3 次", Quests.Events.HeroLevelUp, 3, 20),
                Seven("s1_stage", 1, "通關關卡 5 次", Quests.Events.StageClear, 5, 20),
                Seven("s1_gacha", 1, "抽卡 10 次", Quests.Events.GachaPull, 10, 20),
                Seven("s2_res", 2, "挑戰資源副本 3 次", Quests.Events.ResourceRun, 3, 20),
                Seven("s2_stage", 2, "通關關卡 8 次", Quests.Events.StageClear, 8, 20),
                Seven("s3_equip", 3, "穿戴裝備 3 次", Quests.Events.Equip, 3, 20),
                Seven("s3_stage", 3, "通關關卡 10 次", Quests.Events.StageClear, 10, 20),
                Seven("s4_sweep", 4, "使用掃蕩 3 次", Quests.Events.Sweep, 3, 20),
                Seven("s5_stage", 5, "通關關卡 10 次", Quests.Events.StageClear, 10, 20),
                Seven("s6_break", 6, "突破武將 1 次", Quests.Events.Breakthrough, 1, 20),
                Seven("s7_login", 7, "登入遊戲", Quests.Events.Login, 1, 20),
            });
            book.Milestones.Add(new QuestMilestone { Points = 60, Reward = new Reward(yuanbao: 500) });
            book.Milestones.Add(new QuestMilestone { Points = 120, Reward = new Reward(yuanbao: 1000).With(HeroGrowth.HeroExp, 5000) });
            book.Milestones.Add(new QuestMilestone { Points = 200, Reward = new Reward().WithHero("zhangfei") });
            return book;
        }
    }
}
