using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家本季的通行證進度（存檔的一部分）。</summary>
    public sealed class PassState
    {
        /// <summary>賽季（<see cref="DailyClock.MonthKey"/>）；換季時進度、購買與領取紀錄全部重置。</summary>
        public string Season = "";
        public int Points;
        /// <summary>本季購買的付費版：空字串 = 未購買；<see cref="BattlePass.Basic"/> 或 <see cref="BattlePass.Luxury"/>。</summary>
        public string Tier = "";
        public HashSet<int> ClaimedFree = new HashSet<int>();
        public HashSet<int> ClaimedPaid = new HashSet<int>();
    }

    public enum PassClaimResult { Ok, InvalidLevel, NotReached, NotPurchased, AlreadyClaimed }

    /// <summary>
    /// 通行證（GDD 05 §9 的 1.0 付費點；內容與定價為暫定，待確認）：
    /// 每月一季（與世界 Boss 同步），30 級，每級 150 點；消耗體力 1 點 = 通行證經驗 1 點（主線、掃蕩、素材副本）。
    /// 免費線：金幣、武將經驗、將魂，不給元寶（不改變無課玩家的月抽數預算）。
    /// 付費線（通行證 ¥30／豪華通行證 ¥98）：每級 50 元寶（全滿 1500）、每 5 級一件裝備（最高 3 階）、第 10／20／30 級將魂。
    /// 豪華版另外立即 +10 級並送 980 元寶。
    /// </summary>
    public static class BattlePass
    {
        public const string Basic = "basic";
        public const string Luxury = "luxury";
        public const int MaxLevel = 30;
        public const int PointsPerLevel = 150;
        public const int LuxuryBonusLevels = 10;

        public static string SeasonOf(long now) => DailyClock.MonthKey(now);

        /// <summary>換季：重置本季進度與購買。</summary>
        public static void Roll(PlayerProfile p, long now)
        {
            string season = SeasonOf(now);
            if (p.Pass.Season == season) return;
            p.Pass = new PassState { Season = season };
        }

        public static int Level(PlayerProfile p) => Math.Min(MaxLevel, p.Pass.Points / PointsPerLevel);

        public static void AddPoints(PlayerProfile p, int amount, long now)
        {
            if (amount <= 0) return;
            Roll(p, now);
            p.Pass.Points = Math.Min(MaxLevel * PointsPerLevel, p.Pass.Points + amount);
        }

        public static Reward FreeReward(int level)
        {
            var r = new Reward(gold: 2000);
            if (level % 5 == 0) r.With(HeroGrowth.HeroExp, 3000);
            if (level % 10 == 0) r.With(HeroGrowth.Soul, 20);
            return r;
        }

        public static Reward PaidReward(int level)
        {
            var r = new Reward(yuanbao: 50);
            if (level % 5 == 0)
            {
                var slot = Equipment.Slots[(level / 5 - 1) % Equipment.Slots.Length];
                // 付費線裝備最高 3 階（2026-10-09：原本 30 級送 5 階，模擬發現付費玩家第 5 天就有 4–5 階裝、主線快無課 3 倍，違反「主線進度與無課相同」）
                int tier = level < 15 ? 2 : 3;
                r.With(Equipment.ItemKey(slot, tier), 1);
            }
            if (level % 10 == 0) r.With(HeroGrowth.Soul, 30);
            return r;
        }

        public static PassClaimResult Claim(PlayerProfile p, int level, bool paid, long now)
        {
            Roll(p, now);
            if (level < 1 || level > MaxLevel) return PassClaimResult.InvalidLevel;
            if (Level(p) < level) return PassClaimResult.NotReached;
            if (paid && p.Pass.Tier == "") return PassClaimResult.NotPurchased;
            var claimed = paid ? p.Pass.ClaimedPaid : p.Pass.ClaimedFree;
            if (!claimed.Add(level)) return PassClaimResult.AlreadyClaimed;
            p.Grant(paid ? PaidReward(level) : FreeReward(level), now);
            return PassClaimResult.Ok;
        }

        /// <summary>一鍵領取所有已達成、尚未領取的獎勵；回傳領了幾項。</summary>
        public static int ClaimAll(PlayerProfile p, long now)
        {
            Roll(p, now);
            int n = 0;
            for (int lv = 1; lv <= Level(p); lv++)
            {
                if (Claim(p, lv, false, now) == PassClaimResult.Ok) n++;
                if (p.Pass.Tier != "" && Claim(p, lv, true, now) == PassClaimResult.Ok) n++;
            }
            return n;
        }

        /// <summary>付款完成後啟用付費版（由 <see cref="Shop.Fulfill"/> 呼叫）。</summary>
        internal static void Activate(PlayerProfile p, string tier, long now)
        {
            Roll(p, now);
            p.Pass.Tier = tier;
            if (tier == Luxury) AddPoints(p, LuxuryBonusLevels * PointsPerLevel, now);
        }
    }
}
