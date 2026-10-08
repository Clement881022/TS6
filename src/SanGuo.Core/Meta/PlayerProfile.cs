using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>關卡獎勵與消耗（資料驅動，之後由 JSON 載入）。</summary>
    public sealed class StageReward
    {
        public string StageId = "";
        public int Chapter = 1;
        public int StaminaCost = 10;
        public int Exp;
        public int Gold;
        public int FirstClearYuanbao;
        /// <summary>首通贈送的武將 id（空字串 = 不送）；已擁有就轉成重複份（滿突後轉將魂）。</summary>
        public string FirstClearHero = "";
        /// <summary>首通贈送的重複份（劇情武將的突破材料，每個 id 一份）；未擁有則直接獲得該武將。</summary>
        public string[] FirstClearDuplicates = Array.Empty<string>();
        /// <summary>第三星的限定回合數（0 = 不設限）。</summary>
        public int StarTurnPar;
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
        /// <summary>首通獲得的武將 id（沒有則空字串；已擁有的重複武將仍會回報，實際轉成碎片）。</summary>
        public string HeroGained = "";
        /// <summary>首通獲得的重複份（武將 id，每個一份）。</summary>
        public List<string> DuplicatesGained = new List<string>();
    }

    /// <summary>玩家存檔的純資料與規則（不含 I/O）：帳號等級、貨幣、體力、關卡進度。</summary>
    public sealed class PlayerProfile
    {
        public int Level = 1;
        public int Exp;
        public int Yuanbao;
        public int Gold;
        public StaminaClock Stamina = new StaminaClock();
        public HashSet<string> ClearedStages = new HashSet<string>();
        public Dictionary<string, HeroState> Heroes = new Dictionary<string, HeroState>();
        /// <summary>素材 / 碎片（例：「shard:guanyu」= 關羽突破素材）。</summary>
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, PoolState> PoolStates = new Dictionary<string, PoolState>();
        /// <summary>關卡最高星數（1–3）。</summary>
        public Dictionary<string, int> StageStars = new Dictionary<string, int>();

        /// <summary>進行中的關卡（已扣體力、伺服器發了種子，等待戰鬥重播驗證）；沒有則為空字串。</summary>
        public string PendingStageId = "";
        public long PendingSeed;
        /// <summary>進行中的關卡若開放編隊，這裡記下玩家送來的編隊（結算時用同一份重建戰鬥）。</summary>
        public List<FormationEntry> PendingFormation = new List<FormationEntry>();

        /// <summary>建立帳號的遊戲日（<see cref="DailyClock.DayIndex"/>），七日目標從這天起算。</summary>
        public long CreatedDay;
        /// <summary>每日資料所屬的遊戲日；換日時由 <see cref="EnsureDaily"/> 清空。</summary>
        public long DailyDay = long.MinValue;
        public Dictionary<string, int> DailyTaskProgress = new Dictionary<string, int>();
        public HashSet<string> DailyTaskClaimed = new HashSet<string>();
        public Dictionary<string, int> SevenDayProgress = new Dictionary<string, int>();
        /// <summary>已領取的七日任務與里程碑（里程碑 id 為 "milestone:點數"）。</summary>
        public HashSet<string> SevenDayClaimed = new HashSet<string>();

        /// <summary>月卡到期的遊戲日（<see cref="DailyClock.DayIndex"/>，不含該日）與最近一次領取每日獎勵的遊戲日。</summary>
        public Dictionary<string, long> MonthCardExpiry = new Dictionary<string, long>();
        public Dictionary<string, long> MonthCardClaimedDay = new Dictionary<string, long>();
        /// <summary>將魂商店本月的購買紀錄（商品 id → 次數）與所屬月份（yyyy-MM）。</summary>
        public Dictionary<string, int> SoulShopBought = new Dictionary<string, int>();
        public string SoulShopMonth = "";
        /// <summary>訂單（訂單 id → "pending:商品" 或 "paid:商品"），用來讓付款回呼冪等。</summary>
        public Dictionary<string, string> Orders = new Dictionary<string, string>();

        public int GetMaterial(string key) => Materials.TryGetValue(key, out int n) ? n : 0;

        public void AddMaterial(string key, int amount) => Materials[key] = GetMaterial(key) + amount;

        public static PlayerProfile CreateNew(long now) => new PlayerProfile
        {
            Stamina = new StaminaClock(PlayerLevelCurve.StaminaCap(1), PlayerLevelCurve.StaminaRegenSeconds, now),
            CreatedDay = DailyClock.DayIndex(now),
        };

        /// <summary>換日就清空每日資料（資源副本次數、每日任務）。每個會讀寫每日資料的動作都先呼叫它。</summary>
        public void EnsureDaily(long now)
        {
            long day = DailyClock.DayIndex(now);
            if (day == DailyDay) return;
            DailyDay = day;
            DailyTaskProgress.Clear();
            DailyTaskClaimed.Clear();
        }

        /// <summary>登入：換日重置每日資料並回報登入事件。</summary>
        public void OnLogin(long now)
        {
            EnsureDaily(now);
            Quests.Report(this, Quests.Events.Login, 1, now);
        }

        /// <summary>發放獎勵。贈送的武將若已擁有，當作重複份處理（滿突後轉將魂）。</summary>
        public void Grant(Reward reward, long now)
        {
            Yuanbao += reward.Yuanbao;
            Gold += reward.Gold;
            if (reward.Stamina > 0) Stamina.Add(reward.Stamina, now);
            foreach (var m in reward.Materials) AddMaterial(m.Key, m.Value);
            foreach (var id in reward.Heroes)
            {
                if (Heroes.ContainsKey(id)) HeroGrowth.AddDuplicate(this, id);
                else Heroes[id] = new HeroState { HeroId = id };
            }
        }

        /// <summary>開打前檢查體力（主線不設玩家等級門檻）；成功才扣體力。</summary>
        public StageEntryResult TryEnterStage(StageReward stage, long now)
        {
            if (!Stamina.TrySpend(stage.StaminaCost, now))
                return StageEntryResult.NotEnoughStamina;
            return StageEntryResult.Ok;
        }

        /// <summary>戰鬥勝利結算：經驗、金幣、首通元寶，並記錄星數（只會往上更新）。</summary>
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
            if (result.FirstClear)
            {
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

        /// <summary>
        /// 掃蕩：三星通關的關卡直接領獎勵，不用開戰。每次花一般體力；沒有首通元寶。
        /// 檢查全數通過才一次扣完體力。
        /// </summary>
        public SweepResult TrySweep(StageReward stage, int count, long now, out ClearResult? result)
        {
            result = null;
            if (count < 1 || count > MaxSweepCount) return SweepResult.InvalidCount;
            if (!StageStars.TryGetValue(stage.StageId, out int stars) || stars < 3) return SweepResult.NotThreeStars;
            if (!Stamina.TrySpend(stage.StaminaCost * count, now)) return SweepResult.NotEnoughStamina;

            result = new ClearResult { ExpGained = stage.Exp * count, GoldGained = stage.Gold * count };
            Gold += result.GoldGained;
            result.LevelsGained = AddExp(result.ExpGained, now);
            Quests.Report(this, Quests.Events.StageClear, count, now);
            Quests.Report(this, Quests.Events.Sweep, count, now);
            return SweepResult.Ok;
        }

        /// <summary>加經驗；每升一級提高體力上限（不補體力）。回傳升了幾級。</summary>
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
            if (gained > 0) Stamina.SetCap(PlayerLevelCurve.StaminaCap(Level), now);
            if (Level >= PlayerLevelCurve.MaxLevel) Exp = 0;
            return gained;
        }
    }
}
