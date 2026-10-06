using System.Collections.Generic;

namespace SanGuo.Core
{
    /// <summary>
    /// Demo 用的蜀漢前期小隊與黃巾敵人（暫以程式碼定義；之後改由 ScriptableObject 匯出的 JSON 載入）。
    /// 數值已依自動對戰掃描調整（教學關：約 9 回合、勝率接近 100%），仍為佔位。
    /// </summary>
    public static class DemoContent
    {
        // ---- 卡牌建構輔助 ----

        private static EffectDef Dmg(double mult) => new EffectDef { Type = EffectType.Damage, Multiplier = mult };
        private static EffectDef Armor(double mult, bool self = true) => new EffectDef { Type = EffectType.Armor, Multiplier = mult, OnSelf = self };
        private static EffectDef Heal(double mult) => new EffectDef { Type = EffectType.Heal, Multiplier = mult };
        private static EffectDef Status(StatusType type, double power, int turns, bool self = false) =>
            new EffectDef { Type = EffectType.ApplyStatus, Status = type, Multiplier = power, Amount = turns, OnSelf = self };
        private static EffectDef Draw(int n) => new EffectDef { Type = EffectType.Draw, Amount = n, OnSelf = true };
        private static EffectDef Gain(int n) => new EffectDef { Type = EffectType.GainCost, Amount = n, OnSelf = true };

        /// <summary>專屬技能牌。</summary>
        private static CardDef Card(string id, string name, int cost, TargetRule target, Shape shape,
            CardKeywords keywords, params EffectDef[] effects)
        {
            return new CardDef
            {
                Id = id, Name = name, Cost = cost, Target = target, Shape = shape,
                Keywords = keywords, Effects = new List<EffectDef>(effects),
            };
        }

        /// <summary>基礎牌（普攻 / 防禦 / 治療）。</summary>
        private static CardDef Basic(string id, string name, int cost, TargetRule target, params EffectDef[] effects)
        {
            var card = Card(id, name, cost, target, Shape.Single, CardKeywords.None, effects);
            card.Basic = true;
            return card;
        }

        // ---- 武將 ----

        // 套牌原則：基礎牌（普攻 / 防禦 / 治療）為主 + 一兩張專屬技能。移動不在牌庫裡，是全隊共用的按鈕。

        public static HeroDef ZhangFei()
        {
            var attack = Basic("zf_attack", "長矛突刺", 1, TargetRule.EnemyFront, Dmg(1.0));
            var guard = Basic("zf_guard", "鐵壁", 1, TargetRule.Self, Armor(1.5));
            var taunt = Card("zf_taunt", "燕人怒吼", 1, TargetRule.Self, Shape.Single, CardKeywords.Innate,
                Status(StatusType.Taunt, 0, 3, self: true), Armor(3.0));
            var roar = Card("zf_roar", "當陽橋喝斷", 3, TargetRule.EnemyFront, Shape.Row, CardKeywords.Exhaust,
                Dmg(1.4), Status(StatusType.Stun, 0, 1));
            var breakArmor = Card("zf_break", "蛇矛破陣", 1, TargetRule.EnemyFront, Shape.Single, CardKeywords.None,
                Dmg(0.6), Status(StatusType.ArmorBreak, 0.5, 4));
            return new HeroDef
            {
                Id = "zhangfei", Name = "張飛", Role = Role.Tank, Rarity = Rarity.UR, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 1200, Atk = 135, Def = 70, Dodge = 0, Speed = 1, Crit = 5, CritDmg = 150 },
                Deck = new List<CardDef> { attack, guard, guard, taunt, roar, breakArmor },
            };
        }

