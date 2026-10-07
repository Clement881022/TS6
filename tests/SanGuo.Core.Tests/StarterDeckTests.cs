using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>初始牌庫規則：每名武將同樣厚度；稀有度只決定幾張普通攻擊被高級牌取代。</summary>
    public class StarterDeckTests
    {
        private static bool IsAttack(CardDef c) => c.Id.EndsWith("_attack");

        private static readonly Dictionary<Role, string[]> Skills = new Dictionary<Role, string[]>
        {
            [Role.Tank] = new[] { "防禦姿態", "嘲諷" },
            [Role.Warrior] = new[] { "旋風斬", "豎劈斬" },
            [Role.Healer] = new[] { "治療", "上盾" },
            [Role.Strategist] = new[] { "攻擊鼓舞", "暴擊鼓舞" },
            [Role.Archer] = new[] { "狙擊", "破甲箭" },
            [Role.Mage] = new[] { "火計", "火燒連營" },
        };

        public static IEnumerable<object[]> RoleAndRarity() =>
            from role in System.Enum.GetValues(typeof(Role)).Cast<Role>()
            from rarity in System.Enum.GetValues(typeof(Rarity)).Cast<Rarity>()
            select new object[] { role, rarity };

        [Theory]
        [MemberData(nameof(RoleAndRarity))]
        public void Deck_HasFixedSize_AndRarityDecidesHowManyAttacksAreReplaced(Role role, Rarity rarity)
        {
            var deck = DemoContent.BuildDeck("t", role, rarity);
            int unique = rarity == Rarity.UR ? 2 : rarity == Rarity.SR ? 1 : 0;

            Assert.Equal(DemoContent.DeckSize, deck.Count);
            Assert.Equal(DemoContent.DeckSize - unique, deck.Count(IsAttack));
            // SR 拿職業的第 1 張高級牌，UR 兩張都拿。
            Assert.Equal(Skills[role].Take(unique), deck.Where(c => !IsAttack(c)).Select(c => c.Name));
            Assert.All(deck.Where(IsAttack), c => Assert.Equal("普通攻擊", c.Name));
        }

        [Fact]
        public void EveryRosterHero_HasSameThickness_AndLowRarityIsAllBasicAttacks()
        {
            foreach (var hero in DemoContent.Roster())
            {
                Assert.Equal(DemoContent.DeckSize, hero.Deck.Count);
                int unique = hero.Deck.Count(c => !IsAttack(c));
                Assert.Equal(hero.Rarity == Rarity.UR ? 2 : hero.Rarity == Rarity.SR ? 1 : 0, unique);
            }
        }

        [Fact]
        public void EveryRole_IsRepresentedInTheRoster()
        {
            var roles = DemoContent.Roster().Where(h => h.Rarity == Rarity.UR).Select(h => h.Role).Distinct();
            Assert.Equal(System.Enum.GetValues(typeof(Role)).Length, roles.Count());
        }

        // ---- 新卡牌的引擎行為 ----

        private static Battle Fight(HeroDef hero, params EnemySlot[] enemies)
        {
            var setup = new BattleSetup { NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(hero, new Position(2, 0)));
            setup.Enemies.AddRange(enemies);
            return new Battle(setup);
        }

        private static HeroDef Hero(Role role, Rarity rarity = Rarity.UR, int atk = 100, int def = 0) => new HeroDef
        {
            Id = "t", Name = "t", Role = role, Rarity = rarity, Base = new Stats { Hp = 5000, Atk = atk, Def = def, Crit = 0 },
            Deck = DemoContent.BuildDeck("t", role, rarity),
        };

        private static EnemySlot Foe(string name, int lane, int row, int hp = 1000, int def = 0, int atk = 1) =>
            new EnemySlot(new EnemyDef { Id = name, Name = name, Base = new Stats { Hp = hp, Atk = atk, Def = def } }, new Position(lane, row));

        private static CardInstance Card(Battle b, string name) => b.Hand.Concat(b.DrawPile).First(c => c.Def.Name == name);

        private static void ToHand(Battle b, CardInstance c)
        {
            if (!b.Hand.Contains(c)) { b.DrawPile.Remove(c); b.Hand.Add(c); }
        }

        private static Unit Enemy(Battle b, string name) => b.Units.First(u => u.Side == Side.Enemy && u.Name == name);

        [Fact]
        public void Sweep_HitsTheFrontRow_AndCleave_HitsTheLane()
        {
            var b = Fight(Hero(Role.Warrior), Foe("a", 1, 0), Foe("b", 2, 0), Foe("c", 3, 0), Foe("back", 2, 1));
            var sweep = Card(b, "旋風斬");
            var cleave = Card(b, "豎劈斬");

            Assert.Equal(new[] { "a", "b", "c" }, b.ResolveTargets(sweep.Owner, sweep.Def)!.Select(u => u.Name).OrderBy(n => n));
            Assert.Equal(new[] { "b", "back" }, b.ResolveTargets(cleave.Owner, cleave.Def)!.Select(u => u.Name).OrderBy(n => n));
            Assert.Equal(1.0, sweep.Def.Effects[0].Multiplier);
            Assert.Equal(1.5, cleave.Def.Effects[0].Multiplier);
        }

        [Fact]
        public void Snipe_TargetsTheLowestHpEnemy_WhereverItStands()
        {
            var b = Fight(Hero(Role.Archer), Foe("tank", 2, 0, hp: 900), Foe("weak", 4, 1, hp: 200), Foe("mid", 0, 0, hp: 500));
            var snipe = Card(b, "狙擊");

            var targets = b.ResolveTargets(snipe.Owner, snipe.Def)!;
            Assert.Equal("weak", Assert.Single(targets).Name);

            Enemy(b, "mid").Hp = 100;
            Assert.Equal("mid", b.ResolveTargets(snipe.Owner, snipe.Def)!.Single().Name);
        }

        [Fact]
        public void PierceArrow_UsesTheChosenEnemy_AndFallsBackToBackRow()
        {
            var b = Fight(Hero(Role.Archer), Foe("front", 2, 0), Foe("back", 4, 1));
            var pierce = Card(b, "破甲箭");
            ToHand(b, pierce);

            Assert.Equal("back", b.ResolveTargets(pierce.Owner, pierce.Def)!.Single().Name); // 沒指定 → 後排優先
            Assert.Equal("front", b.ResolveTargets(pierce.Owner, pierce.Def, Enemy(b, "front"))!.Single().Name);

            Assert.Equal(PlayResult.Ok, b.PlayCard(pierce, Enemy(b, "front")));
            Assert.Single(Enemy(b, "front").DefBreaks);
            Assert.Empty(Enemy(b, "back").DefBreaks);
            Assert.True(Enemy(b, "front").Hp < 1000);
        }

        [Fact]
        public void DefenseStance_RaisesOwnDefense_ForTwoTurns()
        {
            var b = Fight(Hero(Role.Tank, def: 100), Foe("e", 2, 0, atk: 100));
            var stance = Card(b, "防禦姿態");
            var tank = stance.Owner;
            Assert.Equal(100, tank.EffectiveDef);

            ToHand(b, stance);
            b.PlayCard(stance);
            Assert.Equal(200, tank.EffectiveDef);          // +100%
            Assert.Equal(2, tank.Statuses[StatusType.DefUp].Turns);

            b.EndTurn();
            Assert.Equal(200, tank.EffectiveDef);          // 下一回合仍有效
            b.EndTurn();
            Assert.Equal(100, tank.EffectiveDef);          // 兩回合後消失
        }

        [Fact]
        public void Taunt_AddsSmallerDefenseThanStance_AndTheLargerBuffWins()
        {
            var b = Fight(Hero(Role.Tank, def: 100), Foe("e", 2, 0));
            var taunt = Card(b, "嘲諷");
            var stance = Card(b, "防禦姿態");
            var tank = taunt.Owner;
            ToHand(b, taunt); ToHand(b, stance);

            b.PlayCard(taunt);
            Assert.True(tank.Has(StatusType.Taunt));
            Assert.Equal(130, tank.EffectiveDef);          // +30%
            b.PlayCard(stance);
            Assert.Equal(200, tank.EffectiveDef);          // 取較大加成，不疊加
            Assert.Equal(3, tank.Statuses[StatusType.DefUp].Turns); // 取較長回合
        }

        [Fact]
        public void StrategistBuffs_ApplyToTheWholeTeam_AndRaiseDamageAndCrit()
        {
            var setup = new BattleSetup { NoRandomness = false };
            var strategist = Hero(Role.Strategist);
            setup.Heroes.Add(new HeroSlot(strategist, new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero(Role.Warrior, atk: 200), new Position(2, 0)));
            setup.Enemies.Add(Foe("e", 2, 0, hp: 100000));
            var b = new Battle(setup);
            var warrior = b.AliveUnits(Side.Player).First(u => u.Pos.Lane == 2);

            var atkUp = Card(b, "攻擊鼓舞");
            var critUp = Card(b, "暴擊鼓舞");
            ToHand(b, atkUp); ToHand(b, critUp);
            Assert.Equal(200, warrior.EffectiveAtk);

            b.PlayCard(atkUp);
            Assert.All(b.AliveUnits(Side.Player), u => Assert.True(u.Has(StatusType.AtkUp)));
            Assert.Equal(260, warrior.EffectiveAtk);       // +30%

            b.PlayCard(critUp);
            Assert.All(b.AliveUnits(Side.Player), u => Assert.Equal(25, u.EffectiveCrit)); // 基礎 0 + 25
        }

        [Fact]
        public void AtkUp_ScalesCardDamage()
        {
            var b = Fight(Hero(Role.Strategist, atk: 100), Foe("e", 2, 0, hp: 100000, def: 0));
            var enemy = Enemy(b, "e");
            var attack = b.Hand.First(c => IsAttack(c.Def));
            var atkUp = Card(b, "攻擊鼓舞");
            ToHand(b, atkUp);

            b.PlayCard(attack);
            int before = 100000 - enemy.Hp;                 // 100 × 1.0
            b.PlayCard(atkUp);
            var attack2 = b.Hand.First(c => IsAttack(c.Def));
            int hp = enemy.Hp;
            b.PlayCard(attack2);

            Assert.Equal(100, before);
            Assert.Equal(130, hp - enemy.Hp);               // 攻擊 +30%
        }

        [Fact]
        public void Mage_FireThenInferno_BurnsAndDetonates()
        {
            var b = Fight(Hero(Role.Mage), Foe("a", 1, 0, hp: 5000), Foe("b", 2, 0, hp: 5000), Foe("c", 3, 0, hp: 5000));
            var fire = Card(b, "火計");
            var inferno = Card(b, "火燒連營");
            ToHand(b, fire); ToHand(b, inferno);

            b.PlayCard(fire);
            Assert.True(Enemy(b, "b").Has(StatusType.Burn));
            b.PlayCard(inferno);
            // 火計 70（0.7 × 100）+ 引爆剩餘燒傷 100 × 3 回合 = 370。
            Assert.Equal(5000 - 370, Enemy(b, "b").Hp);
            Assert.Equal(5000 - 370, Enemy(b, "a").Hp);
        }

        private static BattleSetup PierceSetup()
        {
            var setup = new BattleSetup { NoRandomness = true, ScriptedDraw = new List<string> { "t_pierce" } };
            setup.Heroes.Add(new HeroSlot(Hero(Role.Archer), new Position(2, 0)));
            setup.Enemies.Add(Foe("front", 2, 0));
            setup.Enemies.Add(Foe("back", 4, 1));
            return setup;
        }

        [Fact]
        public void ChosenTarget_IsRecordedInReplay_AndReproducedByVerifier()
        {
            var rec = new Data.ReplayRecorder(new Battle(PierceSetup()));
            var pierce = rec.Battle.Hand.First(c => c.Def.Name == "破甲箭");
            var front = Enemy(rec.Battle, "front");

            Assert.Equal(PlayResult.Ok, rec.Play(pierce, front));
            Assert.Equal(front.Id, rec.Actions.Single().TargetId);

            var result = Data.ReplayVerifier.Verify(PierceSetup(), rec.Actions);
            Assert.True(result.Valid, result.Error);
            Assert.Single(Enemy(result.Battle!, "front").DefBreaks);
            Assert.Empty(Enemy(result.Battle!, "back").DefBreaks);
        }

        [Fact]
        public void Replay_RejectsAnInvalidChosenTarget()
        {
            var pierce = new Battle(PierceSetup()).Hand.First(c => c.Def.Name == "破甲箭");
            var bad = new[] { Data.ReplayAction.Play(pierce.Id, targetId: 0) }; // 0 號是我方武將
            Assert.False(Data.ReplayVerifier.Verify(PierceSetup(), bad).Valid);
        }

        [Fact]
        public void BuffStatuses_RoundTripThroughJson()
        {
            var json = Data.ContentSerializer.HeroesToJson(new[] { Hero(Role.Strategist), Hero(Role.Archer) });
            var heroes = Data.ContentSerializer.HeroesFromJson(json);
            Assert.Contains(heroes[0].Deck, c => c.Effects.Any(e => e.Status == StatusType.CritUp));
            Assert.Contains(heroes[1].Deck, c => c.Target == TargetRule.EnemyLowestHp);
            Assert.Contains(heroes[1].Deck, c => c.Target == TargetRule.EnemyAny);
            Assert.Equal(json, Data.ContentSerializer.HeroesToJson(heroes));
        }
    }
}
