using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>卡池定義（機率、保底、UP 全部資料驅動，可由後台調整；機率單位為萬分比）。</summary>
    public sealed class GachaPool
    {
        public string Id = "";
        public string Name = "";

        /// <summary>UR / SR 基礎機率（萬分比，300 = 3%）；R 為其餘。</summary>
        public int UrRateBp = 300;
        public int SrRateBp = 1700;

        public List<string> UrHeroes = new List<string>();
        public List<string> SrHeroes = new List<string>();
        public List<string> RHeroes = new List<string>();

        /// <summary>UP 的 UR（空 = 常駐池）。出 UR 時有 <see cref="UpRateBp"/> 機率為 UP，未中不保底。</summary>
        public string UpUr = "";
        public int UpRateBp = 5000;

        public int SingleCost = 200;
        public int TenCost = 2000;

        /// <summary>十連至少 1 張 SR（含以上）。</summary>
        public bool TenPullGuaranteesSr = true;

        /// <summary>硬保底：連續這麼多抽未出 UR 時下一抽必出 UR；0 = 停用（目前暫無，之後可調）。</summary>
        public int HardPityUr;

        /// <summary>新手池：玩家在本池的第一次十連至少 1 張 UR。</summary>
        public bool FirstTenGuaranteesUr;

        /// <summary>每個稀有度的機率公示（合規：公示值必須與實際一致，所以由同一份資料產生）。</summary>
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

    /// <summary>玩家在某個卡池的保底計數（需存檔，伺服器端保存）。</summary>
    public sealed class PoolState
    {
        public int PullsSinceUr;
        public int TotalPulls;
        public int TenPulls;
    }

    public sealed class PullResult
    {
        public string HeroId = "";
        public Rarity Rarity;
        public bool IsUp;
        /// <summary>true = 由保底（十連保底 / 新手保底 / 硬保底）強制提升而來。</summary>
        public bool FromPity;
        public bool IsNew;
        /// <summary>重複武將轉換出的突破碎片，新武將為 0。</summary>
        public int Shards;
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
        /// <summary>重複武將轉換的碎片：一隻重複 = 一次突破的份量（任何稀有度皆同，見 progression.md）。</summary>
        public static int DuplicateShards(Rarity rarity) => HeroGrowth.CopyShards;

        /// <summary>
        /// 抽卡（純規則，不扣款）：單抽或十連。<paramref name="state"/> 會被更新。
        /// 不論保底如何介入，單抽 / 十連的實際機率都在 <see cref="GachaPool.DisclosedRates"/> 公示的基礎機率上。
        /// </summary>
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

                var res = Pick(pool, rarity, rng);
                res.FromPity = fromPity;
                results.Add(res);

                state.TotalPulls++;
                if (rarity == Rarity.UR) state.PullsSinceUr = 0;
                else state.PullsSinceUr++;
            }

            if (isTen) state.TenPulls++;
            return results;
        }

        /// <summary>抽卡並結算：檢查元寶、扣款、發武將，重複的轉成突破素材。</summary>
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
                    r.Shards = DuplicateShards(r.Rarity);
                    player.Materials.TryGetValue("shard:" + r.HeroId, out int have);
                    player.Materials["shard:" + r.HeroId] = have + r.Shards;
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

        private static PullResult Pick(GachaPool pool, Rarity rarity, Rng rng)
        {
            var res = new PullResult { Rarity = rarity };
            if (rarity == Rarity.UR)
            {
                if (!string.IsNullOrEmpty(pool.UpUr))
                {
                    var others = pool.UrHeroes.Where(h => h != pool.UpUr).ToList();
                    bool up = others.Count == 0 || rng.Next(10000) < pool.UpRateBp;
                    res.IsUp = up;
                    res.HeroId = up ? pool.UpUr : others[rng.Next(others.Count)];
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