        public static HeroDef GuanYu()
        {
            var attack = Basic("gy_attack", "青龍斬", 1, TargetRule.EnemyFront, Dmg(1.2));
            var guard = Basic("gy_guard", "持刀架勢", 1, TargetRule.Self, Armor(1.0));
            var sweep = Card("gy_sweep", "偃月橫掃", 2, TargetRule.EnemyFront, Shape.Row, CardKeywords.None, Dmg(0.9));
            var duel = Card("gy_duel", "溫酒斬將", 3, TargetRule.EnemyFront, Shape.Single, CardKeywords.Exhaust,
                Dmg(2.8), Status(StatusType.ArmorBreak, 0.25, 2));
            var breakArmor = Card("gy_break", "斷甲", 1, TargetRule.EnemyFront, Shape.Single, CardKeywords.Innate,
                Dmg(0.8), Status(StatusType.ArmorBreak, 0.5, 4));
            return new HeroDef
            {
                Id = "guanyu", Name = "關羽", Role = Role.Warrior, Rarity = Rarity.UR, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 960, Atk = 195, Def = 45, Dodge = 5, Speed = 2, Crit = 15, CritDmg = 180 },
                Deck = new List<CardDef> { attack, attack, guard, sweep, duel, breakArmor },
            };
        }

        public static HeroDef LiuBei()
        {
            var attack = Basic("lb_attack", "雙股劍", 1, TargetRule.EnemyFront, Dmg(0.9));
            var guard = Basic("lb_guard", "仁者護佑", 1, TargetRule.AllyLowestHp, Armor(1.8, self: false));
            var heal = Basic("lb_heal", "撫慰", 1, TargetRule.AllyLowestHp, Heal(1.5));
            var virtue = Card("lb_virtue", "仁德", 2, TargetRule.AllyLowestHp, Shape.Single, CardKeywords.None, Heal(3.0));
            var rally = Card("lb_rally", "桃園結義", 3, TargetRule.AllAllies, Shape.All, CardKeywords.Exhaust,
                Heal(1.5), Armor(1.0, self: false));
            return new HeroDef
            {
                Id = "liubei", Name = "劉備", Role = Role.Healer, Rarity = Rarity.UR, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 800, Atk = 120, Def = 35, Dodge = 5, Speed = 2, Crit = 5, CritDmg = 150 },
                Deck = new List<CardDef> { attack, guard, heal, virtue, rally },
            };
        }

        public static HeroDef ZhugeLiang()
        {
            var staff = Basic("zgl_attack", "羽扇輕搖", 1, TargetRule.EnemyBack, Dmg(1.0));
            var guard = Basic("zgl_guard", "八卦護身", 1, TargetRule.Self, Armor(0.9));
            var fire = Card("zgl_fire", "火計", 2, TargetRule.EnemyFront, Shape.Cross, CardKeywords.None,
                Dmg(0.7), Status(StatusType.Burn, 0.4, 3));
            var plan = Card("zgl_plan", "錦囊妙計", 0, TargetRule.Self, Shape.Single, CardKeywords.Exhaust, Draw(2));
            var tempo = Card("zgl_tempo", "運籌帷幄", 0, TargetRule.Self, Shape.Single, CardKeywords.Exhaust, Gain(2));
            return new HeroDef
            {
                Id = "zhugeliang", Name = "諸葛亮", Role = Role.Strategist, Rarity = Rarity.UR, AttackType = AttackType.Ranged,
                Base = new Stats { Hp = 680, Atk = 165, Def = 25, Dodge = 10, Speed = 1, Crit = 10, CritDmg = 150 },
                Deck = new List<CardDef> { staff, staff, guard, fire, plan, tempo },
            };
        }

        public static HeroDef ZhaoYun()
        {
            var strike = Basic("zy_attack", "龍膽突刺", 1, TargetRule.EnemyFront, Dmg(1.3));
            var guard = Basic("zy_guard", "游龍身法", 1, TargetRule.Self, Armor(0.8));
            var ult = Card("zy_ult", "七進七出", 3, TargetRule.EnemyFront, Shape.Column, CardKeywords.Exhaust, Dmg(2.0));
            return new HeroDef
            {
                Id = "zhaoyun", Name = "趙雲", Role = Role.Warrior, Rarity = Rarity.UR, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 840, Atk = 210, Def = 40, Dodge = 15, Speed = 3, Crit = 20, CritDmg = 170 },
                Deck = new List<CardDef> { strike, strike, guard, ult },
            };
        }

        public static HeroDef HuangZhong()
        {
            var shot = Basic("hz_attack", "穿雲箭", 1, TargetRule.EnemyBack, Dmg(1.2));
            var guard = Basic("hz_guard", "閃身", 1, TargetRule.Self, Armor(0.8));
            var snipe = Card("hz_snipe", "百步穿楊", 2, TargetRule.EnemyBack, Shape.Single, CardKeywords.Innate, Dmg(2.0));
            var pierce = Card("hz_pierce", "穿甲箭", 1, TargetRule.EnemyBack, Shape.Single, CardKeywords.Innate,
                Dmg(0.6), Status(StatusType.ArmorBreak, 0.5, 4));
            var volley = Card("hz_volley", "箭雨", 3, TargetRule.EnemyBack, Shape.Column, CardKeywords.Exhaust, Dmg(1.8));
            return new HeroDef
            {
                Id = "huangzhong", Name = "黃忠", Role = Role.Archer, Rarity = Rarity.UR, AttackType = AttackType.Ranged,
                Base = new Stats { Hp = 720, Atk = 200, Def = 30, Dodge = 8, Speed = 2, Crit = 20, CritDmg = 170 },
                Deck = new List<CardDef> { shot, shot, guard, snipe, volley, pierce },
            };
        }

        // ---- R 級基礎小兵（測試用的弱單位；只有基礎牌）----

        public static HeroDef MilitiaSoldier()
        {
            var attack = Basic("r_mil_attack", "揮砍", 1, TargetRule.EnemyFront, Dmg(1.0));
            var guard = Basic("r_mil_guard", "舉盾", 1, TargetRule.Self, Armor(1.0));
            return new HeroDef
            {
                Id = "r_militia", Name = "義勇兵", Role = Role.Warrior, Rarity = Rarity.R, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 600, Atk = 120, Def = 25, Dodge = 0, Speed = 2, Crit = 5, CritDmg = 150 },
                Deck = new List<CardDef> { attack, attack, guard },
            };
        }

        public static HeroDef MilitiaShield()
        {
            var attack = Basic("r_shd_attack", "盾擊", 1, TargetRule.EnemyFront, Dmg(0.8));
            var guard = Basic("r_shd_guard", "固守", 1, TargetRule.Self, Armor(1.2));
            return new HeroDef
            {
                Id = "r_shield", Name = "鄉勇盾兵", Role = Role.Tank, Rarity = Rarity.R, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 800, Atk = 90, Def = 50, Dodge = 0, Speed = 1, Crit = 0, CritDmg = 150 },
                Deck = new List<CardDef> { attack, guard, guard },
            };
        }

        public static HeroDef MilitiaArcher()
        {
            var shot = Basic("r_arc_attack", "射擊", 1, TargetRule.EnemyBack, Dmg(1.0));
            var guard = Basic("r_arc_guard", "閃避", 1, TargetRule.Self, Armor(0.8));
            return new HeroDef
            {
                Id = "r_archer", Name = "鄉勇弓手", Role = Role.Archer, Rarity = Rarity.R, AttackType = AttackType.Ranged,
                Base = new Stats { Hp = 450, Atk = 130, Def = 15, Dodge = 5, Speed = 2, Crit = 10, CritDmg = 150 },
                Deck = new List<CardDef> { shot, shot, guard },
            };
        }

        public static HeroDef MilitiaHealer()
        {
            var attack = Basic("r_hlr_attack", "棍擊", 1, TargetRule.EnemyFront, Dmg(0.6));
            var heal = Basic("r_hlr_heal", "包紮", 1, TargetRule.AllyLowestHp, Heal(1.5));
            return new HeroDef
            {
                Id = "r_healer", Name = "鄉勇醫士", Role = Role.Healer, Rarity = Rarity.R, AttackType = AttackType.Melee,
                Base = new Stats { Hp = 450, Atk = 100, Def = 15, Dodge = 5, Speed = 2, Crit = 0, CritDmg = 150 },
                Deck = new List<CardDef> { attack, heal, heal },
            };
        }

        /// <summary>所有可上場武將（編隊用），依稀有度與登場順序排列。</summary>
        public static List<HeroDef> Roster() => new List<HeroDef>
        {
            ZhangFei(), GuanYu(), HuangZhong(), LiuBei(), ZhugeLiang(), ZhaoYun(),
            MilitiaSoldier(), MilitiaShield(), MilitiaArcher(), MilitiaHealer(),
        };

        // ---- 敵人（黃巾）----

        public static EnemyDef YellowTurbanSoldier() => new EnemyDef
        {
            Id = "yt_soldier", Name = "黃巾兵", AttackType = AttackType.Melee, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 300, Atk = 160, Def = 20, Speed = 1, Crit = 0, CritDmg = 150 },
        };

        public static EnemyDef YellowTurbanArcher() => new EnemyDef
        {
            Id = "yt_archer", Name = "黃巾弓手", AttackType = AttackType.Ranged, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 200, Atk = 180, Def = 10, Speed = 1, Crit = 5, CritDmg = 150 },
        };

        /// <summary>第 2 關用的精準弓手：攻擊高、血量中等，每回合都會重創後排。</summary>
        public static EnemyDef YellowTurbanSharpshooter() => new EnemyDef
        {
            Id = "yt_sharpshooter", Name = "黃巾神射", AttackType = AttackType.Ranged, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 450, Atk = 470, Def = 10, Speed = 1, Crit = 5, CritDmg = 150 },
        };

        /// <summary>第 3 關用的鐵甲力士：防禦極高，不破甲幾乎打不動。</summary>
        public static EnemyDef YellowTurbanIronBrute() => new EnemyDef
        {
            Id = "yt_ironbrute", Name = "鐵甲力士", AttackType = AttackType.Melee, AttackMultiplier = 1.2,
            Base = new Stats { Hp = 900, Atk = 220, Def = 400, Speed = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第 4 關的妖道：脆皮，每回合治療血量比例最低的友軍；放在後排，要靠弓手才打得到。</summary>
        public static EnemyDef YellowTurbanPriest() => new EnemyDef
        {
            Id = "yt_priest", Name = "黃巾妖道", AttackType = AttackType.Ranged, AttackMultiplier = 0.8,
            Ability = EnemyAbility.Healer, AbilityPower = 1.5,
            Base = new Stats { Hp = 300, Atk = 200, Def = 10, Speed = 1, Crit = 0, CritDmg = 150 },
        };

        public static EnemyDef YellowTurbanBrute() => new EnemyDef
        {
            Id = "yt_brute", Name = "黃巾力士", AttackType = AttackType.Melee, AttackMultiplier = 1.3,
            Base = new Stats { Hp = 700, Atk = 200, Def = 50, Speed = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第一章關卡總數與名稱（見 docs/demo-chapter1.md）。</summary>
        public static readonly string[] LevelNames =
        {
            "涿郡義勇", "黃巾探子", "力士攔路", "妖道作亂", "渠帥來襲",
            "護送鄉民", "火燒連營", "符水妖術", "雙渠帥", "黃巾之首",
        };

        /// <summary>已實作的關卡數（其餘在地圖上顯示為尚未開放）。</summary>
        public const int ChapterLevelCount = 4;

        /// <summary>第一章「黃巾之亂」關卡（教學關：固定隊伍、不開放自動戰鬥）。</summary>
        public static BattleSetup Level(int level, ulong seed = 1)
        {
            // 第一章每一關都是教學關：不開放自動戰鬥。
            var setup = new BattleSetup { Seed = seed, AutoAllowed = false };
            if (level == 3) setup.TurnLimit = 11;
            // 教學關：小兵湊數，只讓一兩名武將帶著該關要教的技能卡上場（編隊鎖定，之後再開放）。
            setup.FormationLocked = true;
            switch (level)
            {
                case 1: // 純小兵
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), new Position(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), new Position(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), new Position(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), new Position(2, 1)));
                    break;
                case 2: // 張飛（燕人怒吼・先登）
                    setup.Heroes.Add(new HeroSlot(ZhangFei(), new Position(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), new Position(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), new Position(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), new Position(2, 1)));
                    break;
                case 4: // 黃忠（百步穿楊・先登）專打後排的妖道
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), new Position(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), new Position(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), new Position(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), new Position(2, 1)));
                    break;
                default: // 關羽（斷甲・先登）＋黃忠（穿甲箭・先登）
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), new Position(0, 0)));
                    setup.Heroes.Add(new HeroSlot(GuanYu(), new Position(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), new Position(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), new Position(2, 1)));
                    break;
            }
            switch (level)
            {
                case 1: // 涿郡義勇：出牌與費用（教學）
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(2, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(3, 0)));
                    break;
                case 2: // 黃巾探子：兩名神射手專打後排；要靠張飛的挑釁把火力拉到前排
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSharpshooter(), new Position(2, 1)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSharpshooter(), new Position(3, 1)));
                    break;
                case 3: // 力士攔路：鐵甲力士擋在最上路，同路沒人時全隊火力都落在牠身上；要疊破甲才打得動
                    setup.Enemies.Add(new EnemySlot(YellowTurbanIronBrute(), new Position(0, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(0, 1)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(3, 0)));
                    break;
                case 4: // 妖道作亂：妖道躲在後排持續治療，只有弓手打得到
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(0, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(2, 0)));
                    setup.Enemies.Add(new EnemySlot(YellowTurbanPriest(), new Position(2, 1)));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return setup;
        }

        /// <summary>範例關卡：劉關張 + 諸葛亮 對 黃巾兵 / 弓手 / 力士。</summary>
        public static BattleSetup SampleBattle(ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed };
            setup.Heroes.Add(new HeroSlot(ZhangFei(), new Position(1, 0)));
            setup.Heroes.Add(new HeroSlot(GuanYu(), new Position(2, 0)));
            setup.Heroes.Add(new HeroSlot(LiuBei(), new Position(3, 1)));
            setup.Heroes.Add(new HeroSlot(ZhugeLiang(), new Position(2, 1)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(1, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanBrute(), new Position(2, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), new Position(3, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanArcher(), new Position(2, 1)));
            return setup;
        }
    }
}
