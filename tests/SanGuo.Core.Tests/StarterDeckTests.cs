using System.Linq;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>標竿卡組、效價驗算、職業屬性基準與武將名單（GDD 02、03、08）。</summary>
    public class StarterDeckTests
    {
        private static CardDef Special(HeroDef h, string suffix) => h.Deck.First(c => c.Id.EndsWith(suffix));

        // ---- 名單 ----

        [Fact]
        public void Roster_HasGddCounts()
        {
            var all = HeroRoster.All();
            Assert.Equal(6, all.Count(h => h.Rarity == Rarity.R));
            Assert.Equal(15, all.Count(h => h.Rarity == Rarity.SR));
            Assert.Equal(8, all.Count(h => h.Rarity == Rarity.UR));
            Assert.Equal(all.Count, all.Select(h => h.Id).Distinct().Count());
            Assert.Equal(all.Count, all.Select(h => h.Name).Distinct().Count());
        }

        [Fact]
        public void Roster_StoryHeroesAreSrAndNotDrawable_DrawableSrHasTwoPerRole()
        {
            foreach (var id in HeroRoster.StoryHeroIds) Assert.Equal(Rarity.SR, HeroRoster.Find(id)!.Rarity);
            var drawable = HeroRoster.DrawableSrIds().Select(id => HeroRoster.Find(id)!).ToList();
            Assert.Equal(12, drawable.Count);
            foreach (Role role in System.Enum.GetValues(typeof(Role)))
                Assert.Equal(2, drawable.Count(h => h.Role == role));
            Assert.Equal(Role.Healer, HeroRoster.Find("liubei")!.Role);
            Assert.Equal(Role.Warrior, HeroRoster.Find("guanyu")!.Role);
            Assert.Equal(Role.Tank, HeroRoster.Find("zhangfei")!.Role);
        }

        [Fact]
        public void Roster_UrPools_StandardHasOnePerRole_FirstUpIsZhangFeiAndGuanYu()
        {
            var std = HeroRoster.StandardUrIds.Select(id => HeroRoster.Find(id)!).ToList();
            Assert.Equal(6, std.Count);
            Assert.All(std, h => Assert.Equal(Rarity.UR, h.Rarity));
            Assert.Equal(6, std.Select(h => h.Role).Distinct().Count());
            var up = HeroRoster.FirstUpUrIds.Select(id => HeroRoster.Find(id)!).ToList();
            Assert.Equal(new[] { Role.Tank, Role.Warrior }, up.Select(h => h.Role).ToArray());
            Assert.Equal(8, std.Count + up.Count);
        }

        [Fact]
        public void EveryHero_HasFiveCards_ThreeBasicAndTwoSpecial()
        {
            foreach (var h in HeroRoster.All())
            {
                Assert.Equal(5, h.Deck.Count);
                Assert.Equal(3, h.Deck.Count(c => c.Basic));
                Assert.Equal(2, h.Deck.Count(c => !c.Basic));
                Assert.All(h.Deck.Where(c => c.Basic), c => Assert.Equal(1, c.Cost));
                Assert.All(h.Deck.Where(c => c.Basic), c => Assert.Equal(1.0, c.Effects[0].Multiplier));
            }
        }

        [Fact]
        public void SameRoleDifferentRarity_StatsScaleByRarity()
        {
            // 企劃 2026-10-09：稀有度影響基礎屬性 R 100%／SR 115%／UR 135%（生命、攻擊、謀略、防禦）；射程與移動力只看職業。
            foreach (var h in HeroRoster.All())
            {
                var role = CardLibrary.RoleStats(h.Role);
                double f = CardLibrary.RarityStatFactor(h.Rarity);
                Assert.Equal((int)System.Math.Round(role.Hp * f, System.MidpointRounding.AwayFromZero), h.Base.Hp);
                Assert.Equal((int)System.Math.Round(role.Atk * f, System.MidpointRounding.AwayFromZero), h.Base.Atk);
                Assert.Equal(role.Range, h.Base.Range);
                Assert.Equal(role.Move, h.Base.Move);
            }
            Assert.True(HeroRoster.Find("lvbu")!.Base.Atk > HeroRoster.Find("huaxiong")!.Base.Atk);
            Assert.True(HeroRoster.Find("huaxiong")!.Base.Atk > HeroRoster.Find("r_sword")!.Base.Atk);
        }

        // ---- 屬性基準（08 §5）與預設範圍（03 §2.1）----

        [Theory]
        [InlineData(Role.Tank, 800, 80, 50, 100, 0, 5, 1, 1)]
        [InlineData(Role.Warrior, 600, 120, 40, 50, 0, 20, 1, 2)]
        [InlineData(Role.Ranger, 450, 110, 50, 30, 10, 10, 2, 2)]
        [InlineData(Role.Mage, 400, 40, 120, 0, 5, 0, 2, 1)]
        [InlineData(Role.Strategist, 450, 40, 100, 10, 5, 0, 2, 1)]
        [InlineData(Role.Healer, 450, 40, 110, 10, 5, 0, 2, 1)]
        public void RoleStats_MatchGdd(Role role, int hp, int atk, int intel, int def, int dodge, int crit, int range, int move)
        {
            var s = CardLibrary.RoleStats(role);
            Assert.Equal((hp, atk, intel, def, dodge, crit, range, move), (s.Hp, s.Atk, s.Int, s.Def, s.Dodge, s.Crit, s.Range, s.Move));
            Assert.Equal(150, s.CritDmg);
        }

        // ---- 標竿卡組（02 §3）----

        [Fact]
        public void Benchmark_Tank_TauntTwice()
        {
            var h = HeroRoster.MilitiaShield();
            var taunts = h.Deck.Where(c => c.Name == "嘲諷").ToList();
            Assert.Equal(2, taunts.Count);
            Assert.All(taunts, t =>
            {
                Assert.Equal(1, t.Cost);
                Assert.Equal(TargetRule.AllEnemies, t.Target);
                Assert.Equal(StatusType.Taunt, t.Effects[0].Status);
                Assert.Equal(1, t.Effects[0].Amount);
            });
            Assert.NotEqual(taunts[0].Id, taunts[1].Id); // 兩張各自可升級
        }

        [Fact]
        public void Benchmark_Warrior_SweepAndHeavy()
        {
            var h = HeroRoster.MilitiaSword();
            var sweep = Special(h, "_sweep");
            var heavy = Special(h, "_heavy");
            Assert.Equal((2, Shape.Row3, 1.11), (sweep.Cost, sweep.Shape, sweep.Effects[0].Multiplier));
            Assert.Equal((2, Shape.Single, 1.67), (heavy.Cost, heavy.Shape, heavy.Effects[0].Multiplier));
            Assert.Equal(DamageKind.Physical, sweep.Effects[0].Kind);
        }

        [Fact]
        public void Benchmark_Ranger_ArmorPiercingArrowTwice()
        {
            var h = HeroRoster.MilitiaArcher();
            var arrows = h.Deck.Where(c => c.Name == "破甲箭").ToList();
            Assert.Equal(2, arrows.Count);
            Assert.All(arrows, a =>
            {
                Assert.Equal(2, a.Cost);
                Assert.Equal(1.5, a.Effects[0].Multiplier);
                Assert.Equal(StatusType.ArmorBreak, a.Effects[1].Status);
                Assert.Equal(0.25, a.Effects[1].Multiplier);
            });
        }

        [Fact]
        public void Benchmark_Mage_FireTwice_BasicAttackIsMagical()
        {
            var h = HeroRoster.MilitiaMage();
            var fires = h.Deck.Where(c => c.Name == "火攻").ToList();
            Assert.Equal(2, fires.Count);
            Assert.All(fires, f =>
            {
                Assert.Equal(2, f.Cost);
                Assert.Equal(StatusType.Burn, f.Effects[0].Status);
                Assert.Equal(1.67, f.Effects[0].Multiplier);
            });
            var attack = h.Deck.First(c => c.Basic);
            Assert.Equal(DamageKind.Magical, attack.Effects[0].Kind);
        }

        [Fact]
        public void Benchmark_Healer_HealAndBarrier()
        {
            var h = HeroRoster.MilitiaHealer();
            var heal = Special(h, "_heal");
            var barrier = Special(h, "_barrier");
            Assert.Equal((1, EffectType.Heal, 1.0), (heal.Cost, heal.Effects[0].Type, heal.Effects[0].Multiplier));
            Assert.Equal((1, EffectType.Shield, 0.67), (barrier.Cost, barrier.Effects[0].Type, barrier.Effects[0].Multiplier));
            Assert.Equal(TargetRule.Ally, heal.Target);
        }

        [Fact]
        public void Benchmark_Strategist_GroupRallyTwice()
        {
            var h = HeroRoster.MilitiaStrategist();
            var rallies = h.Deck.Where(c => c.Name == "群體鼓舞").ToList();
            Assert.Equal(2, rallies.Count);
            Assert.All(rallies, r =>
            {
                Assert.Equal(1, r.Cost);
                Assert.Equal(TargetRule.AllAllies, r.Target);
                Assert.Equal(StatusType.AtkUp, r.Effects[0].Status);
                Assert.Equal(0.25, r.Effects[0].Multiplier);
                Assert.Equal(2, r.Effects[0].Amount);
            });
        }

        // ---- 效價驗算（02 §4）：效價 = 費用 + 0.5 ----

        private static double Efficiency(CardDef c)
        {
            var e = c.Effects[0];
            double shape = c.Shape == Shape.Row3 || c.Shape == Shape.Column3 ? 1.5 : c.Shape == Shape.Cross ? 2.0 : c.Shape == Shape.All ? 3.0 : 1.0;
            switch (e.Type)
            {
                case EffectType.Damage: return e.Multiplier * 1.5 * shape + (c.Effects.Count > 1 ? 0.25 : 0); // 破甲 25% ≈ 0.25 費
                case EffectType.Heal: return e.Multiplier * 1.5 * shape;
                case EffectType.Shield: return e.Multiplier * 1.5 * 1.5 * shape;
                default:
                    if (e.Status == StatusType.Burn) return e.Multiplier * 1.5;
                    return c.Cost + 0.5; // 嘲諷、鼓舞：預算 1.5，依 GDD 直接視為定價
            }
        }

        [Theory]
        [InlineData("r_swd_attack", 1.5)]
        [InlineData("r_swd_heavy", 2.5)]
        [InlineData("r_swd_sweep", 2.5)]
        [InlineData("r_hlr_heal", 1.5)]
        [InlineData("r_hlr_barrier", 1.5)]
        [InlineData("r_arc_pierce", 2.5)]
        [InlineData("r_mag_fire", 2.5)]
        [InlineData("r_shd_taunt", 1.5)]
        [InlineData("r_str_rally", 1.5)]
        public void Benchmark_Efficiency_EqualsCostPlusHalf(string cardId, double expected)
        {
            var card = HeroRoster.All().SelectMany(h => h.Deck).First(c => c.Id == cardId);
            Assert.Equal(card.Cost + 0.5, expected);
            Assert.Equal(expected, Efficiency(card), 1);
        }

        // ---- 稀有度：SR / UR 特殊卡每張效價 +1 / +2 ----

        [Theory]
        [InlineData("_sweep")]
        [InlineData("_heavy")]
        [InlineData("_heal")]
        [InlineData("_barrier")]
        [InlineData("_fire")]
        [InlineData("_pierce")]
        public void RarityRaisesSpecialEfficiency_ByOneAndTwo(string suffix)
        {
            foreach (Role role in System.Enum.GetValues(typeof(Role)))
            {
                var r = CardLibrary.BuildDeck("x", role, Rarity.R).FirstOrDefault(c => c.Id.EndsWith(suffix));
                if (r == null) continue;
                var sr = CardLibrary.BuildDeck("x", role, Rarity.SR).First(c => c.Id.EndsWith(suffix));
                var ur = CardLibrary.BuildDeck("x", role, Rarity.UR).First(c => c.Id.EndsWith(suffix));
                Assert.Equal(Efficiency(r) + 1, Efficiency(sr), 1);
                Assert.Equal(Efficiency(r) + 2, Efficiency(ur), 1);
                Assert.Equal(r.Cost, sr.Cost);
                Assert.Equal(r.Cost, ur.Cost);
            }
        }

        [Fact]
        public void BasicAttacks_DoNotChangeWithRarity()
        {
            foreach (Role role in System.Enum.GetValues(typeof(Role)))
            {
                var r = CardLibrary.BuildDeck("x", role, Rarity.R).First(c => c.Basic);
                var ur = CardLibrary.BuildDeck("x", role, Rarity.UR).First(c => c.Basic);
                Assert.Equal(r.Effects[0].Multiplier, ur.Effects[0].Multiplier);
                Assert.Equal(r.Cost, ur.Cost);
            }
        }
    }
}
