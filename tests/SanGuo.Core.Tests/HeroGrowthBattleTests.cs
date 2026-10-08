using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>成長結果帶進實際戰鬥：屬性、套牌升級、裝備都要反映在戰鬥單位上，且不重複套用等級。</summary>
    public class HeroGrowthBattleTests
    {
        private static Battle Fight(HeroDef def, HeroState state)
        {
            var setup = new BattleSetup { NoRandomness = true };
            setup.Heroes.Add(HeroGrowth.BuildSlot(def, state, new Position(2, 3)));
            setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), new Position(2, 1)));
            return new Battle(setup);
        }

        [Fact]
        public void BuildSlot_InBattle_UnitHasScaledStats_AndNoDoubleLevelScaling()
        {
            var zf = HeroRoster.ZhangFei();
            var state = new HeroState { HeroId = "zhangfei", Level = 10, Stars = 1 };
            state.Equipment["Armor"] = 3;
            var battle = Fight(zf, state);

            var unit = battle.Units.First(u => u.Side == Side.Player);
            // 等級 ×1.135、一突（坦克主屬性生命）×1.1、防具三階 ×1.3
            Assert.Equal((int)System.Math.Round(zf.Base.Hp * 1.135 * 1.1 * 1.3), unit.MaxHp);
            Assert.Equal((int)System.Math.Round(zf.Base.Def * 1.135 * 1.3), unit.Stats.Def);
        }

        [Fact]
        public void UpgradedTaunt_LastsOneTurnLonger_InBattle()
        {
            var zf = HeroRoster.ZhangFei();
            var battle = Fight(zf, new HeroState { HeroId = "zhangfei", Stars = 2 });
            var plus = battle.Hand.Concat(battle.DrawPile).First(c => c.Def.Id == "zf_taunt_plus");
            battle.Hand.Add(plus);
            battle.DrawPile.Remove(plus);
            Assert.Equal(PlayResult.Ok, battle.PlayCard(plus));
            var enemy = battle.Units.First(u => u.Side == Side.Enemy);
            Assert.Equal(3, enemy.Statuses[StatusType.Taunt].Turns); // SR 嘲諷基礎 2 回合，升級後 +1
        }

        [Fact]
        public void GrownTeam_BeatsTheSameFightMoreOftenThanAFreshOne()
        {
            int Wins(int level, int stars)
            {
                int wins = 0;
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    var setup = DemoMeta.DungeonSetup("res_2", seed);
                    foreach (var (id, pos) in new[] { ("r_shield", new Position(1, 3)), ("r_sword", new Position(2, 3)), ("r_archer", new Position(2, 4)), ("r_healer", new Position(3, 4)) })
                        setup.Heroes.Add(HeroGrowth.BuildSlot(HeroRoster.Find(id)!, new HeroState { HeroId = id, Level = level, Stars = stars }, pos));
                    var battle = new Battle(setup);
                    AutoPlayer.RunToEnd(battle, 60);
                    if (battle.Result == BattleResult.Won) wins++;
                }
                return wins;
            }
            Assert.True(Wins(30, 5) > Wins(1, 0));
        }
    }
}
