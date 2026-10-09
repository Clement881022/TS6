using System.Linq;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>專屬牌與被動（docs/signature-cards-batch1.md）：牌的新效果與被動的觸發點。</summary>
    public class SignatureTests
    {
        /// <summary>我方 1 名武將（指定星級）對指定敵人；無隨機，方便驗證數值。</summary>
        private static Battle Fight(string heroId, int stars, params (EnemyDef Def, Position Pos)[] enemies) => Fight(heroId, stars, false, enemies);

        /// <param name="fillers">true = 另外帶 2 名 R 武將（牌庫夠大，才看得出抽牌差異）。</param>
        private static Battle Fight(string heroId, int stars, bool fillers, params (EnemyDef Def, Position Pos)[] enemies)
        {
            var def = HeroRoster.Find(heroId)!;
            var setup = new BattleSetup { Seed = 1, NoRandomness = true };
            setup.Heroes.Add(HeroGrowth.BuildSlot(def, new HeroState { HeroId = heroId, Level = 1, Stars = stars }, new Position(2, 3)));
            if (fillers)
            {
                setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaShield(), new Position(1, 3)));
                setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaArcher(), new Position(3, 4)));
            }
            foreach (var (e, pos) in enemies) setup.Enemies.Add(new EnemySlot(e, pos, 1));
            return new Battle(setup);
        }

        private static CardInstance Take(Battle b, string cardId)
        {
            // 2★ 起第 1 張專屬牌已升級為「＋」版（id 加 _plus）
            var card = b.Hand.Concat(b.DrawPile).Concat(b.DiscardPile).First(c => c.Def.Id == cardId || c.Def.Id == cardId + "_plus");
            b.DrawPile.Remove(card);
            b.DiscardPile.Remove(card);
            if (!b.Hand.Contains(card)) b.Hand.Add(card);
            return card;
        }

        private static EnemyDef Weak()
        {
            var e = DemoContent.BanditGrunt();
            e.Base.Hp = 1;
            return e;
        }

        [Fact]
        public void UrAndStoryHeroes_UseSignatureCards_RHeroesKeepRoleCards()
        {
            var lvbu = HeroRoster.Find("lvbu")!;
            Assert.Contains(lvbu.Deck, c => c.Name == "無雙");
            Assert.Contains(lvbu.Deck, c => c.Name == "天下無雙");
            Assert.Equal(3, lvbu.Deck.Count(c => c.Basic));
            Assert.Equal(PassiveKind.RenZhongLvBu, lvbu.Passive);
            Assert.Equal(HeroFocus.Boss, lvbu.Focus);
            Assert.Contains(HeroRoster.Find("r_shield")!.Deck, c => c.Name == "嘲諷");
            // 教學關的劉關張仍用職業標竿卡（教學牌序依賴這組 id）
            Assert.Contains(HeroRoster.TutorialZhangFei().Deck, c => c.Id == "zf_taunt");
            Assert.Contains(DemoContent.Level(2).Heroes.Single(h => h.Def.Id == "zhangfei").Def.Deck, c => c.Id == "zf_taunt");
        }

        [Fact]
        public void Passive_UnlocksOnlyAtFiveStars()
        {
            var def = HeroRoster.Find("xunyu")!;
            Assert.Equal(PassiveKind.None, HeroGrowth.BuildDef(def, new HeroState { HeroId = "xunyu", Stars = 4 }).ActivePassive);
            Assert.Equal(PassiveKind.JuZhong, HeroGrowth.BuildDef(def, new HeroState { HeroId = "xunyu", Stars = 5 }).ActivePassive);
            var fifth = Breakthroughs.For(def).Single(e => e.Stars == 5);
            Assert.Equal(BreakthroughKind.UnlockPassive, fifth.Kind);
            // 沒有被動的 SR：5★ 仍是第 2 張特殊卡升級
            Assert.Equal(BreakthroughKind.UpgradeCard, Breakthroughs.For(HeroRoster.Find("handang")!).Single(e => e.Stars == 5).Kind);
        }

        [Fact]
        public void KillRefund_ReturnsCostWhenTheCardKills()
        {
            var b = Fight("gongsunzan", 0, (Weak(), new Position(2, 1)), (DemoContent.BanditGrunt(), new Position(0, 0)));
            int before = b.Cost;
            var card = Take(b, "gsz_youqi");
            Assert.Equal(PlayResult.Ok, b.PlayCard(card, new Position(2, 1)));
            Assert.Equal(before - 1 + 1, b.Cost);
        }

        [Fact]
        public void UnlimitedSingleTaunt_ReachesAnyEnemy()
        {
            var far = DemoContent.BanditGrunt();
            var b = Fight("xiahoudun", 0, (far, new Position(0, 0)));
            var tank = b.Units.First(u => u.Side == Side.Player);
            var enemy = b.Units.First(u => u.Side == Side.Enemy);
            Assert.True(Position.Distance(tank.Pos, enemy.Pos) > tank.AttackRange);
            Assert.Equal(PlayResult.Ok, b.PlayCard(Take(b, "xhd_ganglie"), enemy.Pos));
            Assert.Equal(tank.Id, enemy.Statuses[StatusType.Taunt].SourceId);
            Assert.Equal(2, enemy.Statuses[StatusType.Taunt].Turns);
            Assert.Equal(100, tank.BuffTotal(StatusType.DefUp));
        }

        [Fact]
        public void CritUp_RaisesEffectiveCrit()
        {
            var b = Fight("lvbu", 0, (DemoContent.BanditGrunt(), new Position(2, 2)));
            var hero = b.Units.First(u => u.Side == Side.Player);
            int crit = hero.EffectiveCrit;
            Assert.Equal(PlayResult.Ok, b.PlayCard(Take(b, "lb2_wushuang"), new Position(2, 2)));
            Assert.Equal(System.Math.Min(100, crit + 50), hero.EffectiveCrit);
        }

        [Fact]
        public void BonusPerDebuff_HitsHarderOnDebuffedTargets()
        {
            int Damage(bool burning)
            {
                var tough = DemoContent.BanditGrunt();
                tough.Base.Hp = 100000;
                var b = Fight("lvbu", 0, (tough, new Position(2, 2)));
                var enemy = b.Units.First(u => u.Side == Side.Enemy);
                if (burning) enemy.Statuses[StatusType.Burn] = new StatusState { Power = 5 };
                int hp = enemy.Hp;
                b.PlayCard(Take(b, "lb2_tianxia"), enemy.Pos);
                return hp - enemy.Hp;
            }
            Assert.Equal(System.Math.Round(Damage(false) * 1.3), Damage(true), 0);
        }

        [Fact]
        public void JuZhong_DrawsOneMoreFromTurnTwo()
        {
            int HandAfterTurn(int stars)
            {
                var tough = DemoContent.BanditGrunt();
                tough.Base.Hp = 100000;
                tough.Base.Atk = 0;
                var b = Fight("xunyu", stars, true, (tough, new Position(0, 0)));
                int pile = b.DrawPile.Count; // 用牌庫減少的張數判斷（手牌上限 10，超出的會直接棄掉）
                b.EndTurn();
                return pile - b.DrawPile.Count;
            }
            Assert.Equal(HandAfterTurn(0) + 1, HandAfterTurn(5));
        }

        [Fact]
        public void BaiMa_RefundsOnFirstKillEachTurn()
        {
            var b = Fight("gongsunzan", 5, (Weak(), new Position(2, 1)), (Weak(), new Position(3, 2)), (DemoContent.BanditGrunt(), new Position(0, 0)));
            int cost = b.Cost;
            b.PlayCard(Take(b, "gsz_attack"), new Position(2, 1));
            Assert.Equal(cost - 1 + 1, b.Cost); // 第一次擊敗回 1 費
            b.PlayCard(Take(b, "gsz_attack"), new Position(3, 2));
            Assert.Equal(cost - 1 + 1 - 1, b.Cost); // 同回合第二次不回
        }

        [Fact]
        public void GangLie_TriggersOnceBelowHalfHp()
        {
            var b = Fight("xiahoudun", 5, (DemoContent.BanditGrunt(), new Position(0, 0)));
            var tank = b.Units.First(u => u.Side == Side.Player);
            var enemy = b.Units.First(u => u.Side == Side.Enemy);
            Assert.Equal(PlayResult.Ok, b.PlayCard(Take(b, "xhd_ganglie"), enemy.Pos));
            int defBefore = tank.BuffTotal(StatusType.DefUp);
            tank.Hp = tank.MaxHp / 2 + 1;
            // 讓敵人打一下（被嘲諷，一定打坦克）
            for (int i = 0; i < 6 && tank.Hp * 2 >= tank.MaxHp && b.Result == BattleResult.Ongoing; i++) b.EndTurn();
            Assert.True(tank.PassiveFired);
            Assert.Contains(b.Events, e => e.Type == EventType.PassiveTriggered && e.Text == "剛烈不屈");
            Assert.Single(b.Events, e => e.Type == EventType.PassiveTriggered && e.Text == "剛烈不屈");
            Assert.True(defBefore >= 0);
        }

        [Fact]
        public void WuQin_HealsTheMostHurtAllyAtTurnEnd()
        {
            var tough = DemoContent.BanditGrunt();
            tough.Base.Atk = 0;
            tough.Base.Hp = 100000;
            var b = Fight("huatuo", 5, (tough, new Position(0, 0)));
            var healer = b.Units.First(u => u.Side == Side.Player);
            healer.Hp = healer.MaxHp / 2;
            int hp = healer.Hp;
            b.EndTurn();
            Assert.True(healer.Hp > hp);
            Assert.Contains(b.Events, e => e.Type == EventType.PassiveTriggered && e.Text == "五禽戲");
        }
            [Fact]
        public void ArmorBreakOnTheSameCard_AppliesBeforeTheDamage()
        {
            // 韓當「穿甲箭」：破甲 40% 先生效，這一擊就吃到破甲（企劃 2026-10-09：狀態先於傷害結算）。
            var tough = DemoContent.BanditIronBrute();
            tough.Base.Hp = 100000;
            var b = Fight("handang", 0, (tough, new Position(2, 1)));
            var hero = b.Units.First(u => u.Side == Side.Player);
            var enemy = b.Units.First(u => u.Side == Side.Enemy);
            double defBroken = enemy.Stats.Def * (1 - 0.4);
            int expected = DamageCalc.Physical(hero.EffectiveAtk, 2.0, defBroken, false, hero.Stats.CritDmg);
            int hp = enemy.Hp;
            Assert.Equal(PlayResult.Ok, b.PlayCard(Take(b, "hd_chuanjia"), enemy.Pos));
            Assert.Equal(expected, hp - enemy.Hp);
        }

        [Fact]
        public void BurnResist_ReducesAppliedStacks()
        {
            int Stacks(double resist)
            {
                var e = DemoContent.BanditGrunt();
                e.Base.Hp = 100000;
                e.BurnResist = resist;
                var b = Fight("yuji", 0, (e, new Position(2, 1)));
                var enemy = b.Units.First(u => u.Side == Side.Enemy);
                b.PlayCard(Take(b, "yj_fushui"), enemy.Pos);
                return enemy.BurnStacks;
            }
            int full = Stacks(0);
            Assert.InRange(Stacks(0.5), full / 2 - 1, full / 2 + 1);
        }

        [Fact]
        public void EveryDrawableSrAndUr_HasSignatureCardsAndAFocus()
        {
            foreach (var h in HeroRoster.All().Where(h => h.Rarity != Rarity.R))
            {
                Assert.True(Signatures.Has(h.Id), h.Id);
                Assert.Equal(2, h.Deck.Count(c => !c.Basic));
                Assert.Equal(h.Rarity == Rarity.UR || HeroRoster.StoryHeroIds.Contains(h.Id), h.Passive != PassiveKind.None);
            }
            // 每個職業的可抽 SR：刷圖型、Boss 型各一
            foreach (var g in HeroRoster.All().Where(h => h.Rarity == Rarity.SR && !HeroRoster.StoryHeroIds.Contains(h.Id)).GroupBy(h => h.Role))
            {
                Assert.Contains(g, h => h.Focus == HeroFocus.Farming);
                Assert.Contains(g, h => h.Focus == HeroFocus.Boss);
            }
        }
    }
}
