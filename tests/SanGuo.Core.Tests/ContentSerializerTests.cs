using System.Linq;
using SanGuo.Core.Data;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class ContentSerializerTests
    {
        [Fact]
        public void EveryChapter1Level_RoundTripsToIdenticalJson()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                string json = ContentSerializer.SetupToJson(DemoContent.Level(level));
                var loaded = ContentSerializer.SetupFromJson(json);
                Assert.Equal(json, ContentSerializer.SetupToJson(loaded));
            }
        }

        [Fact]
        public void LoadedLevels_PlayOutIdenticallyToCode()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    var original = new Battle(DemoContent.Level(level, seed));
                    var fromJson = new Battle(ContentSerializer.SetupFromJson(
                        ContentSerializer.SetupToJson(DemoContent.Level(level, seed))));

                    var r1 = AutoPlayer.RunToEnd(original, 60);
                    var r2 = AutoPlayer.RunToEnd(fromJson, 60);

                    Assert.Equal(r1, r2);
                    Assert.Equal(original.Turn, fromJson.Turn);
                    Assert.Equal(original.Units.Sum(u => u.Hp), fromJson.Units.Sum(u => u.Hp));
                }
            }
        }

        [Fact]
        public void HeroRoster_RoundTrips()
        {
            string json = ContentSerializer.HeroesToJson(DemoContent.Roster());
            var heroes = ContentSerializer.HeroesFromJson(json);
            Assert.Equal(DemoContent.Roster().Count, heroes.Count);
            Assert.Equal(json, ContentSerializer.HeroesToJson(heroes));
            var zf = heroes.First(h => h.Id == "zhangfei");
            var taunt = zf.Deck.First(c => c.Id == "zf_yanren");
            Assert.Equal(TargetRule.AllEnemies, taunt.Target);
            Assert.Equal(StatusType.Taunt, taunt.Effects[0].Status);
            Assert.Equal(PassiveKind.ChangBan, zf.Passive);
            var xhd = heroes.First(h => h.Id == "xiahoudun");
            Assert.True(xhd.Deck.First(c => c.Id == "xhd_ganglie").Unlimited);
            Assert.Equal(1, heroes.First(h => h.Id == "gongsunzan").Deck.First(c => c.Id == "gsz_youqi").KillRefund);
        }

        [Fact]
        public void UnknownEnumName_IsRejected()
        {
            const string json = "{\"heroes\":[{\"id\":\"x\",\"role\":\"Wizard\"}]}";
            Assert.Throws<System.FormatException>(() => ContentSerializer.HeroesFromJson(json));
        }
    }
}
