using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>關卡獎勵與消耗（資料驅動，之後由 JSON 載入）。</summary>
    public sealed class StageReward
    {
        public string StageId = "";
        public int Chapter = 1;
        public int StaminaCost = 8;
        public int Exp;
        public int Gold;
        public int FirstClearYuanbao;
    }

    public enum StageEntryResult
    {
        Ok,
        LevelTooLow,
        NotEnoughStamina,
    }

    public sealed class ClearResult
    {
        public bool FirstClear;
        public int ExpGained;
        public int GoldGained;
        public int YuanbaoGained;
        public int LevelsGained;
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
        public HashSet<string> OwnedHeroes = new HashSet<string>();
        /// <summary>素材 / 碎片（例：「shard:guanyu」= 關羽突破素材）。</summary>
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, PoolState> PoolStates = new Dictionary<string, PoolState>();

        public static PlayerProfile CreateNew(long now) => new PlayerProfile
        {
            Stamina = new StaminaClock(120, 360, now),
        };

        /// <summary>開打前檢查等級門檻與體力；成功才扣體力。</summary>
        public StageEntryResult TryEnterStage(StageReward stage, long now)
        {
            if (Level < PlayerLevelCurve.RequiredLevelForChapter(stage.Chapter))
                return StageEntryResult.LevelTooLow;
            if (!Stamina.TrySpend(stage.StaminaCost, now))
                return StageEntryResult.NotEnoughStamina;
            return StageEntryResult.Ok;
        }

        /// <summary>戰鬥勝利結算：經驗、金幣、首通元寶。</summary>
        public ClearResult ClaimClear(StageReward stage, long now)
        {
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
            }
            result.LevelsGained = AddExp(stage.Exp, now);
            return result;
        }

        /// <summary>加經驗；每升一級回滿體力。回傳升了幾級。</summary>
        public int AddExp(int amount, long now)
        {
            int gained = 0;
            Exp += amount;
            while (Level < PlayerLevelCurve.MaxLevel && Exp >= PlayerLevelCurve.ExpToNext(Level))
            {
                Exp -= PlayerLevelCurve.ExpToNext(Level);
                Level++;
                gained++;
                Stamina.RefillToCap(now);
            }
            if (Level >= PlayerLevelCurve.MaxLevel) Exp = 0;
            return gained;
        }
    }
}
