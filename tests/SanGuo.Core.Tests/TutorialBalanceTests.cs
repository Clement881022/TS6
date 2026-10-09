using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SanGuo.Core.Tests
{
    public class TutorialBalanceTests
    {
        private readonly ITestOutputHelper _out;
        public TutorialBalanceTests(ITestOutputHelper output) { _out = output; }

        private sealed class Bot
        {
            public bool UseSpecials;

            public void PlayTurn(Battle b)
            {
                for (int guard = 0; guard < 60 && b.Result == BattleResult.Ongoing; guard++)
                {
                    if (PlayOne(b)) continue;
                    var move = b.Hand.FirstOrDefault(c => c.Def.Target == TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok);
                    var advancer = move == null ? null : b.AliveUnits(Side.Player)
                        .Where(h => !h.Protected && h.Hero != null && b.CanMoveUnit(h) && AutoPlayer.ChooseMove(b, h) != null)
                        .OrderBy(h => h.Hero!.Role == Role.Tank ? 1 : 0).FirstOrDefault();
                    if (move != null && advancer != null) { b.PlayCard(move, AutoPlayer.ChooseMove(b, advancer), advancer); continue; }
                    break;
                }
                b.EndTurn();
            }

            private bool PlayOne(Battle b)
            {
                var options = b.Hand.Where(c => c.Def.Target != TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok)
                    .Where(c => UseSpecials || c.Def.Basic)
                    .OrderByDescending(c => c.Def.Basic ? 1 : 2)
                    .ToList();
                foreach (var c in options)
                {
                    var owner = c.Owner!;
                    var effect = c.Def.Effects[0];
                    if (c.Def.Target == TargetRule.Ally)
                    {
                        var hurt = b.AliveUnits(Side.Player).Any(u => u.Hp < u.MaxHp * 0.75);
                        if (!hurt && effect.Type == EffectType.Heal) continue;
                        if (b.ResolveTargets(owner, c.Def) == null) continue;
                        b.PlayCard(c);
                        return true;
                    }
                    if (c.Def.Target == TargetRule.Enemy)
                    {
                        var inRange = b.AliveUnits(Side.Enemy).Where(e => Position.Distance(owner.Pos, e.Pos) <= owner.AttackRange).ToList();
                        if (inRange.Count == 0) continue;
                        Unit pick;
                        bool debuff = c.Def.Effects.Any(e => e.Status == StatusType.ArmorBreak || e.Status == StatusType.Burn);
                        if (debuff) pick = inRange.OrderByDescending(e => e.EffectiveDef).ThenByDescending(e => e.Hp).First();
                        else pick = inRange.OrderByDescending(e => e.Stats.Range > 1 && e.Stats.Hp < 350 ? 1 : 0).ThenBy(e => e.Hp).First();
                        if (c.Def.Shape == Shape.Row3)
                            pick = inRange.OrderByDescending(e => b.AliveUnits(Side.Enemy).Count(o => o.Pos.Row == e.Pos.Row && System.Math.Abs(o.Pos.Lane - e.Pos.Lane) <= 1)).First();
                        if (b.PlayCard(c, pick.Pos) == PlayResult.Ok) return true;
                        continue;
                    }
                    if (c.Def.Target == TargetRule.AllEnemies && effect.Status == StatusType.Taunt)
                    {
                        var foes = b.AliveUnits(Side.Enemy);
                        if (!foes.Any(e => !e.Statuses.ContainsKey(StatusType.Taunt) || e.Charging)) continue;
                    }
                    if (c.Def.Target == TargetRule.AllAllies && b.AliveUnits(Side.Enemy).Count == 0) continue;
                    if (b.PlayCard(c) == PlayResult.Ok) return true;
                }
                return false;
            }
        }

        private static readonly Bot FollowTutorial = new Bot { UseSpecials = true };
        private static readonly Bot IgnoreTutorial = new Bot { UseSpecials = false };

        private static BattleResult Run(Battle b, Bot bot)
        {
            for (int i = 0; i < 40 && b.Result == BattleResult.Ongoing; i++) bot.PlayTurn(b);
            return b.Result;
        }

        private double WinRate(int level, Bot bot, int runs = 20)
        {
            int wins = 0;
            for (ulong seed = 1; seed <= (ulong)runs; seed++)
            {
                var battle = new Battle(DemoContent.Level(level, seed));
                if (Run(battle, bot) == BattleResult.Won) wins++;
            }
            return 100.0 * wins / runs;
        }

        [Fact]
        public void Report()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                var follow = WinRate(level, FollowTutorial);
                var ignore = WinRate(level, IgnoreTutorial);
                var b = new Battle(DemoContent.Level(level, 1));
                Run(b, FollowTutorial);
                _out.WriteLine($"關 {level} {DemoContent.LevelNames[level - 1]}: 照教學 {follow:F0}%（{b.Turn} 回合）／無視教學 {ignore:F0}%");
            }
        }

        [Fact]
        public void EveryTutorialLevel_IsWinnableWhenFollowingTheTutorial()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                Assert.True(WinRate(level, FollowTutorial) >= 75, $"第 {level} 關照教學打應該能過");
        }

        [Fact]
        public void KeyLevels_FailWhenTheTutorialMechanicIsIgnored()
        {
            foreach (var level in new[] { 3, 10 })
                Assert.True(WinRate(level, IgnoreTutorial) <= 30, $"第 {level} 關無視教學應該失敗");
        }

        [Fact]
        public void FirstLevel_IsEasy()
        {
            Assert.True(WinRate(1, IgnoreTutorial) >= 90);
        }
    }
}
