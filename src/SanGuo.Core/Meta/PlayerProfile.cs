using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public sealed class StageReward
    {
        public string StageId = "";
        public int Chapter = 1;
        public int StaminaCost = 10;
        public int Exp;
        public int Gold;
        public int FirstClearYuanbao;
        public string FirstClearHero = "";
        public string[] FirstClearDuplicates = Array.Empty<string>();
        public int StarTurnPar;
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, int> FirstClearMaterials = new Dictionary<string, int>();
    }

    public enum SweepResult
    {
        Ok,
        InvalidCount,
        NotThreeStars,
        NotEnoughStamina,
    }

    public enum StageEntryResult
    {
        Ok,
        NotEnoughStamina,
    }

    public sealed class ClearResult
    {
        public bool FirstClear;
        public int ExpGained;
        public int GoldGained;
        public int YuanbaoGained;
        public int LevelsGained;
        public string HeroGained = "";
        public List<string> DuplicatesGained = new List<string>();
    }

    public sealed class PlayerProfile
    {
        public int Level = 1;
        public int Exp;
        public int Yuanbao;
        public int Gold;
        public StaminaClock Stamina = new StaminaClock();
        public HashSet<string> ClearedStages = new HashSet<string>();
        public Dictionary<string, HeroState> Heroes = new Dictionary<string, HeroState>();
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, PoolState> PoolStates = new Dictionary<string, PoolState>();
        public Dictionary<string, int> StageStars = new Dictionary<string, int>();

        public string PendingStageId = "";
        public long PendingSeed;
        public List<FormationEntry> PendingFormation = new List<FormationEntry>();

        public PassState Pass = new PassState();
        public bool FirstPackBought;

        public WorldBossState WorldBoss = new WorldBossState();

        public long CreatedDay;
        public long DailyDay = long.MinValue;
        public Dictionary<string, int> DailyTaskProgress = new Dictionary<string, int>();
        public HashSet<string> DailyTaskClaimed = new HashSet<string>();
        public Dictionary<string, int> SevenDayProgress = new Dictionary<string, int>();
        public HashSet<string> SevenDayClaimed = new HashSet<string>();
        public long WeeklyWeek = long.MinValue;
        public Dictionary<string, int> WeeklyProgress = new Dictionary<string, int>();
        public HashSet<string> WeeklyClaimed = new HashSet<string>();
        public HashSet<string> RechargeBought = new HashSet<string>();

        public Dictionary<string, long> MonthCardExpiry = new Dictionary<string, long>();
        public Dictionary<string, long> MonthCardClaimedDay = new Dictionary<string, long>();
        public Dictionary<string, int> SoulShopBought = new Dictionary<string, int>();
        public string SoulShopMonth = "";
        public Dictionary<string, string> Orders = new Dictionary<string, string>();

        public int GetMaterial(string key) => Materials.TryGetValue(key, out int n) ? n : 0;

        public void AddMaterial(string key, int amount) => Materials[key] = GetMaterial(key) + amount;

        public static PlayerProfile CreateNew(long now) => new PlayerProfile
        {
            Stamina = new StaminaClock(PlayerLevelCurve.StaminaCap(1), PlayerLevelCurve.StaminaRegenSeconds, now),
            CreatedDay = DailyClock.DayIndex(now),
        };

        public void EnsureDaily(long now)
        {
            long week = DailyClock.WeekIndex(now);
            if (week != WeeklyWeek)
            {
                WeeklyWeek = week;
                WeeklyProgress.Clear();
                WeeklyClaimed.Clear();
            }
            long day = DailyClock.DayIndex(now);
            if (day == DailyDay) return;
            DailyDay = day;
            DailyTaskProgress.Clear();
            DailyTaskClaimed.Clear();
        }

        public void OnLogin(long now)
        {
            EnsureDaily(now);
            if (!DailyTaskProgress.ContainsKey("d_login")) Quests.Report(this, Quests.Events.LoginDay, 1, now);
            Quests.Report(this, Quests.Events.Login, 1, now);
        }

        public void Grant(Reward reward, long now)
        {
            Yuanbao += reward.Yuanbao;
            Gold += reward.Gold;
            if (reward.Stamina > 0) Stamina.Add(reward.Stamina, now);
            foreach (var m in reward.Materials) AddMaterial(m.Key, m.Value);
            Equipment.AutoForge(this);
            foreach (var id in reward.Heroes)
            {
                if (Heroes.ContainsKey(id)) HeroGrowth.AddDuplicate(this, id);
                else Heroes[id] = new HeroState { HeroId = id };
            }
        }

        public StageEntryResult TryEnterStage(StageReward stage, long now)
        {
            if (!Stamina.TrySpend(stage.StaminaCost, now))
                return StageEntryResult.NotEnoughStamina;
            BattlePass.AddPoints(this, stage.StaminaCost, now);
            return StageEntryResult.Ok;
        }

        public ClearResult ClaimClear(StageReward stage, long now, int stars = 1)
        {
            stars = Math.Max(1, Math.Min(3, stars));
            StageStars.TryGetValue(stage.StageId, out int best);
            if (stars > best) StageStars[stage.StageId] = stars;
            var result = new ClearResult
            {
                FirstClear = ClearedStages.Add(stage.StageId),
                ExpGained = stage.Exp,
                GoldGained = stage.Gold,
            };
            Gold += stage.Gold;
            foreach (var m in stage.Materials) AddMaterial(m.Key, m.Value);
            if (result.FirstClear)
            {
                foreach (var m in stage.FirstClearMaterials) AddMaterial(m.Key, m.Value);
                Yuanbao += stage.FirstClearYuanbao;
                result.YuanbaoGained = stage.FirstClearYuanbao;
                if (stage.FirstClearHero != "")
                {
                    Grant(new Reward().WithHero(stage.FirstClearHero), now);
                    result.HeroGained = stage.FirstClearHero;
                }
                foreach (var id in stage.FirstClearDuplicates)
                {
                    Grant(new Reward().WithHero(id), now);
                    result.DuplicatesGained.Add(id);
                }
            }
            result.LevelsGained = AddExp(stage.Exp, now);
            Quests.Report(this, Quests.Events.StageClear, 1, now);
            return result;
        }

        public const int MaxSweepCount = 10;

        public SweepResult TrySweep(StageReward stage, int count, long now, out ClearResult? result)
        {
            result = null;
            if (count < 1 || count > MaxSweepCount) return SweepResult.InvalidCount;
            if (!StageStars.TryGetValue(stage.StageId, out int stars) || stars < 3) return SweepResult.NotThreeStars;
            if (!Stamina.TrySpend(stage.StaminaCost * count, now)) return SweepResult.NotEnoughStamina;
            BattlePass.AddPoints(this, stage.StaminaCost * count, now);

            result = new ClearResult { ExpGained = stage.Exp * count, GoldGained = stage.Gold * count };
            Gold += result.GoldGained;
            foreach (var m in stage.Materials) AddMaterial(m.Key, m.Value * count);
            result.LevelsGained = AddExp(result.ExpGained, now);
            Quests.Report(this, Quests.Events.StageClear, count, now);
            Quests.Report(this, Quests.Events.Sweep, count, now);
            return SweepResult.Ok;
        }

        public int AddExp(int amount, long now)
        {
            int gained = 0;
            Exp += amount;
            while (Level < PlayerLevelCurve.MaxLevel && Exp >= PlayerLevelCurve.ExpToNext(Level))
            {
                Exp -= PlayerLevelCurve.ExpToNext(Level);
                Level++;
                gained++;
            }
            if (gained > 0)
            {
                Stamina.SetCap(PlayerLevelCurve.StaminaCap(Level), now);
                Stamina.RefillToCap(now);
            }
            if (Level >= PlayerLevelCurve.MaxLevel) Exp = 0;
            return gained;
        }
    }
}
