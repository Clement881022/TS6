using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public sealed class GachaPool
    {
        public string Id = "";
        public string Name = "";

        public int UrRateBp = 300;
        public int SrRateBp = 1700;

        public List<string> UrHeroes = new List<string>();
        public List<string> SrHeroes = new List<string>();
        public List<string> RHeroes = new List<string>();

        public List<string> UpUrs = new List<string>();
        public int UpRateBp = 5000;

        public bool UpGuarantee = true;

        public int SingleCost = 200;
        public int TenCost = 2000;

        public bool TenPullGuaranteesSr = true;

        public int HardPityUr = 80;

        public bool FirstTenGuaranteesUr;

        public string PityDescription()
        {
            var parts = new List<string>();
            if (TenPullGuaranteesSr) parts.Add("十連至少獲得 1 名 SR 以上武將");
            if (HardPityUr > 0) parts.Add($"連續 {HardPityUr} 次未獲得 UR，第 {HardPityUr} 次必定獲得 UR");
            if (UpUrs.Count > 0)
            {
                parts.Add($"獲得 UR 時有 {UpRateBp / 100.0:0.##}% 機率為 UP 武將");
                if (UpGuarantee) parts.Add("上一名 UR 未獲得 UP 武將時，下一名 UR 必定為 UP 武將");
            }
            if (FirstTenGuaranteesUr) parts.Add("首次十連必定獲得 1 名 UR");
            return string.Join("；", parts);
        }

        public Dictionary<Rarity, double> DisclosedRates()
        {
            int rBp = 10000 - UrRateBp - SrRateBp;
            return new Dictionary<Rarity, double>
            {
                { Rarity.UR, UrRateBp / 100.0 },
                { Rarity.SR, SrRateBp / 100.0 },
                { Rarity.R, rBp / 100.0 },
            };
        }
    }

    public sealed class PoolState
    {
        public int PullsSinceUr;
        public bool UpGuaranteed;
        public int TotalPulls;
        public int TenPulls;
    }

    public sealed class PullResult
    {
        public string HeroId = "";
        public Rarity Rarity;
        public bool IsUp;
        public bool FromPity;
        public bool IsNew;
        public int Shards;
        public int Souls;
    }

    public enum PullStatus
    {
        Ok,
        NotEnoughYuanbao,
        InvalidPool,
    }

    public sealed class PullOutcome
    {
        public PullStatus Status;
        public List<PullResult> Results = new List<PullResult>();
    }

    public static class Gacha
    {
        public static List<PullResult> Roll(GachaPool pool, PoolState state, int count, Rng rng)
        {
            var results = new List<PullResult>(count);
            bool isTen = count == 10;
            bool firstTen = isTen && pool.FirstTenGuaranteesUr && state.TenPulls == 0;

            for (int i = 0; i < count; i++)
            {
                bool last = i == count - 1;
                bool forceUr = false;
                bool fromPity = false;

                if (pool.HardPityUr > 0 && state.PullsSinceUr + 1 >= pool.HardPityUr)
                {
                    forceUr = true;
                    fromPity = true;
                }
                if (isTen && last)
                {
                    if (firstTen && !results.Any(r => r.Rarity == Rarity.UR))
                    {
                        forceUr = true;
                        fromPity = true;
                    }
                }

                Rarity rarity = forceUr ? Rarity.UR : RollRarity(pool, rng);

                if (isTen && last && pool.TenPullGuaranteesSr && rarity == Rarity.R
                    && !results.Any(r => r.Rarity != Rarity.R))
                {
                    rarity = Rarity.SR;
                    fromPity = true;
                }

                var res = Pick(pool, state, rarity, rng);
                res.FromPity = res.FromPity || fromPity;
                results.Add(res);

                state.TotalPulls++;
                if (rarity == Rarity.UR) state.PullsSinceUr = 0;
                else state.PullsSinceUr++;
            }

            if (isTen) state.TenPulls++;
            return results;
        }

        public static PullOutcome Pull(PlayerProfile player, GachaPool pool, int count, Rng rng)
        {
            var outcome = new PullOutcome();
            if (pool == null || (count != 1 && count != 10))
            {
                outcome.Status = PullStatus.InvalidPool;
                return outcome;
            }
            int cost = count == 1 ? pool.SingleCost : pool.TenCost;
            if (player.Yuanbao < cost)
            {
                outcome.Status = PullStatus.NotEnoughYuanbao;
                return outcome;
            }

            if (!player.PoolStates.TryGetValue(pool.Id, out var state))
            {
                state = new PoolState();
                player.PoolStates[pool.Id] = state;
            }

            player.Yuanbao -= cost;
            outcome.Results = Roll(pool, state, count, rng);
            foreach (var r in outcome.Results)
            {
                if (!player.Heroes.ContainsKey(r.HeroId))
                {
                    player.Heroes[r.HeroId] = new HeroState { HeroId = r.HeroId };
                    r.IsNew = true;
                }
                else
                {
                    var dup = HeroGrowth.AddDuplicate(player, r.HeroId);
                    r.Shards = dup.Shards;
                    r.Souls = dup.Souls;
                }
            }
            outcome.Status = PullStatus.Ok;
            return outcome;
        }

        private static Rarity RollRarity(GachaPool pool, Rng rng)
        {
            int roll = rng.Next(10000);
            if (roll < pool.UrRateBp) return Rarity.UR;
            if (roll < pool.UrRateBp + pool.SrRateBp) return Rarity.SR;
            return Rarity.R;
        }

        private static PullResult Pick(GachaPool pool, PoolState state, Rarity rarity, Rng rng)
        {
            var res = new PullResult { Rarity = rarity };
            if (rarity == Rarity.UR)
            {
                if (pool.UpUrs.Count > 0)
                {
                    var others = pool.UrHeroes.Where(h => !pool.UpUrs.Contains(h)).ToList();
                    bool guaranteed = pool.UpGuarantee && state.UpGuaranteed;
                    bool up = others.Count == 0 || guaranteed || rng.Next(10000) < pool.UpRateBp;
                    res.IsUp = up;
                    if (guaranteed) res.FromPity = true;
                    state.UpGuaranteed = pool.UpGuarantee && !up;
                    res.HeroId = up ? pool.UpUrs[rng.Next(pool.UpUrs.Count)] : others[rng.Next(others.Count)];
                }
                else
                {
                    res.HeroId = pool.UrHeroes[rng.Next(pool.UrHeroes.Count)];
                }
            }
            else
            {
                var list = rarity == Rarity.SR ? pool.SrHeroes : pool.RHeroes;
                if (list.Count == 0) throw new InvalidOperationException($"卡池 {pool.Id} 沒有 {rarity} 武將");
                res.HeroId = list[rng.Next(list.Count)];
            }
            return res;
        }
    }
}
