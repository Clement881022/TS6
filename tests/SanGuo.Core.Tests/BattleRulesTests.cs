using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class BattleRulesTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public BattleRulesTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            _out = output;
        }

        // ---- 測試用的最小武將 / 敵人 ----

        // 棋盤 5x5：我方站下兩列（列 3–4）、敵方站上兩列（列 0–1）。測試用 PH / PE 取格。
        private static Position PH(int lane, int row = 3) => new Position(lane, row);
        private static Position PE(int lane, int row = 1) => new Position(lane, row);

        /// <summary>手牌裡第一張非移動卡（手牌順序是洗過的，移動卡可能排在前面）。</summary>
        private static CardInstance FirstCard(Battle b) => b.Hand.First(c => c.Def.Target != TargetRule.MoveDest);

        private static CardDef Attack(string id = "atk", int cost = 1, TargetRule target = TargetRule.Enemy,
            Shape shape = Shape.Single, double mult = 1.0, CardKeywords kw = CardKeywords.None, int range = 10) =>
            new CardDef
            {
                Id = id, Name = id, Cost = cost, Target = target, Range = range, Shape = shape, Keywords = kw,
                Effects = { new EffectDef { Type = EffectType.Damage, Multiplier = mult } },
            };

        private static HeroDef Hero(string name, IEnumerable<CardDef> deck, int atk = 100, int hp = 1000, int def = 0,
            int move = 1, AttackType type = AttackType.Melee, Role role = Role.Warrior, int intel = 0) =>
            new HeroDef
            {
                Id = name, Name = name, AttackType = type, Role = role,
                Base = new Stats { Hp = hp, Atk = atk, Int = intel, Def = def, Move = move, Crit = 0, Dodge = 0 },
                Deck = deck.ToList(),
            };

        private static EnemyDef Enemy(string name, int hp = 500, int atk = 50, int def = 0, int move = 1,
            AttackType type = AttackType.Melee) =>
            new EnemyDef
            {
                Id = name, Name = name, AttackType = type,
                Base = new Stats { Hp = hp, Atk = atk, Def = def, Move = move, Crit = 0, Dodge = 0 },
            };

        private static Unit EnemyUnit(Battle b, string name) => b.Units.First(u => u.Side == Side.Enemy && u.Name == name);

        // ---- 傷害公式 ----

        [Fact]
        public void DamageFormula_UsesMultiplicativeDefense()
        {
            Assert.Equal(50, DamageCalc.Compute(100, 1.0, 100, false, 150));
            Assert.Equal(100, DamageCalc.Compute(100, 1.0, 0, false, 150));
            Assert.Equal(150, DamageCalc.Compute(100, 1.0, 0, true, 150));
            Assert.Equal(1, DamageCalc.Compute(1, 0.1, 500, false, 150));
        }

        [Fact]
        public void ArmorBreak_SeparateEntries_MultiplyAndExpireIndependently()
        {
            CardDef Breaker(string id, double pct, int turns) => new CardDef
            {
                Id = id, Name = id, Cost = 0, Target = TargetRule.Enemy, Range = 10,
                Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.ArmorBreak, Multiplier = pct, Amount = turns } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Breaker("b40", 0.4, 1), Breaker("b15", 0.15, 3), Attack() }, atk: 100, hp: 99999), PH(1)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1, def: 100), PE(1)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            Assert.Equal(100.0, enemy.EffectiveDef);
            foreach (var card in battle.Hand.Where(c => c.Def.Id.StartsWith("b")).ToList()) battle.PlayCard(card);
            Assert.Equal(2, enemy.DefBreaks.Count);
            Assert.Equal(51.0, enemy.EffectiveDef, 6); // 100 × (1-0.4) × (1-0.15)

            battle.EndTurn(); // 40% 那筆只有 1 回合，到期；15% 那筆還在
            Assert.Single(enemy.DefBreaks);
            Assert.Equal(85.0, enemy.EffectiveDef, 6);
        }

        // ---- 隨機數 ----

        [Fact]
        public void Rng_SameSeedSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 50; i++) Assert.Equal(a.NextULong(), b.NextULong());
            Assert.NotEqual(new Rng(1).NextULong(), new Rng(2).NextULong());
        }

        // ---- 目標與範圍 ----

        [Fact]
        public void Shape_Cross_Row_Column_Cells()
        {
            var cross = Targeting.ExpandShape(new Position(2, 0), Shape.Cross, 5, 5);
            Assert.Equal(4, cross.Count); // 中心 + 左右 + 下方（上方出界）
            Assert.Contains(cross, p => p.Lane == 1 && p.Row == 0);
            Assert.Contains(cross, p => p.Lane == 3 && p.Row == 0);
            Assert.Contains(cross, p => p.Lane == 2 && p.Row == 1);

            Assert.Equal(5, Targeting.ExpandShape(new Position(2, 1), Shape.Row, 5, 5).Count);
            Assert.Equal(5, Targeting.ExpandShape(new Position(2, 1), Shape.Column, 5, 5).Count);
            Assert.Equal(25, Targeting.ExpandShape(new Position(0, 0), Shape.All, 5, 5).Count);
        }

        [Fact]
        public void Target_AutoPicksNearestInRange_AndRangeLimitsChoice()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(range: 1) }), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("near"), new Position(2, 2)));     // 距離 1
            setup.Enemies.Add(new EnemySlot(Enemy("far"), new Position(2, 1)));      // 距離 2，超出射程
            var battle = new Battle(setup);
            var hero = battle.Units[0];
            var def = FirstCard(battle).Def;

            var auto = battle.ResolveTargets(hero, def)!;
            Assert.Single(auto);
            Assert.Equal("near", auto[0].Name);

            var far = EnemyUnit(battle, "far");
            Assert.Null(battle.ResolveTargets(hero, def, far));                      // 指定射程外的目標：不可
            Assert.Equal(PlayResult.OutOfRange, battle.PlayCard(FirstCard(battle), far));
            Assert.Equal(PlayResult.Ok, battle.PlayCard(FirstCard(battle), EnemyUnit(battle, "near")));
        }

        [Fact]
        public void Target_NoEnemyInRange_CannotPlay()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(range: 1) }), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e"), PE(2)));                     // 距離 2
            var battle = new Battle(setup);
            Assert.Equal(PlayResult.NoTarget, battle.CanPlay(FirstCard(battle)));
        }

        [Fact]
        public void LowestHpRule_PicksWeakestInRange()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(target: TargetRule.EnemyLowestHp, range: 3) }), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("strong", hp: 900), new Position(2, 2)));
            setup.Enemies.Add(new EnemySlot(Enemy("weak", hp: 100), new Position(1, 1)));      // 距離 3
            setup.Enemies.Add(new EnemySlot(Enemy("weakest", hp: 10), new Position(0, 0)));    // 距離 5，超出射程
            var battle = new Battle(setup);
            var targets = battle.ResolveTargets(battle.Units[0], FirstCard(battle).Def)!;
            Assert.Equal("weak", targets[0].Name);
        }

        // ---- 費用與牌庫 ----

        [Fact]
        public void Cost_StartsAtThree_StacksAndCapsAtTen()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), PH(0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(4, 0)));
            var battle = new Battle(setup);
            Assert.Equal(3, battle.Cost);
            battle.EndTurn();
            Assert.Equal(6, battle.Cost);
            for (int i = 0; i < 10; i++) battle.EndTurn();
            Assert.Equal(10, battle.Cost);
        }

        [Fact]
        public void Hand_CarriesOver_DrawsPerTurn_AndCapsAtTen()
        {
            var deck = Enumerable.Range(0, 20).Select(i => Attack("c" + i)).ToList();   // + 1 張移動卡
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), PH(0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(4, 0)));
            var battle = new Battle(setup);
            Assert.Equal(5, battle.Hand.Count);
            Assert.Equal(16, battle.DrawPile.Count);

            battle.EndTurn();                       // 手牌不棄：5 + 3
            Assert.Equal(8, battle.Hand.Count);
            battle.EndTurn();                       // 8 + 3 → 受手牌上限 10 限制
            Assert.Equal(Battle.MaxHandSize, battle.Hand.Count);
            Assert.Empty(battle.DiscardPile);
        }

        [Fact]
        public void DrawPile_NeverReshuffles_WhenExhausted()
        {
            var deck = Enumerable.Range(0, 4).Select(i => Attack("c" + i, cost: 0)).ToList();   // + 1 張移動卡 = 5 張
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(2, 2)));
            var battle = new Battle(setup);
            Assert.Equal(5, battle.Hand.Count);
            Assert.Empty(battle.DrawPile);

            battle.PlayCard(FirstCard(battle));
            battle.PlayCard(FirstCard(battle));
            Assert.Equal(2, battle.DiscardPile.Count);
            battle.EndTurn();                       // 牌堆已空：不會把棄牌堆洗回來
            Assert.Equal(3, battle.Hand.Count);
            Assert.Empty(battle.DrawPile);
            Assert.Equal(2, battle.DiscardPile.Count);
        }

        private static Battle SmallBattle(params CardDef[] deck)
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(2, 2)));
            return new Battle(setup);
        }

        [Fact]
        public void Keyword_Exhaust_MovesCardToExhaustPile()
        {
            var battle = SmallBattle(Attack("ex", cost: 0, kw: CardKeywords.Exhaust));
            var card = FirstCard(battle);
            battle.PlayCard(card);
            Assert.Contains(card, battle.ExhaustPile);
            Assert.DoesNotContain(card, battle.DiscardPile);
        }

        [Fact]
        public void UnplayedCards_StayInHandAtTurnEnd()
        {
            var battle = SmallBattle(Attack("a", cost: 0), Attack("b", cost: 0));
            var held = battle.Hand.ToList();
            battle.EndTurn();
            Assert.All(held, c => Assert.Contains(c, battle.Hand));
        }

        [Fact]
        public void Keyword_Innate_AlwaysInOpeningHand()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var deck = new List<CardDef> { Attack("in", kw: CardKeywords.Innate) };
                deck.AddRange(Enumerable.Range(0, 20).Select(i => Attack("f" + i)));
                var setup = new BattleSetup { Seed = seed };
                setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), PH(2)));
                setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
                var battle = new Battle(setup);
                Assert.Contains(battle.Hand, c => c.Def.Id == "in");
            }
        }

        // ---- 謀略（法系屬性）----

        private static CardDef HealCard(double mult) => new CardDef
        {
            Id = "heal", Name = "heal", Cost = 0, Target = TargetRule.AllyLowestHp, Range = 3,
            Effects = { new EffectDef { Type = EffectType.Heal, Multiplier = mult } },
        };

        private static CardDef BuffCard() => new CardDef
        {
            Id = "buff", Name = "buff", Cost = 0, Target = TargetRule.Self,
            Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.DefUp, Multiplier = 0.3, Amount = 2, OnSelf = true } },
        };

        [Fact]
        public void Caster_HealUsesIntellect_NotAttack()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Healer", new[] { HealCard(1.0) }, atk: 10, intel: 200, role: Role.Healer), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
            var battle = new Battle(setup);
            var healer = battle.Units[0];
            healer.Hp = 100;
            battle.PlayCard(FirstCard(battle));
            Assert.Equal(300, healer.Hp);               // 200（謀略）× 1.0；攻擊 10 不參與
        }

        [Fact]
        public void NonCaster_HealStillUsesAttack()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Knight", new[] { HealCard(1.0) }, atk: 50, intel: 999, role: Role.Warrior), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
            var battle = new Battle(setup);
            var knight = battle.Units[0];
            knight.Hp = 100;
            battle.PlayCard(FirstCard(battle));
            Assert.Equal(150, knight.Hp);
        }

        [Fact]
        public void Caster_BuffStrengthScalesWithIntellect()
        {
            Unit Cast(Role role, int intel)
            {
                var setup = new BattleSetup();
                setup.Heroes.Add(new HeroSlot(Hero("H", new[] { BuffCard() }, intel: intel, role: role), PH(2)));
                setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
                var battle = new Battle(setup);
                battle.PlayCard(FirstCard(battle));
                return battle.Units[0];
            }
            Assert.Equal(30, Cast(Role.Warrior, 300).Statuses[StatusType.DefUp].Power);           // 非法系：固定
            Assert.Equal(30, Cast(Role.Strategist, DamageCalc.CasterReference).Statuses[StatusType.DefUp].Power);
            Assert.Equal(60, Cast(Role.Strategist, DamageCalc.CasterReference * 2).Statuses[StatusType.DefUp].Power);
        }

        [Fact]
        public void Caster_DamageUsesIntellect_ArcherUsesAttack()
        {
            int Hit(Role role)
            {
                var setup = new BattleSetup();
                setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(cost: 0) }, atk: 100, intel: 300, role: role), PH(2)));
                setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
                var battle = new Battle(setup);
                battle.PlayCard(FirstCard(battle));
                return 99999 - EnemyUnit(battle, "e").Hp;
            }
            Assert.Equal(300, Hit(Role.Mage));
            Assert.Equal(100, Hit(Role.Archer));
        }

        // ---- 護甲 / 狀態 ----

        [Fact]
        public void Armor_AbsorbsDamage_AndPersistsAcrossTurns()
        {
            var guard = new CardDef
            {
                Id = "guard", Name = "guard", Cost = 1, Target = TargetRule.Self,
                Effects = { new EffectDef { Type = EffectType.Armor, Multiplier = 1.0, OnSelf = true } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { guard }, atk: 100), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", atk: 30, hp: 99999), new Position(2, 2)));
            var battle = new Battle(setup);
            var hero = battle.Units[0];

            battle.PlayCard(FirstCard(battle));
            Assert.Equal(100, hero.Armor);
            battle.EndTurn(); // 敵人攻擊 30 → 護甲 70，血量不變
            Assert.Equal(70, hero.Armor);
            Assert.Equal(hero.MaxHp, hero.Hp);
        }

        [Fact]
        public void DeadHero_CardsCannotBePlayed()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack("a") }, hp: 10), PH(1)));
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack("b") }), PH(3)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", atk: 999, hp: 99999), new Position(1, 2)));
            var battle = new Battle(setup);
            var cardA = battle.Hand.First(c => c.Owner.Name == "A" && c.Def.Id == "a");

            battle.EndTurn(); // 敵人殺死 A
            Assert.False(battle.Units[0].Alive);
            Assert.Equal(PlayResult.OwnerDead, battle.CanPlay(cardA));
        }

        [Fact]
        public void Stunned_HeroCannotPlayCards()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), PE(2)));
            var battle = new Battle(setup);
            battle.Units[0].Statuses[StatusType.Stun] = new StatusState { Turns = 1 };
            Assert.Equal(PlayResult.Stunned, battle.CanPlay(FirstCard(battle)));
        }

        [Fact]
        public void Burn_DealsDamageAtStartOfOwnPhase_AndExpires()
        {
            var fire = new CardDef
            {
                Id = "fire", Name = "fire", Cost = 1, Target = TargetRule.Enemy, Range = 10,
                Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.Burn, Multiplier = 0.5, Amount = 2 } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { fire }, atk: 100, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 1000, atk: 1), PE(2)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            battle.PlayCard(FirstCard(battle));
            battle.EndTurn(); // 敵方階段灼燒 50
            Assert.Equal(950, enemy.Hp);
            battle.EndTurn(); // 第二次灼燒（持續 2 輪）
            Assert.Equal(900, enemy.Hp);
            battle.EndTurn();
            Assert.Equal(900, enemy.Hp); // 已到期
        }

        // ---- 移動卡 ----

        [Fact]
        public void MoveCard_OnePerHeroWithCards_ZeroCost()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("A", Enumerable.Range(0, 8).Select(i => Attack("a" + i)), hp: 99999), PH(1)));
            setup.Heroes.Add(new HeroSlot(Hero("B", Enumerable.Range(0, 8).Select(i => Attack("b" + i)), hp: 99999), PH(2)));
            setup.Heroes.Add(new HeroSlot(Hero("Vip", new CardDef[0]), PH(3)) { IsProtected = true });   // 沒有牌的單位不洗入移動卡
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(2)));
            var battle = new Battle(setup);

            var moves = battle.Hand.Concat(battle.DrawPile).Where(c => c.Def.Target == TargetRule.MoveDest).ToList();
            Assert.Equal(2, moves.Count);
            Assert.All(moves, c => Assert.Equal(0, c.Def.Cost));
            Assert.Equal(new[] { "A", "B" }, moves.Select(c => c.Owner.Name).OrderBy(n => n).ToArray());
        }

        private static (Battle, Unit, CardInstance) MoveBattle(int move, Position heroPos, params (string, Position)[] others)
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, move: move), heroPos));
            foreach (var (name, pos) in others)
                setup.Enemies.Add(new EnemySlot(Enemy(name, hp: 99999, atk: 1), pos));
            var battle = new Battle(setup);
            return (battle, battle.Units[0], battle.Hand.First(c => c.Def.Target == TargetRule.MoveDest));
        }

        [Fact]
        public void MoveCard_MovesWithinMoveRange_AndCostsNothing()
        {
            var (battle, hero, card) = MoveBattle(2, PH(2), ("e", PE(0, 0)));
            Assert.Equal(PlayResult.OutOfRange, battle.PlayCard(card, null, new Position(2, 0)));   // 3 格，超過移動力 2
            Assert.Equal(3, battle.Cost);
            Assert.Equal(PlayResult.Ok, battle.PlayCard(card, null, new Position(2, 1)));
            Assert.Equal(new Position(2, 1), hero.Pos);
            Assert.Same(hero, battle.UnitAt(new Position(2, 1)));
            Assert.Null(battle.UnitAt(new Position(2, 3)));
            Assert.Equal(3, battle.Cost);                                                            // 0 費
            Assert.Contains(card, battle.DiscardPile);
        }

        [Fact]
        public void MoveCard_NeedsDestination_AndCannotLandOnOrPassThroughUnits()
        {
            var (battle, hero, card) = MoveBattle(2, PH(2), ("wall", new Position(2, 2)));
            Assert.Equal(PlayResult.NoTarget, battle.PlayCard(card));                                // 沒給目的地
            Assert.Equal(PlayResult.OutOfRange, battle.PlayCard(card, null, new Position(2, 2)));    // 不能停在單位上
            Assert.Equal(PlayResult.OutOfRange, battle.PlayCard(card, null, new Position(2, 1)));    // 直線被擋，繞路要 4 步
            Assert.Equal(PlayResult.OutOfRange, battle.PlayCard(card, null, hero.Pos));              // 原地不算移動
            Assert.Equal(PlayResult.Ok, battle.PlayCard(card, null, new Position(1, 2)));
        }

        [Fact]
        public void MoveCard_BlockedByStunOrDeath()
        {
            var (battle, hero, card) = MoveBattle(2, PH(2), ("e", PE(0, 0)));
            hero.Statuses[StatusType.Stun] = new StatusState { Turns = 1 };
            Assert.Equal(PlayResult.Stunned, battle.CanPlay(card));
            hero.Statuses.Clear();
            hero.Alive = false;
            Assert.Equal(PlayResult.OwnerDead, battle.CanPlay(card));
        }

        [Fact]
        public void AutoPlayer_UsesMoveCardToCloseDistance()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("A", Enumerable.Range(0, 6).Select(i => Attack("a" + i, range: 1)), hp: 99999, atk: 500, move: 3), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 600, atk: 1, move: 1), PE(2, 0)));
            var battle = new Battle(setup);
            Assert.Equal(BattleResult.Won, AutoPlayer.RunToEnd(battle, 30));
        }

        // ---- 敵方 AI ----

        [Fact]
        public void MeleeEnemy_MovesAndAttacksInSameTurn_WhenReachable()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 100, move: 2), PE(2, 0)));     // 距離 3，走 2 步就能貼身
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            var intent = battle.GetIntent(enemy);
            Assert.Equal(Intent.Kind.Attack, intent.Type);
            Assert.Equal(new Position(2, 2), intent.MoveTo);
            battle.EndTurn();
            Assert.Equal(new Position(2, 2), enemy.Pos);
            Assert.True(battle.Units[0].Hp < 99999);
        }

        [Fact]
        public void MeleeEnemy_ApproachesWhenOutOfReach_WithoutAttacking()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 100, move: 1), PE(2, 0)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            Assert.Equal(Intent.Kind.Move, battle.GetIntent(enemy).Type);
            battle.EndTurn();
            Assert.Equal(new Position(2, 1), enemy.Pos);
            Assert.Equal(99999, battle.Units[0].Hp);
        }

        [Fact]
        public void MeleeEnemy_AttacksNearestHero()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Left", new[] { Attack() }, hp: 99999), PH(0)));
            setup.Heroes.Add(new HeroSlot(Hero("Right", new[] { Attack() }, hp: 99999), PH(4)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), PE(1, 2)));
            var battle = new Battle(setup);
            Assert.Equal("Left", battle.GetIntent(EnemyUnit(battle, "e")).Target!.Name);
        }

        [Fact]
        public void RangedEnemy_PrefersRearmostHeroInRange()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Front", new[] { Attack() }, hp: 99999), PH(2, 3)));
            setup.Heroes.Add(new HeroSlot(Hero("Back", new[] { Attack() }, hp: 99999), PH(2, 4)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, type: AttackType.Ranged), PE(2)));   // 距離 2 / 3，都在射程 3 內
            var battle = new Battle(setup);
            Assert.Equal("Back", battle.GetIntent(EnemyUnit(battle, "e")).Target!.Name);
        }

        [Fact]
        public void Taunt_ForcesEnemyToAttackTauntingHero()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Tank", new[] { Attack() }, hp: 99999), PH(0)));
            setup.Heroes.Add(new HeroSlot(Hero("Squishy", new[] { Attack() }, hp: 99999), PH(1)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), new Position(1, 2)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");
            var tank = battle.Units[0];

            // 沒挑釁：敵人打貼身的 Squishy。
            Assert.Equal("Squishy", battle.GetIntent(enemy).Target!.Name);

            tank.Statuses[StatusType.Taunt] = new StatusState { Turns = 2 };
            var intent = battle.GetIntent(enemy);
            Assert.Equal(Intent.Kind.Attack, intent.Type); // 強制打挑釁者（必要時先走位）
            Assert.Equal("Tank", intent.Target!.Name);
        }

        [Fact]
        public void Taunt_ReducesDamageTakenByTaunter()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Tank", new[] { Attack() }, hp: 99999), PH(0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 100), new Position(0, 2)));
            var battle = new Battle(setup);
            var tank = battle.Units[0];

            battle.EndTurn();                       // 沒挑釁：吃 100
            int normal = 99999 - tank.Hp;
            tank.Hp = 99999;
            tank.Statuses[StatusType.Taunt] = new StatusState { Turns = 2 };
            battle.EndTurn();                       // 挑釁：吃 70
            int taunted = 99999 - tank.Hp;
            Assert.Equal(100, normal);
            Assert.Equal(70, taunted);
        }

        // ---- 勝負與完整對戰 ----

        [Fact]
        public void KillingAllEnemies_Wins_AllHeroesDead_Loses()
        {
            var win = new BattleSetup();
            win.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(mult: 100) }), PH(2)));
            win.Enemies.Add(new EnemySlot(Enemy("e", hp: 10), PE(2)));
            var b1 = new Battle(win);
            b1.PlayCard(FirstCard(b1));
            Assert.Equal(BattleResult.Won, b1.Result);

            var lose = new BattleSetup();
            lose.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 10), PH(2)));
            lose.Enemies.Add(new EnemySlot(Enemy("e", atk: 999, hp: 99999), new Position(2, 2)));
            var b2 = new Battle(lose);
            b2.EndTurn();
            Assert.Equal(BattleResult.Lost, b2.Result);
        }

        [Fact]
        public void TurnLimit_LosesWhenExceeded()
        {
            var setup = new BattleSetup { TurnLimit = 3 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(4, 4)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), PE(0, 0)));
            var battle = new Battle(setup);
            for (int i = 0; i < 5; i++) battle.EndTurn();
            Assert.Equal(BattleResult.Lost, battle.Result);
        }

        [Fact]
        public void SameSeed_ProducesIdenticalBattle()
        {
            var b1 = new Battle(DemoContent.SampleBattle(7));
            var b2 = new Battle(DemoContent.SampleBattle(7));
            var r1 = AutoPlayer.RunToEnd(b1);
            var r2 = AutoPlayer.RunToEnd(b2);
            Assert.Equal(r1, r2);
            Assert.Equal(b1.Events.Count, b2.Events.Count);
            Assert.Equal(b1.Events.Select(e => e.ToString()), b2.Events.Select(e => e.ToString()));
        }

        [Fact]
        public void DemoSampleBattle_AutoPlayTerminates()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var battle = new Battle(DemoContent.SampleBattle(seed));
                var result = AutoPlayer.RunToEnd(battle, maxTurns: 100);
                Assert.NotEqual(BattleResult.Ongoing, result);
            }
        }

        private static bool IsBreak(CardInstance c) =>
            c.Def.Effects.Any(e => e.Type == EffectType.ApplyStatus && e.Status == StatusType.ArmorBreak);

        private static bool IsTaunt(CardInstance c) =>
            c.Def.Effects.Any(e => e.Type == EffectType.ApplyStatus && e.Status == StatusType.Taunt);

        /// <summary>模擬「照教學打」：挑釁與破甲牌先出，再出其他牌。</summary>
        private static bool IsStun(CardInstance c) => c.Def.Effects.Any(e => e.Type == EffectType.StunGauge);

        private static bool SummonerAlive(Battle b) => b.AliveUnits(Side.Enemy).Any(e => e.Ability.HasFlag(EnemyAbility.Summoner));

        private static bool IsColumnPierce(CardInstance c) =>
            c.Def.Shape == Shape.Column && c.Def.Target == TargetRule.Enemy && c.Def.Effects.Any(e => e.Type == EffectType.Damage);

        private static bool IsDetonate(CardInstance c) => c.Def.Effects.Any(e => e.Type == EffectType.Detonate);

        private static bool IsBurn(CardInstance c) =>
            c.Def.Effects.Any(e => e.Type == EffectType.ApplyStatus && e.Status == StatusType.Burn);

        private static bool AnyBurning(Battle b) => b.AliveUnits(Side.Enemy).Any(e => e.Has(StatusType.Burn));

        private static bool IsShield(CardInstance c) =>
            c.Def.Target == TargetRule.AllyLowestHp && c.Def.Effects.Any(e => e.Type == EffectType.Armor);

        private static bool VipAlive(Battle b) => b.AliveUnits(Side.Player).Any(u => u.Protected);

        private static bool ChargerAlive(Battle b) => b.AliveUnits(Side.Enemy).Any(e => e.Ability.HasFlag(EnemyAbility.Charger));

        private static bool HealerAlive(Battle b) => b.AliveUnits(Side.Enemy).Any(e => e.Ability.HasFlag(EnemyAbility.Healer));

        private static bool IsBackShot(CardInstance c) =>
            (c.Def.Target == TargetRule.EnemyLowestHp || (c.Def.Target == TargetRule.Enemy && c.Def.Range >= 3))
            && c.Def.Effects.Any(e => e.Type == EffectType.Damage);

        private static int TutorialPriority(Battle battle, CardInstance c)
        {
            if (IsColumnPierce(c) && SummonerAlive(battle)) return 10; // 縱列穿透，一槍打到後排的召喚者
            if (IsBurn(c)) return 9;                             // 先放火
            if (IsDetonate(c)) return AnyBurning(battle) ? 8 : -1; // 有火再引爆
            if (IsShield(c) && VipAlive(battle)) return 7;      // 先用護甲保護鄉民
            if (IsStun(c) && ChargerAlive(battle)) return 6;    // 有蓄力的敵人：先把昏亂條打滿
            if (IsBackShot(c) && HealerAlive(battle)) return 5; // 先用弓手打後排的治療者
            if (IsTaunt(c)) return battle.AliveUnits(Side.Player).Any(u => u.Has(StatusType.Taunt)) ? -1 : 4; // 已在挑釁中就不重複出
            return IsBreak(c) ? 3 : 0;
        }

        /// <summary>模擬「忽略教學」：不出挑釁、不出破甲，其他照出。</summary>
        private static int IgnoreTutorialPriority(Battle battle, CardInstance c) =>
            IsTaunt(c) || IsBreak(c) || IsDetonate(c) || (IsColumnPierce(c) && SummonerAlive(battle)) || (IsShield(c) && VipAlive(battle)) || (IsStun(c) && ChargerAlive(battle)) || (IsBackShot(c) && HealerAlive(battle)) ? -1 : 0;

        private sealed class LevelStats
        {
            public int Wins, Total, Turns;
            public double Alive;
            public double WinRate => 100.0 * Wins / Total;
            public override string ToString() =>
                $"勝率 {WinRate:F0}%，{Turns / (double)Total:F1} 回合，存活 {Alive / Total:F1}/4";
        }

        private static LevelStats RunLevel(int level, System.Func<Battle, CardInstance, int>? priority)
        {
            var s = new LevelStats { Total = 200 };
            for (ulong seed = 1; seed <= (ulong)s.Total; seed++)
            {
                var battle = new Battle(DemoContent.Level(level, seed));
                if (AutoPlayer.RunToEnd(battle, 100, priority) == BattleResult.Won) s.Wins++;
                s.Turns += battle.Turn;
                s.Alive += battle.AliveUnits(Side.Player).Count;
            }
            return s;
        }

        [Fact]
        public void Level3_ArmorBreakLessonMatters()
        {
            var smart = RunLevel(3, TutorialPriority);
            var ignore = RunLevel(3, IgnoreTutorialPriority);
            _out.WriteLine($"第 3 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void Level2_TauntLessonMatters()
        {
            var smart = RunLevel(2, TutorialPriority);
            var ignore = RunLevel(2, IgnoreTutorialPriority);
            _out.WriteLine($"第 2 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
            Assert.True(smart.Alive / smart.Total >= ignore.Alive / ignore.Total + 1.5, "挑釁沒有明顯保住後排");
        }

        [Fact]
        public void Level4_KillTheHealerLessonMatters()
        {
            var smart = RunLevel(4, TutorialPriority);
            var ignore = RunLevel(4, IgnoreTutorialPriority);
            _out.WriteLine($"第 4 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void Level5_InterruptTheChargeLessonMatters()
        {
            var smart = RunLevel(5, TutorialPriority);
            var ignore = RunLevel(5, IgnoreTutorialPriority);
            _out.WriteLine($"第 5 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void Level6_ProtectTheVillagerLessonMatters()
        {
            var smart = RunLevel(6, TutorialPriority);
            var ignore = RunLevel(6, IgnoreTutorialPriority);
            _out.WriteLine($"第 6 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void ProtectedUnitDying_LosesTheBattle()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Vip", new CardDef[0], hp: 10), PH(1, 4)) { IsProtected = true });
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 999, type: AttackType.Ranged), PE(0)));
            var battle = new Battle(setup);
            battle.EndTurn();                       // 遠程敵人優先打後排的保護目標
            Assert.Equal(BattleResult.Lost, battle.Result);
            Assert.True(battle.Units.Any(u => u.Name == "H" && u.Alive)); // 其餘單位還活著也算輸
        }

        [Fact]
        public void Level7_DetonateTheFireLessonMatters()
        {
            var smart = RunLevel(7, TutorialPriority);
            var ignore = RunLevel(7, IgnoreTutorialPriority);
            _out.WriteLine($"第 7 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void Detonate_SettlesBurnAndSpreadsToNeighbors()
        {
            var fire = new CardDef
            {
                Id = "fire", Name = "fire", Cost = 0, Target = TargetRule.Enemy, Range = 10, Shape = Shape.Single,
                Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.Burn, Multiplier = 1.0, Amount = 3 } },
            };
            var boom = new CardDef
            {
                Id = "boom", Name = "boom", Cost = 0, Target = TargetRule.AllEnemies, Shape = Shape.All,
                Effects = { new EffectDef { Type = EffectType.Detonate, Status = StatusType.Burn, Amount = 2 } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { fire, boom }, atk: 100, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("left", hp: 1000, atk: 1), PE(1)));
            setup.Enemies.Add(new EnemySlot(Enemy("mid", hp: 1000, atk: 1), PE(2)));
            setup.Enemies.Add(new EnemySlot(Enemy("far", hp: 1000, atk: 1), PE(4)));
            var battle = new Battle(setup);
            var mid = EnemyUnit(battle, "mid");
            var left = EnemyUnit(battle, "left");
            var far = EnemyUnit(battle, "far");

            battle.PlayCard(battle.Hand.First(c => c.Def.Id == "fire"));
            Assert.True(mid.Has(StatusType.Burn));
            battle.PlayCard(battle.Hand.First(c => c.Def.Id == "boom"));
            Assert.Equal(1000 - 300, mid.Hp);                // 剩餘燒傷 100 × 3 回合 一次結算
            Assert.False(mid.Has(StatusType.Burn));          // 引爆即消耗
            Assert.True(left.Has(StatusType.Burn));          // 擴散給相鄰
            Assert.Equal(2, left.Statuses[StatusType.Burn].Turns);
            Assert.False(far.Has(StatusType.Burn));          // 不相鄰不擴散
        }

        [Fact]
        public void Level8_SlayTheSummonerLessonMatters()
        {
            var smart = RunLevel(8, TutorialPriority);
            var ignore = RunLevel(8, IgnoreTutorialPriority);
            _out.WriteLine($"第 8 關 照教學打 {smart}｜忽略教學 {ignore}");
            Assert.True(smart.WinRate >= 100, $"照教學打沒有 100%：{smart}");
            Assert.True(ignore.WinRate <= 10, $"忽略教學勝率太高：{ignore}");
        }

        [Fact]
        public void Summoner_FillsEmptyCells_UntilCap()
        {
            var minion = new EnemyDef { Id = "m", Name = "m", Base = new Stats { Hp = 10, Atk = 1, Def = 0 } };
            var summoner = new EnemyDef
            {
                Id = "s", Name = "s", Ability = EnemyAbility.Summoner, Summons = minion, SummonCap = 3,
                Base = new Stats { Hp = 99999, Atk = 1, Def = 0 },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(2, 4)));
            setup.Enemies.Add(new EnemySlot(summoner, PE(2, 0)));
            var battle = new Battle(setup);

            Assert.Equal(1, battle.AliveUnits(Side.Enemy).Count);
            battle.EndTurn();
            Assert.Equal(2, battle.AliveUnits(Side.Enemy).Count);
            battle.EndTurn();
            Assert.Equal(3, battle.AliveUnits(Side.Enemy).Count);
            battle.EndTurn();                                    // 達到上限，不再召喚
            Assert.Equal(3, battle.AliveUnits(Side.Enemy).Count);
        }

        [Fact]
        public void Levels9And10_FinalExamsFollowTheLessons()
        {
            foreach (int level in new[] { 9, 10 })
            {
                var smart = RunLevel(level, TutorialPriority);
                var ignore = RunLevel(level, IgnoreTutorialPriority);
                _out.WriteLine($"第 {level} 關 照教學打 {smart}｜忽略教學 {ignore}");
                Assert.True(smart.WinRate >= 100, $"第 {level} 關照教學打沒有 100%：{smart}");
                Assert.True(ignore.WinRate <= 10, $"第 {level} 關忽略教學勝率太高：{ignore}");
            }
        }

        [Fact]
        public void StunGauge_FillsThenStuns_AndCapGrows_ChargeInterrupted()
        {
            var stunCard = new CardDef
            {
                Id = "stun", Name = "stun", Cost = 0, Target = TargetRule.Enemy, Range = 10,
                Effects = { new EffectDef { Type = EffectType.StunGauge, Amount = 60 } },
            };
            var chief = new EnemyDef
            {
                Id = "c", Name = "c", Ability = EnemyAbility.Charger, AbilityPower = 5.0, StunGauge = 100, StunGrowth = 0.5,
                Base = new Stats { Hp = 99999, Atk = 100, Def = 0 },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { stunCard, stunCard, stunCard, Attack(), Attack() }, hp: 99999), PH(2)));
            setup.Enemies.Add(new EnemySlot(chief, PE(2)));
            var battle = new Battle(setup);
            var boss = EnemyUnit(battle, "c");

            Assert.Equal(Intent.Kind.Charge, battle.GetIntent(boss).Type);
            battle.EndTurn();                                     // 敵方階段：蓄力
            Assert.True(boss.Charging);
            Assert.True(battle.GetIntent(boss).Big);

            var stunCards = battle.Hand.Where(c => c.Def.Id == "stun").ToList();
            battle.PlayCard(stunCards[0]);                        // 60 / 100
            Assert.Equal(60, boss.StunGauge);
            Assert.False(boss.Has(StatusType.Stun));
            battle.PlayCard(stunCards[1]);                        // 120 → 滿 → 眩暈
            Assert.True(boss.Has(StatusType.Stun));
            Assert.Equal(0, boss.StunGauge);
            Assert.Equal(150, boss.StunGaugeMax);                 // 上限 +50%

            int hpBefore = battle.Units[0].Hp;
            battle.EndTurn();                                     // 被打斷：不放大招
            Assert.False(boss.Charging);
            Assert.Equal(hpBefore, battle.Units[0].Hp);
        }

        [Fact]
        public void HealerEnemy_HealsLowestHpAlly()
        {
            var healer = new EnemyDef
            {
                Id = "h", Name = "h", Ability = EnemyAbility.Healer, AbilityPower = 1.0,
                Base = new Stats { Hp = 500, Atk = 100, Int = 100, Def = 0 },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), PH(0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 500, atk: 1), PE(1)));
            setup.Enemies.Add(new EnemySlot(healer, new Position(0, 2)));
            var battle = new Battle(setup);
            var ally = EnemyUnit(battle, "e");
            var priest = EnemyUnit(battle, "h");

            Assert.Equal(Intent.Kind.Attack, battle.GetIntent(priest).Type); // 沒人受傷 → 攻擊
            ally.Hp = 300;
            Assert.Equal(Intent.Kind.Heal, battle.GetIntent(priest).Type);
            Assert.Same(ally, battle.GetIntent(priest).Target);
            battle.EndTurn();
            Assert.Equal(400, ally.Hp); // 治療 = 謀略 100 × 1.0
        }

        [Fact]
        public void Chapter1Levels_WinRateReport()
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
            {
                var auto = RunLevel(level, null);
                var smart = RunLevel(level, TutorialPriority);
                var ignore = RunLevel(level, IgnoreTutorialPriority);
                _out.WriteLine($"第 {level} 關 能出就出 {auto}｜照教學打 {smart}｜忽略教學 {ignore}");
            }
        }

        [Fact]
        public void DemoSampleBattle_WinRateReport()
        {
            int wins = 0, total = 200, turns = 0;
            for (ulong seed = 1; seed <= (ulong)total; seed++)
            {
                var battle = new Battle(DemoContent.SampleBattle(seed));
                if (AutoPlayer.RunToEnd(battle, 100) == BattleResult.Won) wins++;
                turns += battle.Turn;
            }
            _out.WriteLine($"自動對戰（能出就出）勝率 {wins}/{total} = {100.0 * wins / total:F1}%，平均 {turns / (double)total:F1} 回合");
            Assert.InRange(wins, 0, total);
        }
    }
}
