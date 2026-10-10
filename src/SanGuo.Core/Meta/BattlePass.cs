using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public sealed class PassState
    {
        public string Season = "";
        public int Points;
        public string Tier = "";
        public HashSet<int> ClaimedFree = new HashSet<int>();
        public HashSet<int> ClaimedPaid = new HashSet<int>();
    }

    public enum PassClaimResult { Ok, InvalidLevel, NotReached, NotPurchased, AlreadyClaimed }

    public static class BattlePass
    {
        public const string Basic = "basic";
        public const int MaxLevel = 30;
        public const int PointsPerLevel = 150;

        public static string SeasonOf(long now) => DailyClock.MonthKey(now);

        public static void Roll(PlayerProfile p, long now)
        {
            string season = SeasonOf(now);
            if (p.Pass.Season == season)
            {
                if (p.Pass.Tier == "luxury") p.Pass.Tier = Basic;
                return;
            }
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
                int tier = level < 15 ? 2 : 3;
                r.With(slot == EquipSlot.Accessory ? Equipment.AccessoryKey(level == 15, tier) : Equipment.ItemKey(slot, tier), 1);
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

        internal static void Activate(PlayerProfile p, string tier, long now)
        {
            Roll(p, now);
            p.Pass.Tier = Basic;
        }
    }
}
