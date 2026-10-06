using System;
using System.Collections.Generic;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class ProfileSerializerTests
    {
        private static PlayerProfile Sample()
        {
            var p = PlayerProfile.CreateNew(1_000);
            p.Level = 7;
            p.Exp = 123;
            p.Yuanbao = 1600;
            p.Gold = 98765;
            p.Stamina.TrySpend(30, 2_000);
            p.ClearedStages.Add("1-2");
            p.ClearedStages.Add("1-1");
            var hero = new HeroState { HeroId = "zhangfei", Level = 5, Stars = 2 };
            hero.CardLevels["zf_attack"] = 3;
            p.Heroes["zhangfei"] = hero;
            p.AddMaterial(HeroGrowth.ExpBook, 40);
            p.AddMaterial(HeroGrowth.ShardKey("guanyu"), 20);
            p.PoolStates["standard"] = new PoolState { PullsSinceUr = 33, UpGuaranteed = true, TotalPulls = 50, TenPulls = 4 };
            return p;
        }

        [Fact]
        public void RoundTrip_PreservesEverything()
        {
            var original = Sample();
            var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(original));

            Assert.Equal(original.Level, loaded.Level);
            Assert.Equal(original.Exp, loaded.Exp);
            Assert.Equal(original.Yuanbao, loaded.Yuanbao);
            Assert.Equal(original.Gold, loaded.Gold);
            Assert.Equal(original.Stamina.Current, loaded.Stamina.Current);
            Assert.Equal(original.Stamina.Anchor, loaded.Stamina.Anchor);
            Assert.Equal(original.Stamina.Cap, loaded.Stamina.Cap);
            Assert.Equal(original.ClearedStages, loaded.ClearedStages);
            Assert.Equal(2, loaded.Heroes["zhangfei"].Stars);
            Assert.Equal(3, loaded.Heroes["zhangfei"].CardLevels["zf_attack"]);
            Assert.Equal(40, loaded.GetMaterial(HeroGrowth.ExpBook));
            Assert.Equal(20, loaded.GetMaterial(HeroGrowth.ShardKey("guanyu")));
            Assert.True(loaded.PoolStates["standard"].UpGuaranteed);
            Assert.Equal(33, loaded.PoolStates["standard"].PullsSinceUr);
        }

        [Fact]
        public void Output_IsDeterministic()
        {
            Assert.Equal(ProfileSerializer.ToJson(Sample()), ProfileSerializer.ToJson(Sample()));
        }

        [Fact]
        public void NewerVersion_IsRejected()
        {
            Assert.Throws<FormatException>(() => ProfileSerializer.FromJson("{\"version\": 99}"));
        }

        [Fact]
        public void MissingFields_FallBackToDefaults()
        {
            var p = ProfileSerializer.FromJson("{\"version\":1,\"gold\":5}");
            Assert.Equal(5, p.Gold);
            Assert.Equal(1, p.Level);
            Assert.Empty(p.Heroes);
        }

        [Fact]
        public void MiniJson_ParsesEscapesAndNumbers()
        {
            var v = (Dictionary<string, object?>)MiniJson.Parse("{\"a\":\"x\\n\\u0041\\\"\",\"b\":[1,2.5,-3,true,null]}")!;
            Assert.Equal("x\nA\"", v["a"]);
            var list = (List<object?>)v["b"]!;
            Assert.Equal(1L, list[0]);
            Assert.Equal(2.5, list[1]);
            Assert.Equal(-3L, list[2]);
            Assert.Equal(true, list[3]);
            Assert.Null(list[4]);
        }

        [Fact]
        public void MiniJson_RejectsBrokenInput()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\":}"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("[1,2"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{} extra"));
        }
    }
}
