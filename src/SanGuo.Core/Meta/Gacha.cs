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

        /// <summary>UP 的 UR（空 = 常駐池，可多隻）。出 UR 時有 <see cref="UpRateBp"/> 機率為 UP（UP 武將中隨機一隻）；未中時依 <see cref="UpGuarantee"/> 決定下一隻 UR 是否必為 UP（大小保底）。</summary>
        public List<string> UpUrs = new List<string>();
        public int UpRateBp = 5000;

        /// <summary>大小保底：上一隻 UR 沒中 UP，下一隻 UR 必為 UP（2026-10-07 採用，見 gacha.md 第 7 節）。</summary>
        public bool UpGuarantee = true;

        public int SingleCost = 200;
        public int TenCost = 2000;

        /// <summary>十連至少 1 張 SR（含以上）。</summary>
        public bool TenPullGuaranteesSr = true;

        /// <summary>硬保底：連續這麼多抽未出 UR 時下一抽必出 UR；0 = 停用。預設 80（2026-10-07 採用）。</summary>
        public int HardPityUr = 80;

        /// <summary>新手池：玩家在本池的第一次十連至少 1 張 UR。</summary>
        public bool FirstTenGuaranteesUr;

        /// <summary>保底規則公示文字（機率公示須同時說明保底，合規要求與實際一致）。</summary>
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
        /// <summary>true = 上一隻 UR 沒中 UP，下一隻 UR 必為 UP（大小保底）。</summary>
        public bool UpGuaranteed;
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
        /// <summary>重複武將存成的重複份（未滿突時為 1），新武將為 0。</summary>
        public int Shards;
        /// <summary>重複武將轉成的將魂（已滿突時），新武將為 0。</summary>
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
