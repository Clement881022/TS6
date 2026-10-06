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

        private static CardDef Attack(string id = "atk", int cost = 1, TargetRule target = TargetRule.EnemyFront,
            Shape shape = Shape.Single, double mult = 1.0, CardKeywords kw = CardKeywords.None) =>
            new CardDef
            {
                Id = id, Name = id, Cost = cost, Target = target, Shape = shape, Keywords = kw,
                Effects = { new EffectDef { Type = EffectType.Damage, Multiplier = mult } },
            };

        private static HeroDef Hero(string name, IEnumerable<CardDef> deck, int atk = 100, int hp = 1000, int def = 0,
            int speed = 1, AttackType type = AttackType.Melee) =>
            new HeroDef
            {
                Id = name, Name = name, AttackType = type,
                Base = new Stats { Hp = hp, Atk = atk, Def = def, Speed = speed, Crit = 0, Dodge = 0 },
                Deck = deck.ToList(),
            };

        private static EnemyDef Enemy(string name, int hp = 500, int atk = 50, int def = 0, int speed = 1,
            AttackType type = AttackType.Melee) =>
            new EnemyDef
            {
                Id = name, Name = name, AttackType = type,
                Base = new Stats { Hp = hp, Atk = atk, Def = def, Speed = speed, Crit = 0, Dodge = 0 },
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
                Id = id, Name = id, Cost = 0, Target = TargetRule.EnemyFront,
                Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.ArmorBreak, Multiplier = pct, Amount = turns } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Breaker("b40", 0.4, 1), Breaker("b15", 0.15, 3), Attack() }, atk: 100, hp: 99999), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1, def: 100), new Position(0, 0)));
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
            var cross = Targeting.ExpandShape(new Position(2, 0), Shape.Cross, 5, 2);
            Assert.Equal(4, cross.Count); // 中心 + 左右 + 後排（前排無更前）
            Assert.Contains(cross, p => p.Lane == 1 && p.Row == 0);
            Assert.Contains(cross, p => p.Lane == 3 && p.Row == 0);
            Assert.Contains(cross, p => p.Lane == 2 && p.Row == 1);

            Assert.Equal(5, Targeting.ExpandShape(new Position(2, 1), Shape.Row, 5, 2).Count);
            Assert.Equal(2, Targeting.ExpandShape(new Position(2, 1), Shape.Column, 5, 2).Count);
            Assert.Equal(10, Targeting.ExpandShape(new Position(0, 0), Shape.All, 5, 2).Count);
        }

        [Fact]
        public void Target_IsFrontMostInSameLane_ElseTopDownFallback()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("front"), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("back"), new Position(2, 1)));
            setup.Enemies.Add(new EnemySlot(Enemy("otherLane"), new Position(0, 0)));
            var battle = new Battle(setup);

            var targets = battle.ResolveTargets(battle.Units[0], battle.Hand[0].Def)!;
            Assert.Single(targets);
            Assert.Equal("front", targets[0].Name);

            // 同路沒有敵人 → 由上往下（第 0 路起）找第一條有敵人的路，打最前排。
            var setup2 = new BattleSetup();
            setup2.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), new Position(4, 0)));
            setup2.Enemies.Add(new EnemySlot(Enemy("low"), new Position(3, 0)));
            setup2.Enemies.Add(new EnemySlot(Enemy("top"), new Position(1, 0)));
            setup2.Enemies.Add(new EnemySlot(Enemy("topBack"), new Position(1, 1)));
            var battle2 = new Battle(setup2);
            var fallback = battle2.ResolveTargets(battle2.Units[0], battle2.Hand[0].Def)!;
            Assert.Single(fallback);
            Assert.Equal("top", fallback[0].Name);
        }

        [Fact]
        public void BackTargeting_PrefersBackRow()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(target: TargetRule.EnemyBack) }), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("front"), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("back"), new Position(2, 1)));
            var battle = new Battle(setup);
            var targets = battle.ResolveTargets(battle.Units[0], battle.Hand[0].Def)!;
            Assert.Equal("back", targets[0].Name);
        }

        // ---- 費用與牌庫 ----

        [Fact]
        public void Cost_StartsAtThree_StacksAndCapsAtTen()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle = new Battle(setup);
            Assert.Equal(3, battle.Cost);
            battle.EndTurn();
            Assert.Equal(6, battle.Cost);
            for (int i = 0; i < 10; i++) battle.EndTurn();
            Assert.Equal(10, battle.Cost);
        }

        [Fact]
        public void Hand_DrawsFive_DiscardPileReshufflesIntoDrawPile()
        {
            var deck = Enumerable.Range(0, 7).Select(i => Attack("c" + i)).ToList();
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle = new Battle(setup);
            Assert.Equal(5, battle.Hand.Count);
            Assert.Equal(2, battle.DrawPile.Count);

            battle.EndTurn(); // 手牌 5 張進棄牌堆，抽牌堆剩 2 → 抽 2 後洗回再抽 3
            Assert.Equal(5, battle.Hand.Count);
            Assert.Equal(7, battle.Hand.Count + battle.DrawPile.Count + battle.DiscardPile.Count);
        }

        private static Battle SmallBattle(params CardDef[] deck)
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(0, 0)));
            return new Battle(setup);
        }

        [Fact]
        public void Keyword_Exhaust_MovesCardToExhaustPile()
        {
            var battle = SmallBattle(Attack("ex", cost: 0, kw: CardKeywords.Exhaust));
            var card = battle.Hand[0];
            battle.PlayCard(card);
            Assert.Contains(card, battle.ExhaustPile);
            Assert.DoesNotContain(card, battle.DiscardPile);
        }

        [Fact]
        public void Keyword_Retain_StaysInHandAtTurnEnd()
        {
            var battle = SmallBattle(Attack("re", cost: 0, kw: CardKeywords.Retain), Attack("plain", cost: 0));
            var retained = battle.Hand.First(c => c.Def.Id == "re");
            battle.EndTurn();
            Assert.Contains(retained, battle.Hand);
        }

        [Fact]
        public void Keyword_Innate_AlwaysInOpeningHand()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var deck = new List<CardDef> { Attack("in", kw: CardKeywords.Innate) };
                deck.AddRange(Enumerable.Range(0, 20).Select(i => Attack("f" + i)));
                var setup = new BattleSetup { Seed = seed };
                setup.Heroes.Add(new HeroSlot(Hero("H", deck, hp: 99999), new Position(0, 0)));
                setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(0, 0)));
                var battle = new Battle(setup);
                Assert.Contains(battle.Hand, c => c.Def.Id == "in");
            }
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
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { guard }, atk: 100), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", atk: 30, hp: 99999), new Position(0, 0)));
            var battle = new Battle(setup);
            var hero = battle.Units[0];

            battle.PlayCard(battle.Hand[0]);
            Assert.Equal(100, hero.Armor);
            battle.EndTurn(); // 敵人攻擊 30 → 護甲 70，血量不變
            Assert.Equal(70, hero.Armor);
            Assert.Equal(hero.MaxHp, hero.Hp);
        }

        [Fact]
        public void DeadHero_CardsCannotBePlayed()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack("a") }, hp: 10), new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack("b") }), new Position(1, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", atk: 999, hp: 99999), new Position(0, 0)));
            var battle = new Battle(setup);
            var cardA = battle.Hand.First(c => c.Owner.Name == "A");

            battle.EndTurn(); // 敵人殺死 A
            Assert.False(battle.Units[0].Alive);
            Assert.Equal(PlayResult.OwnerDead, battle.CanPlay(cardA));
        }

        [Fact]
        public void Stunned_HeroCannotPlayCards()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), new Position(0, 0)));
            var battle = new Battle(setup);
            battle.Units[0].Statuses[StatusType.Stun] = new StatusState { Turns = 1 };
            Assert.Equal(PlayResult.Stunned, battle.CanPlay(battle.Hand[0]));
        }

        [Fact]
        public void Burn_DealsDamageAtStartOfOwnPhase_AndExpires()
        {
            var fire = new CardDef
            {
                Id = "fire", Name = "fire", Cost = 1, Target = TargetRule.EnemyFront,
                Effects = { new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.Burn, Multiplier = 0.5, Amount = 2 } },
            };
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { fire }, atk: 100, hp: 99999), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 1000, atk: 1), new Position(0, 0)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            battle.PlayCard(battle.Hand[0]);
            battle.EndTurn(); // 敵方階段灼燒 50
            Assert.Equal(950, enemy.Hp);
            battle.EndTurn(); // 第二次灼燒（持續 2 輪）
            Assert.Equal(900, enemy.Hp);
            battle.EndTurn();
            Assert.Equal(900, enemy.Hp); // 已到期
        }

        // ---- 移動 ----

        [Fact]
        public void MoveButton_RespectsSpeed_AndSwapsWithAlly()
        {
            var setup = new BattleSetup { MovesPerTurn = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, speed: 2), new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack() }), new Position(1, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle = new Battle(setup);
            var a = battle.Units[0];
            var b = battle.Units[1];

            Assert.Equal(PlayResult.InvalidMove, battle.Move(a, new Position(3, 0))); // 超出速度
            Assert.Equal(3, battle.Cost);                                              // 失敗不扣費
            Assert.Equal(PlayResult.Ok, battle.Move(a, new Position(1, 0)));           // 與隊友換位
            Assert.Equal(new Position(1, 0), a.Pos);
            Assert.Equal(new Position(0, 0), b.Pos);
            Assert.Same(a, battle.UnitAt(Side.Player, new Position(1, 0)));
            Assert.Equal(2, battle.Cost);                                              // 花 1 費
        }

        [Fact]
        public void MoveButton_OncePerTurn_SharedByWholeTeam_ResetsNextTurn()
        {
            var setup = new BattleSetup { MovesPerTurn = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, speed: 2), new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero("B", new[] { Attack() }, speed: 2), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle = new Battle(setup);
            var a = battle.Units[0];
            var b = battle.Units[1];

            Assert.Equal(PlayResult.Ok, battle.Move(a, new Position(1, 0)));
            Assert.Equal(PlayResult.MoveUsed, battle.CanMove(b));          // 全隊共用一次
            Assert.Equal(PlayResult.MoveUsed, battle.Move(b, new Position(3, 0)));
            battle.EndTurn();
            Assert.Equal(PlayResult.Ok, battle.Move(b, new Position(3, 0))); // 下回合重置
        }

        [Fact]
        public void MoveButton_NeedsCost_AndBlockedByStunOrDeath()
        {
            var setup = new BattleSetup { CostPerTurn = 0, MovesPerTurn = 1 };
            setup.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, speed: 2), new Position(0, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle = new Battle(setup);
            var a = battle.Units[0];
            Assert.Equal(PlayResult.NotEnoughCost, battle.CanMove(a)); // 費用 0

            var setup2 = new BattleSetup { MovesPerTurn = 1 };
            setup2.Heroes.Add(new HeroSlot(Hero("A", new[] { Attack() }, speed: 2), new Position(0, 0)));
            setup2.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(4, 0)));
            var battle2 = new Battle(setup2);
            battle2.Units[0].Statuses[StatusType.Stun] = new StatusState { Turns = 1 };
            Assert.Equal(PlayResult.Stunned, battle2.CanMove(battle2.Units[0]));
            battle2.Units[0].Alive = false;
            Assert.Equal(PlayResult.OwnerDead, battle2.CanMove(battle2.Units[0]));
        }

        // ---- 敵方 AI ----

        [Fact]
        public void EnemyAttacksTopLaneWhenSameLaneEmpty()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Top", new[] { Attack() }, hp: 99999), new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero("Low", new[] { Attack() }, hp: 99999), new Position(3, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), new Position(1, 0)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");

            // 同路沒人 → 打最上方那路（預設坦克位），不需要移動。
            var intent = battle.GetIntent(enemy);
            Assert.Equal(Intent.Kind.Attack, intent.Type);
            Assert.Equal("Top", intent.Target!.Name);
            int hpBefore = battle.Units[0].Hp;
            battle.EndTurn();
            Assert.True(battle.Units[0].Hp < hpBefore);
        }

        [Fact]
        public void Taunt_ForcesEnemyToAttackTauntingHero()
        {
            var setup = new BattleSetup();
            setup.Heroes.Add(new HeroSlot(Hero("Tank", new[] { Attack() }, hp: 99999), new Position(0, 0)));
            setup.Heroes.Add(new HeroSlot(Hero("Squishy", new[] { Attack() }, hp: 99999), new Position(1, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999), new Position(1, 0)));
            var battle = new Battle(setup);
            var enemy = EnemyUnit(battle, "e");
            var tank = battle.Units[0];

            // 沒挑釁：敵人打自己同路的 Squishy。
            Assert.Equal("Squishy", battle.GetIntent(enemy).Target!.Name);

            tank.Statuses[StatusType.Taunt] = new StatusState { Turns = 2 };
            var intent = battle.GetIntent(enemy);
            Assert.Equal(Intent.Kind.Attack, intent.Type); // 不分路，強制打挑釁者
            Assert.Equal("Tank", intent.Target!.Name);
        }

        // ---- 勝負與完整對戰 ----

        [Fact]
        public void KillingAllEnemies_Wins_AllHeroesDead_Loses()
        {
            var win = new BattleSetup();
            win.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack(mult: 100) }), new Position(0, 0)));
            win.Enemies.Add(new EnemySlot(Enemy("e", hp: 10), new Position(0, 0)));
            var b1 = new Battle(win);
            b1.PlayCard(b1.Hand[0]);
            Assert.Equal(BattleResult.Won, b1.Result);

            var lose = new BattleSetup();
            lose.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 10), new Position(0, 0)));
            lose.Enemies.Add(new EnemySlot(Enemy("e", atk: 999, hp: 99999), new Position(0, 0)));
            var b2 = new Battle(lose);
            b2.EndTurn();
            Assert.Equal(BattleResult.Lost, b2.Result);
        }

        [Fact]
        public void TurnLimit_LosesWhenExceeded()
        {
            var setup = new BattleSetup { TurnLimit = 3 };
            setup.Heroes.Add(new HeroSlot(Hero("H", new[] { Attack() }, hp: 99999), new Position(4, 0)));
            setup.Enemies.Add(new EnemySlot(Enemy("e", hp: 99999, atk: 1), new Position(0, 0)));
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
        private static int TutorialPriority(Battle battle, CardInstance c)
        {
            if (IsTaunt(c)) return battle.AliveUnits(Side.Player).Any(u => u.Has(StatusType.Taunt)) ? -1 : 4; // 已在挑釁中就不重複出
            return IsBreak(c) ? 3 : 0;
        }

        /// <summary>模擬「忽略教學」：不出挑釁、不出破甲，其他照出。</summary>
        private static int IgnoreTutorialPriority(Battle battle, CardInstance c) => IsTaunt(c) || IsBreak(c) ? -1 : 0;

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
            Assert.True(smart.WinRate >= 95, $"照教學打勝率太低（目標 100%）：{smart}");
            Assert.True(ignore.WinRate <= 50, $"忽略教學勝率太高：{ignore}");
        }

        // TODO 第 2 關（挑釁）：目前照教學打 ~94%、忽略教學 ~82%，挑釁的價值在現行數值下不明顯，
        // 尚未達成「照教學 100%、忽略教學會輸」，所以只在報表裡記錄，不做斷言。

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
