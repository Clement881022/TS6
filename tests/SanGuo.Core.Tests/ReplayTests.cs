using System.Linq;
using SanGuo.Core.Data;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class ReplayTests
    {
        private static ReplayRecorder RecordAuto(int level, ulong seed = 1)
        {
            var rec = new ReplayRecorder(new Battle(DemoContent.Level(level, seed)));
            for (int i = 0; i < 100 && rec.Battle.Result == BattleResult.Ongoing; i++) rec.PlayAuto();
            return rec;
        }

        [Fact]
        public void RecordedBattle_ReplaysToIdenticalResult()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                var rec = RecordAuto(level);
                var replay = ReplayVerifier.Verify(DemoContent.Level(level, 1), rec.Actions);
                Assert.True(replay.Valid, $"第 {level} 關：{replay.Error}");
                Assert.Equal(rec.Battle.Result, replay.Result);
                Assert.Equal(rec.Battle.Turn, replay.Turns);
            }
        }

        [Fact]
        public void UnknownCard_IsInvalid()
        {
            var r = ReplayVerifier.Verify(DemoContent.Level(1, 1), new[] { ReplayAction.Play(99999) });
            Assert.False(r.Valid);
        }

        [Fact]
        public void ActionsAfterBattleEnded_AreInvalid()
        {
            var rec = RecordAuto(1);
            Assert.Equal(BattleResult.Won, rec.Battle.Result);
            var tampered = rec.Actions.Concat(new[] { ReplayAction.EndTurn() }).ToList();
            Assert.False(ReplayVerifier.Verify(DemoContent.Level(1, 1), tampered).Valid);
        }

        [Fact]
        public void Truncated_IsValidButNotWon()
        {
            var rec = RecordAuto(1);
            var half = rec.Actions.Take(rec.Actions.Count / 2).ToList();
            var r = ReplayVerifier.Verify(DemoContent.Level(1, 1), half);
            Assert.True(r.Valid);
            Assert.False(r.Won);
        }

        [Fact]
        public void EmptyReplay_IsNotWon()
        {
            var r = ReplayVerifier.Verify(DemoContent.Level(1, 1), new ReplayAction[0]);
            Assert.True(r.Valid);
            Assert.False(r.Won);
        }

        [Fact]
        public void TooManyActions_AreRejected()
        {
            var many = Enumerable.Repeat(ReplayAction.EndTurn(), ReplayVerifier.MaxActions + 1).ToList();
            Assert.False(ReplayVerifier.Verify(DemoContent.Level(1, 1), many).Valid);
        }
    }
}
