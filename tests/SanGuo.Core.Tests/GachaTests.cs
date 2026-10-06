using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class GachaTests
    {
        private static GachaPool Pool(string up = "", int hardPity = 0, bool newbie = false) => new GachaPool
        {
            Id = newbie ? "newbie" : "std",
            UrHeroes = { "ur1", "ur2", "ur3" },
            SrHeroes = { "sr1", "sr2" },
            RHeroes = { "r1", "r2", "r3" },
            UpUr = up,
            HardPityUr = hardPity,
            FirstTenGuaranteesUr = newbie,
        };

        [Fact]
        public void Rates_MatchDisclosed_OverManySingles()
        {
            var pool = Pool();
            var state = new PoolState();
            var rng = new Rng(1);
            int n = 200_000;
            int ur = 0, sr = 0;
            for (int i = 0; i < n; i++)
            {
                var r = Gacha.Roll(pool, state, 1, rng)[0];
                if (r.Rarity == Rarity.UR) ur++;
                else if (r.Rarity == Rarity.SR) sr++;
            }
            Assert.InRange(ur / (double)n, 0.027, 0.033);
            Assert.InRange(sr / (double)n, 0.16, 0.18);
        }

        [Fact]
        public void DisclosedRates_SumTo100()
        {
            var rates = Pool().DisclosedRates();
            Assert.Equal(100.0, rates.Values.Sum(), 6);
            Assert.Equal(3.0, rates[Rarity.UR], 6);
        }

        [Fact]
        public void TenPull_AlwaysHasAtLeastOneSr()
        {
            var pool = Pool();
            var state = new PoolState();
            var rng = new Rng(7);
            for (int i = 0; i < 5000; i++)
            {
                var ten = Gacha.Roll(pool, state, 10, rng);
                Assert.Equal(10, ten.Count);
                Assert.Contains(ten, r => r.Rarity != Rarity.R);
            }
        }

        [Fact]
        public void TenPull_GuaranteeCanBeDisabled()
        {
            var pool = Pool();
            pool.TenPullGuaranteesSr = false;
            pool.UrRateBp = 0;
            pool.SrRateBp = 0;
            var ten = Gacha.Roll(pool, new PoolState(), 10, new Rng(1));
            Assert.All(ten, r => Assert.Equal(Rarity.R, r.Rarity));
        }

        [Fact]
        public void NewbiePool_FirstTenHasUr_SecondDoesNotForce()
        {
            var pool = Pool(newbie: true);
            pool.UrRateBp = 0; // 排除自然出 UR，只看保底
            var state = new PoolState();
            var first = Gacha.Roll(pool, state, 10, new Rng(3));
            Assert.Contains(first, r => r.Rarity == Rarity.UR && r.FromPity);
            var second = Gacha.Roll(pool, state, 10, new Rng(3));
            Assert.DoesNotContain(second, r => r.Rarity == Rarity.UR);
        }

        [Fact]
        public void UpPool_UrIsUpAboutHalf_WithoutPityCarry()
        {
            var pool = Pool(up: "ur1");
            pool.UrRateBp = 10000; // 每抽都是 UR，方便統計 UP 比例
            var state = new PoolState();
            var rng = new Rng(11);
            int up = 0, n = 20_000;
            bool prevMiss = false;
            int afterMiss = 0, afterMissUp = 0;
            for (int i = 0; i < n; i++)
            {
                var r = Gacha.Roll(pool, state, 1, rng)[0];
                if (r.IsUp) up++;
                if (prevMiss)
                {
                    afterMiss++;
                    if (r.IsUp) afterMissUp++;
                }
                prevMiss = !r.IsUp;
            }
            Assert.InRange(up / (double)n, 0.48, 0.52);
            // 未中不保底：上一抽沒中 UP，下一抽仍是 50%。
            Assert.InRange(afterMissUp / (double)afterMiss, 0.47, 0.53);
        }

        [Fact]
        public void HardPity_ForcesUrAtThreshold_AndResetsCounter()
        {
            var pool = Pool(hardPity: 30);
            pool.UrRateBp = 0;
            var state = new PoolState();
            var rng = new Rng(5);
            for (int i = 0; i < 29; i++)
                Assert.NotEqual(Rarity.UR, Gacha.Roll(pool, state, 1, rng)[0].Rarity);
            var hit = Gacha.Roll(pool, state, 1, rng)[0];
            Assert.Equal(Rarity.UR, hit.Rarity);
            Assert.True(hit.FromPity);
            Assert.Equal(0, state.PullsSinceUr);
        }

        [Fact]
        public void HardPity_DisabledByDefault()
        {
            var pool = Pool();
            pool.UrRateBp = 0;
            var state = new PoolState();
            var rng = new Rng(5);
            for (int i = 0; i < 500; i++)
                Assert.NotEqual(Rarity.UR, Gacha.Roll(pool, state, 1, rng)[0].Rarity);
        }

        [Fact]
        public void SameSeed_SameResults()
        {
            var pool = Pool(up: "ur2");
            var a = Gacha.Roll(pool, new PoolState(), 10, new Rng(42)).Select(r => r.HeroId);
            var b = Gacha.Roll(pool, new PoolState(), 10, new Rng(42)).Select(r => r.HeroId);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Pull_ChargesYuanbao_AndRejectsWhenShort()
        {
            var p = PlayerProfile.CreateNew(0);
            var pool = Pool();
            p.Yuanbao = 1999;
            var fail = Gacha.Pull(p, pool, 10, new Rng(1));
            Assert.Equal(PullStatus.NotEnoughYuanbao, fail.Status);
            Assert.Equal(1999, p.Yuanbao);
            Assert.Empty(p.PoolStates);

            p.Yuanbao = 2200;
            var ok = Gacha.Pull(p, pool, 10, new Rng(1));
            Assert.Equal(PullStatus.Ok, ok.Status);
            Assert.Equal(200, p.Yuanbao);
            Assert.Equal(10, ok.Results.Count);
            var single = Gacha.Pull(p, pool, 1, new Rng(2));
            Assert.Equal(0, p.Yuanbao);
            Assert.Single(single.Results);
        }

        [Fact]
        public void Pull_InvalidCount_Rejected()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Yuanbao = 99999;
            Assert.Equal(PullStatus.InvalidPool, Gacha.Pull(p, Pool(), 5, new Rng(1)).Status);
            Assert.Equal(99999, p.Yuanbao);
        }

        [Fact]
        public void Duplicates_BecomeShards_NewHeroesAreOwned()
        {
            var p = PlayerProfile.CreateNew(0);
            var pool = new GachaPool { Id = "one", UrRateBp = 0, SrRateBp = 0, RHeroes = { "r1" }, SrHeroes = { "s" } };
            p.Yuanbao = 1000;
            var a = Gacha.Pull(p, pool, 1, new Rng(1));
            var b = Gacha.Pull(p, pool, 1, new Rng(2));
            Assert.True(a.Results[0].IsNew);
            Assert.False(b.Results[0].IsNew);
            Assert.Equal(Gacha.DuplicateShards(Rarity.R), b.Results[0].Shards);
            Assert.Equal(Gacha.DuplicateShards(Rarity.R), p.Materials["shard:r1"]);
        }

        [Fact]
        public void PoolStates_AreTrackedPerPool()
        {
            var p = PlayerProfile.CreateNew(0);
            p.Yuanbao = 10_000;
            var a = Pool(); a.Id = "a";
            var b = Pool(); b.Id = "b";
            Gacha.Pull(p, a, 10, new Rng(1));
            Gacha.Pull(p, b, 1, new Rng(1));
            Assert.Equal(10, p.PoolStates["a"].TotalPulls);
            Assert.Equal(1, p.PoolStates["b"].TotalPulls);
        }
    }
}
