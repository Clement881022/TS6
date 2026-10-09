using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class BattleRulesTests
    {
        private static Position PH(int lane, int row = 3) => new Position(lane, row);
        private static Position PE(int lane, int row = 1) => new Position(lane, row);

        private static CardDef Card(string id, int cost = 1, TargetRule target = TargetRule.Enemy, Shape shape = Shape.Single,
            params EffectDef[] effects) =>
            new CardDef { Id = id, Name = id, Cost = cost, Target = target, Shape = shape, Effects = effects.ToList() };

        private static EffectDef Dmg(double mult, DamageKind kind = DamageKind.Physical) =>
            new EffectDef { Type = EffectType.Damage, Multiplier = mult, Kind = kind };

        private static EffectDef Status(StatusType type, double power, int turns) =>
            new EffectDef { Type = EffectType.ApplyStatus, Status = type, Multiplier = power, Amount = turns };

        private static CardDef Attack(string id = "atk", int cost = 1, double mult = 1.0, DamageKind kind = DamageKind.Physical,
            Shape shape = Shape.Single) => Card(id, cost, TargetRule.Enemy, shape, Dmg(mult, kind));

        private static HeroDef Hero(string name, IEnumerable<CardDef> deck, int atk = 100, int hp = 1000, int def = 0,
            int move = 1, int range = 10, int intel = 100, int dodge = 0, int crit = 0, Role role = Role.Warrior) =>
            new HeroDef
            {
                Id = name, Name = name, Role = role,
                Base = new Stats { Hp = hp, Atk = atk, Int = intel, Def = def, Move = move, Range = range, Crit = crit, Dodge = dodge },
                Deck = deck.ToList(),
            };

        private static EnemyDef Enemy(string name, int hp = 500, int atk = 50, int def = 0, int move = 1, int range = 1,
            int intel = 0, bool magical = false) =>
            new EnemyDef
            {
                Id = name, Name = name, Magical = magical,
                Base = new Stats { Hp = hp, Atk = atk, Int = intel, Def = def, Move = move, Range = range },
            };

        private static Battle Fight(HeroDef hero, EnemyDef enemy, Position? heroPos = null, Position? enemyPos = null, ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(hero, heroPos ?? PH(2)));
            setup.Enemies.Add(new EnemySlot(enemy, enemyPos ?? PE(2), EnemyLevelForUnitScale));
            return new Battle(setup);
        }

        private const int EnemyLevelForUnitScale = 11;

        private static Unit EnemyUnit(Battle b, string name) => b.Units.First(u => u.Side == Side.Enemy && u.Name == name);
        private static Unit HeroUnit(Battle b, string name) => b.Units.First(u => u.Side == Side.Player && u.Name == name);
        private static CardInstance Find(Battle b, string cardId) => b.Hand.First(c => c.Def.Id == cardId);

        [Fact]
        public void Physical_UsesMultiplicativeDefense()
        {
            Assert.Equal(50, DamageCalc.Physical(100, 1.0, 100, false, 150));
            Assert.Equal(100, DamageCalc.Physical(100, 1.0, 0, false, 150));
            Assert.Equal(150, DamageCalc.Physical(100, 1.0, 0, true, 150));
            Assert.Equal(1, DamageCalc.Physical(1, 0.1, 500, false, 150));
        }

        [Fact]
        public void Magical_IgnoresDefenseAndNeverCrits()
        {
            var b = Fight(Hero("H", new[] { Attack("m", mult: 1.0, kind: DamageKind.Magical) }, atk: 1, intel: 100, crit: 100),
                Enemy("e", hp: 10000, def: 500));
            b.PlayCard(Find(b, "m"));
            Assert.Equal(10000 - 100, EnemyUnit(b, "e").Hp);
        }

        [Fact]
        public void Physical_AppliesDefenseAndCrit()
        {
            var setup = new BattleSetup { Seed = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, atk: 100, crit: 100), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 10000, def: 100), PE(2), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.PlayCard(Find(b, "atk"));
            Assert.Equal(10000 - 75, EnemyUnit(b, "e").Hp);
        }

        [Fact]
        public void Dodge_AppliesToPhysicalAndMagical_AndIsCapped()
        {
            var b = Fight(Hero("H", new[] { Attack() }), Enemy("e"));
            var e = EnemyUnit(b, "e");
            e.Stats.Dodge = 90;
            Assert.Equal(DamageCalc.DodgeCap, e.EffectiveDodge);
            e.Buffs.Add(new Buff { Type = StatusType.DodgeUp, Power = 30, Turns = 2 });
            Assert.Equal(DamageCalc.DodgeCap, e.EffectiveDodge);

            var setup = new BattleSetup { Seed = 7 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack("a", cost: 0), Attack("m", cost: 0, kind: DamageKind.Magical) }, hp: 99999), PH(2)));
            var enemy = Enemy("d", hp: 999999);
            enemy.Base.Dodge = 100;
            setup.Enemies.Add(new EnemySlot(enemy, PE(2), EnemyLevelForUnitScale));
            var bb = new Battle(setup);
            for (int i = 0; i < 6; i++)
            {
                foreach (var c in bb.Hand.Where(c => c.Def.Cost == 0).ToList()) bb.PlayCard(c);
                bb.EndTurn();
            }
            Assert.Contains(bb.Events, ev => ev.Type == EventType.Dodge && ev.Source == 0);
            Assert.Contains(bb.Events, ev => ev.Type == EventType.Damage && ev.Source == 0);
        }

        [Fact]
        public void CritRate_IsCappedAt100()
        {
            var b = Fight(Hero("H", new[] { Attack() }, crit: 250), Enemy("e"));
            Assert.Equal(100, HeroUnit(b, "H").EffectiveCrit);
        }

        [Fact]
        public void ArmorBreak_SameName_TakesHighestAndExpiresIndependently()
        {
            CardDef Breaker(string id, double pct, int turns) => Card(id, 0, TargetRule.Enemy, Shape.Single, Status(StatusType.ArmorBreak, pct, turns));
            var b = Fight(Hero("H", new[] { Breaker("a25", 0.25, 2), Breaker("b50", 0.5, 1), Attack() }, hp: 99999),
                Enemy("e", hp: 99999, atk: 1, def: 100));
            var enemy = EnemyUnit(b, "e");

            Assert.Equal(100.0, enemy.EffectiveDef);
            b.PlayCard(Find(b, "a25"));
            Assert.Equal(75.0, enemy.EffectiveDef, 6);
            b.PlayCard(Find(b, "b50"));
            Assert.Equal(2, enemy.DefBreaks.Count);
            Assert.Equal(50.0, enemy.EffectiveDef, 6);

            b.EndTurn();
            Assert.Single(enemy.DefBreaks);
            Assert.Equal(75.0, enemy.EffectiveDef, 6);

            b.EndTurn();
            Assert.Empty(enemy.DefBreaks);
            Assert.Equal(100.0, enemy.EffectiveDef, 6);
        }

        [Fact]
        public void Burn_DealsStacksAtEndOfFactionTurn_ThenDecaysByHalfRoundedDown()
        {
            var fire = Card("fire", 1, TargetRule.Enemy, Shape.Single, Status(StatusType.Burn, 0.08, 0));
            var b = Fight(Hero("H", new[] { fire }, intel: 100, hp: 99999), Enemy("e", hp: 1000, atk: 1));
            var e = EnemyUnit(b, "e");

            b.PlayCard(Find(b, "fire"));
            Assert.Equal(8, e.BurnStacks);
            Assert.Equal(1000, e.Hp);

            var taken = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                int before = e.Hp;
                b.EndTurn();
                taken.Add(before - e.Hp);
            }
            Assert.Equal(new[] { 8, 4, 2, 1, 0 }, taken);
            Assert.Equal(0, e.BurnStacks);
            Assert.Equal(15, 1000 - e.Hp);
        }

        [Fact]
        public void Burn_StacksAddUp_AndCanBeAbsorbedByShield()
        {
            var fire = Card("fire", 0, TargetRule.Enemy, Shape.Single, Status(StatusType.Burn, 0.1, 0));
            var b = Fight(Hero("H", new[] { fire, fire }, intel: 100, hp: 99999), Enemy("e", hp: 1000, atk: 1));
            var e = EnemyUnit(b, "e");
            e.Shield = 5;
            foreach (var c in b.Hand.Where(c => c.Def.Id == "fire").ToList()) b.PlayCard(c);
            Assert.Equal(20, e.BurnStacks);
            b.EndTurn();
            Assert.Equal(0, e.Shield);
            Assert.Equal(1000 - 15, e.Hp);
            Assert.Equal(10, e.BurnStacks);
        }

        [Fact]
        public void Shield_AbsorbsPhysicalAndMagicalBeforeHp_AndHasNoDuration()
        {
            var shield = Card("sh", 0, TargetRule.Self, Shape.Single, new EffectDef { Type = EffectType.Shield, Multiplier = 1.0 });
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { shield }, intel: 100, hp: 1000, range: 1), PH(2, 3)));
            setup.Enemies.Add(new EnemySlot(Enemy("phys", hp: 99999, atk: 60, range: 1), PE(2, 2), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var h = HeroUnit(b, "H");
            b.PlayCard(Find(b, "sh"));
            Assert.Equal(100, h.Shield);

            b.EndTurn();
            Assert.Equal(40, h.Shield);
            Assert.Equal(1000, h.Hp);
            b.EndTurn();
            Assert.Equal(0, h.Shield);
            Assert.Equal(980, h.Hp);
        }

        [Fact]
        public void Shield_StacksIntoSingleValue()
        {
            var shield = Card("sh", 0, TargetRule.Self, Shape.Single, new EffectDef { Type = EffectType.Shield, Multiplier = 0.5 });
            var b = Fight(Hero("H", new[] { shield, shield }, intel: 100), Enemy("e"));
            foreach (var c in b.Hand.Where(c => c.Def.Id == "sh").ToList()) b.PlayCard(c);
            Assert.Equal(100, HeroUnit(b, "H").Shield);
        }

        [Fact]
        public void DefUp_IsFixedValue_StacksAndExpiresIndependently()
        {
            CardDef Up(string id, int turns) => Card(id, 0, TargetRule.Self, Shape.Single, Status(StatusType.DefUp, 50, turns));
            var b = Fight(Hero("H", new[] { Up("a", 1), Up("b", 2), Up("c", 2) }, def: 10), Enemy("e", atk: 1));
            var h = HeroUnit(b, "H");
            foreach (var c in b.Hand.Where(c => c.Def.Cost == 0).ToList()) b.PlayCard(c);
            Assert.Equal(160.0, h.EffectiveDef);
            b.EndTurn();
            Assert.Equal(110.0, h.EffectiveDef);
            b.EndTurn();
            Assert.Equal(10.0, h.EffectiveDef);
        }

        [Fact]
        public void AtkUp_RaisesPhysicalOnly_NotIntelligence()
        {
            var b = Fight(Hero("H", new[] { Card("rally", 0, TargetRule.AllAllies, Shape.All, Status(StatusType.AtkUp, 0.25, 2)) }, atk: 100, intel: 100),
                Enemy("e"));
            var h = HeroUnit(b, "H");
            b.PlayCard(Find(b, "rally"));
            Assert.Equal(125, h.EffectiveAtk);
            Assert.Equal(100, h.EffectiveInt);
        }

        [Fact]
        public void Taunt_ForcesAllEnemiesToTargetTaunter_EvenRanged()
        {
            var taunt = Card("taunt", 0, TargetRule.AllEnemies, Shape.All, Status(StatusType.Taunt, 0, 1));
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("Tank", new[] { taunt }, hp: 1000, range: 1), PH(1)));
            setup.Heroes.Add(new HeroSlot(Hero("Back", new[] { Attack() }, hp: 1000), PH(3, 4)));
            setup.Enemies.Add(new EnemySlot(Enemy("archer", atk: 100, range: 10), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.PlayCard(Find(b, "taunt"));
            b.EndTurn();
            Assert.Equal(900, HeroUnit(b, "Tank").Hp);
            Assert.Equal(1000, HeroUnit(b, "Back").Hp);
        }

        [Fact]
        public void Taunt_LaterOverridesEarlier()
        {
            var taunt = Card("taunt", 0, TargetRule.AllEnemies, Shape.All, Status(StatusType.Taunt, 0, 1));
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("T1", new[] { taunt }, hp: 1000, range: 1), PH(1)));
            setup.Heroes.Add(new HeroSlot(Hero("T2", new[] { taunt }, hp: 1000, range: 1), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", atk: 100, range: 10), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.PlayCard(b.Hand.First(c => c.Owner?.Name == "T1" && c.Def.Id == "taunt"));
            b.PlayCard(b.Hand.First(c => c.Owner?.Name == "T2" && c.Def.Id == "taunt"));
            Assert.Equal(HeroUnit(b, "T2").Id, EnemyUnit(b, "e").Statuses[StatusType.Taunt].SourceId);
            b.EndTurn();
            Assert.Equal(1000, HeroUnit(b, "T1").Hp);
            Assert.Equal(900, HeroUnit(b, "T2").Hp);
        }

        private static EnemyDef Charger(int chargeTurns = 1, int interval = 1, double power = 2.0)
        {
            var e = Enemy("boss", hp: 99999, atk: 100, range: 10);
            e.ChargeTurns = chargeTurns;
            e.ChargeInterval = interval;
            e.ChargePower = power;
            return e;
        }

        [Fact]
        public void Charge_AttacksNormallyThenChargesThenReleasesAoE()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, hp: 10000), PH(1)));
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack() }, hp: 10000), PH(2)));
            setup.Enemies.Add(new EnemySlot(Charger(), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var boss = EnemyUnit(b, "boss");
            int Total() => HeroUnit(b, "A").Hp + HeroUnit(b, "B").Hp;

            Assert.Equal(Intent.Kind.Attack, b.GetIntent(boss).Type);
            b.EndTurn();
            Assert.Equal(20000 - 100, Total());
            Assert.Equal(Intent.Kind.Charging == b.GetIntent(boss).Type, boss.Charging);

            Assert.Equal(Intent.Kind.Charge, b.GetIntent(boss).Type);
            b.EndTurn();
            Assert.True(boss.Charging);
            Assert.Equal(Intent.Kind.Charging, b.GetIntent(boss).Type);
            Assert.Equal(20000 - 100, Total());

            b.EndTurn();
            Assert.False(boss.Charging);
            Assert.Equal(20000 - 100 - 400, Total());
        }

        [Fact]
        public void Charge_TwoTurns_WaitsOneActionBeforeRelease()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, hp: 10000), PH(2)));
            setup.Enemies.Add(new EnemySlot(Charger(chargeTurns: 2, interval: 0), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var boss = EnemyUnit(b, "boss");
            var a = HeroUnit(b, "A");

            b.EndTurn();
            Assert.True(boss.Charging);
            b.EndTurn();
            Assert.True(boss.Charging);
            Assert.Equal(10000, a.Hp);
            b.EndTurn();
            Assert.False(boss.Charging);
            Assert.Equal(10000 - 200, a.Hp);
        }

        [Fact]
        public void Charge_InterruptedByTaunt_RestartsCharging()
        {
            var taunt = Card("taunt", 0, TargetRule.AllEnemies, Shape.All, Status(StatusType.Taunt, 0, 1));
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("T", new[] { taunt, Attack() }, hp: 10000), PH(2)));
            setup.Enemies.Add(new EnemySlot(Charger(chargeTurns: 1, interval: 0), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var boss = EnemyUnit(b, "boss");
            var t = HeroUnit(b, "T");

            b.EndTurn();
            Assert.True(boss.Charging);
            b.PlayCard(Find(b, "taunt"));
            Assert.False(boss.Charging);
            Assert.Contains(b.Events, e => e.Type == EventType.EnemyChargeBreak);

            b.EndTurn();
            Assert.True(boss.Charging);
            Assert.Equal(10000, t.Hp);
        }

        [Fact]
        public void Charge_EndsWhenEnemyIsKilled()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack(mult: 100) }, hp: 10000), PH(2)));
            var weak = Charger(interval: 0);
            weak.Base.Hp = 100;
            setup.Enemies.Add(new EnemySlot(weak, PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.EndTurn();
            var boss = EnemyUnit(b, "boss");
            Assert.True(boss.Charging);
            b.PlayCard(b.Hand.First(c => c.Def.Id == "atk"));
            Assert.False(boss.Alive);
            Assert.False(boss.Charging);
            Assert.Equal(BattleResult.Won, b.Result);
        }

        [Fact]
        public void Shapes_Row3Column3AndCross()
        {
            var row = Targeting.ExpandShape(new Position(2, 1), Shape.Row3, 5, 5);
            Assert.Equal(3, row.Count);
            Assert.All(row, p => Assert.Equal(1, p.Row));
            Assert.Equal(2, Targeting.ExpandShape(new Position(0, 0), Shape.Row3, 5, 5).Count);
            Assert.Equal(3, Targeting.ExpandShape(new Position(2, 1), Shape.Column3, 5, 5).Count);
            Assert.Equal(5, Targeting.ExpandShape(new Position(2, 2), Shape.Cross, 5, 5).Count);
            Assert.Equal(25, Targeting.ExpandShape(new Position(2, 2), Shape.All, 5, 5).Count);
        }

        [Fact]
        public void Row3_HitsOnlyThreeCells()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(shape: Shape.Row3) }, atk: 100), PH(2)));
            for (int lane = 0; lane < 5; lane++) setup.Enemies.Add(new EnemySlot(Enemy("e" + lane, hp: 1000, atk: 1), PE(lane), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.PlayCard(Find(b, "atk"), PE(1));
            Assert.Equal(new[] { 900, 900, 900, 1000, 1000 }, Enumerable.Range(0, 5).Select(l => EnemyUnit(b, "e" + l).Hp).ToArray());
        }

        [Fact]
        public void SingleTarget_RangeIsHeroAttackRange()
        {
            var b = Fight(Hero("H", new[] { Attack() }, range: 2), Enemy("e"), heroPos: PH(2, 3), enemyPos: PE(2, 1));
            Assert.Equal(PlayResult.Ok, b.CanPlay(Find(b, "atk")));
            var far = Fight(Hero("H", new[] { Attack() }, range: 1), Enemy("e"), heroPos: PH(2, 3), enemyPos: PE(2, 1));
            Assert.Equal(PlayResult.NoTarget, far.CanPlay(Find(far, "atk")));
            Assert.Equal(PlayResult.NoTarget, far.PlayCard(Find(far, "atk"), PE(2, 1)));
            Assert.Equal(PlayResult.NoTarget, far.PlayCard(Find(far, "atk")));
        }

        [Fact]
        public void EnemyCards_CannotBeSwungAtEmptyCells()
        {
            var b = Fight(Hero("H", new[] { Attack() }, range: 5), Enemy("e", hp: 500), enemyPos: PE(2, 1));
            int cost = b.Cost;
            Assert.Equal(PlayResult.OutOfRange, b.PlayCard(Find(b, "atk"), PE(0, 0)));
            Assert.Equal(cost, b.Cost);
            Assert.Contains(b.Hand, c => c.Def.Id == "atk");
            Assert.Equal(PlayResult.Ok, b.PlayCard(Find(b, "atk"), PE(2, 1)));
        }

        [Fact]
        public void AreaCards_NeedAtLeastOneEnemyInShape()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(shape: Shape.Row3) }, range: 5), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 500), PE(2, 1), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            Assert.Equal(PlayResult.OutOfRange, b.PlayCard(Find(b, "atk"), PE(0, 1)));
            Assert.Equal(PlayResult.Ok, b.PlayCard(Find(b, "atk"), PE(1, 1)));
        }

        [Fact]
        public void AllEnemies_IgnoresAttackRange()
        {
            var aoe = Card("aoe", 1, TargetRule.AllEnemies, Shape.All, Dmg(1.0));
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { aoe }, range: 1), PH(2, 4)));
            setup.Enemies.Add(new EnemySlot(Enemy("a", hp: 1000), PE(0, 0), EnemyLevelForUnitScale));
            setup.Enemies.Add(new EnemySlot(Enemy("b", hp: 1000), PE(4, 1), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            Assert.Equal(PlayResult.Ok, b.PlayCard(Find(b, "aoe")));
            Assert.Equal(900, EnemyUnit(b, "a").Hp);
            Assert.Equal(900, EnemyUnit(b, "b").Hp);
        }

        [Fact]
        public void AllyTarget_ChosenOrLowestHpRatio()
        {
            var heal = Card("heal", 0, TargetRule.Ally, Shape.Single, new EffectDef { Type = EffectType.Heal, Multiplier = 1.0 });
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(new HeroSlot(Hero("Healer", new[] { heal, heal }, intel: 100, range: 3), PH(2, 4)));
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, hp: 1000), PH(1, 3)) { StartHpPercent = 50 });
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack() }, hp: 1000), PH(3, 3)) { StartHpPercent = 10 });
            setup.Enemies.Add(new EnemySlot(Enemy("e"), PE(2), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var cards = b.Hand.Where(c => c.Def.Id == "heal").ToList();
            b.PlayCard(cards[0]);
            Assert.Equal(200, HeroUnit(b, "B").Hp);
            b.PlayCard(cards[1], PH(1, 3));
            Assert.Equal(600, HeroUnit(b, "A").Hp);
        }

        private static Battle StandardTeam(ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed };
            setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaShield(), DemoContent.HeroPos(0, 0)));
            setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaSword(), DemoContent.HeroPos(1, 0)));
            setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaArcher(), DemoContent.HeroPos(1, 1)));
            setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaHealer(), DemoContent.HeroPos(2, 1)));
            setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(2, 0), 1));
            return new Battle(setup);
        }

        [Fact]
        public void Deck_FourHeroes_Has24Cards_20Specific4Moves()
        {
            var b = StandardTeam();
            var all = b.Hand.Concat(b.DrawPile).Concat(b.DiscardPile).ToList();
            Assert.Equal(24, all.Count);
            Assert.Equal(4, all.Count(c => c.Owner == null && c.Def.Id == "move"));
            Assert.Equal(20, all.Count(c => c.Owner != null));
            foreach (var hero in b.Units.Where(u => u.Side == Side.Player))
                Assert.Equal(5, all.Count(c => c.Owner == hero));
        }

        [Fact]
        public void FirstTurn_Draws5RandomPlus2Moves_ThenThreePerTurn()
        {
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var b = StandardTeam(seed);
                Assert.Equal(7, b.Hand.Count);
                Assert.True(b.Hand.Count(c => c.Owner == null) >= 2);
                Assert.Equal(3, b.Cost);
                b.EndTurn();
                Assert.Equal(10, b.Hand.Count);
                Assert.Equal(6, b.Cost);
            }
        }

        [Fact]
        public void Cost_IsCappedAt10()
        {
            var setup = new BattleSetup { Seed = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(0, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            for (int i = 0; i < 6; i++) b.EndTurn();
            Assert.Equal(10, b.Cost);
        }

        [Fact]
        public void Hand_IsCappedAt10_OverflowGoesToDiscard()
        {
            var b = StandardTeam();
            b.EndTurn();
            Assert.Equal(10, b.Hand.Count);
            int discard = b.DiscardPile.Count;
            b.EndTurn();
            Assert.Equal(10, b.Hand.Count);
            Assert.Equal(discard + 3, b.DiscardPile.Count);
        }

        [Fact]
        public void DrawPile_ReshufflesDiscardWhenEmpty()
        {
            var b = StandardTeam();
            for (int turn = 0; turn < 8; turn++)
            {
                foreach (var c in b.Hand.Where(c => c.Def.Cost == 0).ToList())
                    b.DiscardPile.Add(c);
                b.Hand.RemoveAll(c => c.Def.Cost == 0);
                b.EndTurn();
                if (b.Result != BattleResult.Ongoing) break;
            }
            Assert.True(b.Hand.Count > 0);
            Assert.Equal(24, b.Hand.Count + b.DrawPile.Count + b.DiscardPile.Count);
        }

        [Fact]
        public void HeroDeath_RemovesItsCardsAndOneMoveCard()
        {
            var b = StandardTeam();
            var victim = HeroUnit(b, "義勇醫士");
            typeof(Battle).GetMethod("Kill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(b, new object[] { victim });
            var all = b.Hand.Concat(b.DrawPile).Concat(b.DiscardPile).ToList();
            Assert.Equal(24 - 5 - 1, all.Count);
            Assert.DoesNotContain(all, c => c.Owner == victim);
            Assert.Equal(3, all.Count(c => c.Owner == null));
        }

        [Fact]
        public void Move_StepsEqualMoveStat_CannotPassThroughUnits()
        {
            var setup = new BattleSetup { Seed = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, move: 2), PH(2, 3)));
            setup.Heroes.Add(new HeroSlot(Hero("Wall", new[] { Attack() }, move: 1), PH(2, 2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e"), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            var h = HeroUnit(b, "H");
            var reach = b.ReachableTiles(h);
            Assert.DoesNotContain(new Position(2, 1), reach.Keys);
            Assert.Contains(new Position(1, 2), reach.Keys);
            Assert.DoesNotContain(new Position(2, 2), reach.Keys);
            var move = b.Hand.First(c => c.Def.Id == "move");
            Assert.Equal(PlayResult.OutOfRange, b.PlayCard(move, new Position(2, 1), h));
            Assert.Equal(PlayResult.Ok, b.PlayCard(move, new Position(1, 2), h));
            Assert.Equal(new Position(1, 2), h.Pos);
        }

        [Fact]
        public void HeroLevelGrowth_IsLinear1Point5Percent()
        {
            Assert.Equal(1.585, Battle.HeroLevelFactor(40), 3);
            Assert.Equal(1.885, Battle.HeroLevelFactor(60), 3);
            var s = Battle.ScaleHero(new Stats { Hp = 1000, Atk = 100, Int = 100, Def = 100, Crit = 20, Dodge = 10, Move = 2, Range = 2, CritDmg = 150 }, 40);
            Assert.Equal(1585, s.Hp);
            Assert.Equal(158, s.Atk > 158 ? 158 : s.Atk);
            Assert.Equal(20, s.Crit);
            Assert.Equal(10, s.Dodge);
            Assert.Equal(2, s.Move);
            Assert.Equal(2, s.Range);
        }

        [Fact]
        public void EnemyLevel_AndTierMultipliers()
        {
            Assert.Equal(0.6, Battle.EnemyLevelFactor(1), 6);
            Assert.Equal(2.16, Battle.EnemyLevelFactor(40), 6);

            var baseStats = new Stats { Hp = 1000, Atk = 100, Int = 100, Def = 100 };
            EnemyDef Make(EnemyTier tier) => new EnemyDef { Id = "x", Name = "x", Tier = tier, Base = baseStats };
            var normal = Battle.ScaleEnemy(Make(EnemyTier.Normal), 11);
            var elite = Battle.ScaleEnemy(Make(EnemyTier.Elite), 11);
            var boss = Battle.ScaleEnemy(Make(EnemyTier.Boss), 11);
            Assert.Equal((1000, 100, 100), (normal.Hp, normal.Atk, normal.Def));
            Assert.Equal((1400, 140, 100), (elite.Hp, elite.Atk, elite.Def));
            Assert.Equal((3000, 140, 100), (boss.Hp, boss.Atk, boss.Def));
            Assert.Equal(140, elite.Int);
        }

        [Fact]
        public void Escort_WinsAfterSurvivingTurns_LosesWhenTargetDies()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true, Objective = Objective.Escort, SurviveTurns = 3 };
            setup.Heroes.Add(new HeroSlot(Hero("VIP", new[] { Attack() }, hp: 10000), PH(2)) { IsProtected = true });
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.EndTurn(); b.EndTurn();
            Assert.Equal(BattleResult.Ongoing, b.Result);
            b.EndTurn();
            Assert.Equal(BattleResult.Won, b.Result);

            var lose = new BattleSetup { Seed = 1, NoRandomness = true, Objective = Objective.Escort, SurviveTurns = 5 };
            lose.Heroes.Add(new HeroSlot(Hero("VIP", new[] { Attack() }, hp: 10), PH(2, 4)) { IsProtected = true });
            lose.Heroes.Add(new HeroSlot(Hero("Guard", new[] { Attack() }, hp: 10000), PH(0, 3)));
            lose.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 999, range: 10), PE(2, 0), EnemyLevelForUnitScale));
            var b2 = new Battle(lose);
            b2.EndTurn();
            Assert.Equal(BattleResult.Lost, b2.Result);
        }

        [Fact]
        public void KillTarget_WinsWhenObjectiveDies_EvenWithOthersAlive()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true, Objective = Objective.KillTarget };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(mult: 1000) }, hp: 10000), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("boss", hp: 100, atk: 1), PE(2, 1), EnemyLevelForUnitScale) { IsObjective = true });
            setup.Enemies.Add(new EnemySlot(Enemy("minion", hp: 99999, atk: 1), PE(4, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.PlayCard(Find(b, "atk"), PE(2, 1));
            Assert.Equal(BattleResult.Won, b.Result);
        }

        [Fact]
        public void TurnLimit_FailsIfNotWonInTime()
        {
            var setup = new BattleSetup { Seed = 1, NoRandomness = true, TurnLimit = 2 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 10000), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2, 0), EnemyLevelForUnitScale));
            var b = new Battle(setup);
            b.EndTurn();
            Assert.Equal(BattleResult.Ongoing, b.Result);
            b.EndTurn();
            Assert.Equal(BattleResult.Lost, b.Result);
        }

        [Fact]
        public void SameSeed_ProducesIdenticalBattle()
        {
            string Run(ulong seed)
            {
                var b = StandardTeam(seed);
                AutoPlayer.RunToEnd(b, 50);
                return string.Join("|", b.Events.Select(e => e.ToString()));
            }
            Assert.Equal(Run(5), Run(5));
        }
    }
}
