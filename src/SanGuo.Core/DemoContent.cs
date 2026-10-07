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
        private static EffectDef Detonate(StatusType type, int spreadTurns) => new EffectDef { Type = EffectType.Detonate, Status = type, Amount = spreadTurns };
        private static EffectDef StunGauge(int n) => new EffectDef { Type = EffectType.StunGauge, Amount = n };
        private static EffectDef Gain(int n) => new EffectDef { Type = EffectType.GainCost, Amount = n, OnSelf = true };

        /// <summary>專屬技能牌。</summary>
        private static CardDef Card(string id, string name, int cost, TargetRule target, int range, Shape shape,
            CardKeywords keywords, params EffectDef[] effects)
        {
            return new CardDef
            {
                Id = id, Name = name, Cost = cost, Target = target, Range = range, Shape = shape,
                Keywords = keywords, Effects = new List<EffectDef>(effects),
            };
        }

        /// <summary>基礎牌（普攻 / 防禦 / 治療）。</summary>
        private static CardDef Basic(string id, string name, int cost, TargetRule target, int range, params EffectDef[] effects)
        {
            var card = Card(id, name, cost, target, range, Shape.Single, CardKeywords.None, effects);
            card.Basic = true;
            return card;
        }

        // ---- 站位：舊關卡資料以「路 / 排」表示，這裡映射到共用 5x5 棋盤（敵方在上兩列、我方在下兩列）----

        /// <summary>我方站位：舊的 (路 0–2, 排 0 前 / 1 後) → 列陣區（欄 1–3、列 3 前 / 4 後）。</summary>
        public static Position HeroPos(int lane, int row) => new Position(lane + BattleSetup.FormationMinLane, BattleSetup.FormationMinRow + row);

        /// <summary>敵方站位：舊的 (路 0–4, 排 0 前 / 1 後) → 欄 0–4、列 1 前 / 0 後。</summary>
        public static Position EnemyPos(int lane, int row) => new Position(lane, 1 - row);

        // ---- 武將 ----

        // 初始套牌規則：每名武將固定 DeckSize 張，厚度一致；稀有度只決定「高級牌」換掉幾張普通攻擊：
        //   R = 0 張（全是普通攻擊）、SR = 1 張、UR = 2 張。
        // 高級牌由職業決定（見 RoleSkills，SR 拿第一張、UR 兩張都拿），同職業先共用同一組；
        // 之後要做武將專屬技能，直接在武將定義裡換掉對應那張即可。移動卡不在套牌裡：開局時每名武將另外洗入一張（見 Battle 建構子）。

        /// <summary>每名武將的初始套牌張數。</summary>
        public const int DeckSize = 6;

        private static double BasicAttackMultiplier(Role role)
        {
            switch (role)
            {
                case Role.Warrior:
                case Role.Archer: return 1.2;
                case Role.Healer: return 0.8;
                default: return 1.0;
            }
        }

        /// <summary>普通攻擊的射程：遠程職業（弓手 / 法師 / 軍師）3 格，近戰職業只能打周圍 1 格。</summary>
        private static int BasicAttackRange(Role role) =>
            role == Role.Archer || role == Role.Mage || role == Role.Strategist ? Unit.RangedRange : Unit.MeleeRange;

        /// <summary>職業的高級牌（依序：SR 拿第 1 張、UR 兩張都拿）。<paramref name="p"/> 是武將的卡牌 id 前綴。</summary>
        private static List<CardDef> RoleSkills(string p, Role role)
        {
            switch (role)
            {
                case Role.Tank:
                    return new List<CardDef>
                    {
                        Basic(p + "_stance", "防禦姿態", 1, TargetRule.Self, 0, Status(StatusType.DefUp, 1.0, 2, self: true)),
                        Card(p + "_taunt", "嘲諷", 1, TargetRule.Self, 0, Shape.Single, CardKeywords.Innate,
                            Status(StatusType.Taunt, 0, 3, self: true), Status(StatusType.DefUp, 0.3, 3, self: true)),
                    };
                case Role.Warrior:
                    return new List<CardDef>
                    {
                        Card(p + "_sweep", "旋風斬", 2, TargetRule.Enemy, 1, Shape.Row, CardKeywords.None, Dmg(1.0)),
                        Card(p + "_cleave", "豎劈斬", 2, TargetRule.Enemy, 1, Shape.Column, CardKeywords.None, Dmg(1.5)),
                    };
                case Role.Healer:
                    return new List<CardDef>
                    {
                        Basic(p + "_heal", "治療", 1, TargetRule.AllyLowestHp, 2, Heal(1.5)),
                        Basic(p + "_shield", "上盾", 1, TargetRule.AllyLowestHp, 2, Armor(1.8, self: false)),
                    };
                case Role.Strategist:
                    return new List<CardDef>
                    {
                        Card(p + "_atkup", "攻擊鼓舞", 1, TargetRule.AllAllies, 0, Shape.All, CardKeywords.None,
                            Status(StatusType.AtkUp, 0.3, 2)),
                        Card(p + "_critup", "暴擊鼓舞", 1, TargetRule.AllAllies, 0, Shape.All, CardKeywords.None,
                            Status(StatusType.CritUp, 0.25, 2)),
                    };
                case Role.Archer:
                    return new List<CardDef>
                    {
                        Card(p + "_snipe", "狙擊", 2, TargetRule.EnemyLowestHp, 4, Shape.Single, CardKeywords.None, Dmg(2.0)),
                        Card(p + "_pierce", "破甲箭", 1, TargetRule.Enemy, 3, Shape.Single, CardKeywords.None,
                            Dmg(0.6), Status(StatusType.ArmorBreak, 0.5, 4)),
                    };
                case Role.Mage:
                    return new List<CardDef>
                    {
                        Card(p + "_fire", "火計", 2, TargetRule.Enemy, 3, Shape.Cross, CardKeywords.None,
                            Dmg(0.7), Status(StatusType.Burn, 1.0, 3)),
                        Card(p + "_inferno", "火燒連營", 1, TargetRule.AllEnemies, 0, Shape.All, CardKeywords.None,
                            Detonate(StatusType.Burn, 2)),
                    };
                default:
                    return new List<CardDef>();
            }
        }

        /// <summary>依職業與稀有度組出初始套牌：高級牌取代普通攻擊，總張數固定為 <see cref="DeckSize"/>。</summary>
        public static List<CardDef> BuildDeck(string prefix, Role role, Rarity rarity)
        {
            var skills = RoleSkills(prefix, role);
            int unique = rarity == Rarity.UR ? 2 : rarity == Rarity.SR ? 1 : 0;
            unique = System.Math.Min(unique, skills.Count);

            var attack = Basic(prefix + "_attack", "普通攻擊", 1, TargetRule.Enemy, BasicAttackRange(role), Dmg(BasicAttackMultiplier(role)));
            var deck = new List<CardDef>(DeckSize);
            for (int i = 0; i < DeckSize - unique; i++) deck.Add(attack);
            for (int i = 0; i < unique; i++) deck.Add(skills[i]);
            return deck;
        }

        private static HeroDef Hero(string id, string name, string prefix, Role role, Rarity rarity,
            AttackType attackType, Stats stats) => new HeroDef
        {
            Id = id, Name = name, Role = role, Rarity = rarity, AttackType = attackType, Base = stats,
            Deck = BuildDeck(prefix, role, rarity),
        };

        /// <summary>教學關借牌：把套牌裡的普通攻擊換成指定的教學牌（只用在教學關，正式套牌仍照 <see cref="BuildDeck"/>）。</summary>
        private static HeroDef WithLoan(HeroDef hero, params CardDef[] loan)
        {
            foreach (var card in loan)
            {
                int i = hero.Deck.FindIndex(c => c.Id.EndsWith("_attack"));
                if (i >= 0) hero.Deck[i] = card;
            }
            return hero;
        }

        public static HeroDef ZhangFei() => Hero("zhangfei", "張飛", "zf", Role.Tank, Rarity.UR, AttackType.Melee,
            new Stats { Hp = 1200, Atk = 135, Def = 70, Dodge = 0, Move = 1, Crit = 5, CritDmg = 150 });

        public static HeroDef GuanYu() => Hero("guanyu", "關羽", "gy", Role.Warrior, Rarity.UR, AttackType.Melee,
            new Stats { Hp = 960, Atk = 195, Def = 45, Dodge = 5, Move = 2, Crit = 15, CritDmg = 180 });

        public static HeroDef LiuBei() => Hero("liubei", "劉備", "lb", Role.Healer, Rarity.UR, AttackType.Melee,
            new Stats { Hp = 800, Atk = 70, Int = 120, Def = 35, Dodge = 5, Move = 2, Crit = 5, CritDmg = 150 });

        public static HeroDef ZhugeLiang() => Hero("zhugeliang", "諸葛亮", "zgl", Role.Strategist, Rarity.UR, AttackType.Ranged,
            new Stats { Hp = 680, Atk = 60, Int = 165, Def = 25, Dodge = 10, Move = 1, Crit = 10, CritDmg = 150 });

        /// <summary>法師：Demo 的火攻教學（第 7 關）由他擔任，原本由諸葛亮兼任。</summary>
        public static HeroDef PangTong() => Hero("pangtong", "龐統", "pt", Role.Mage, Rarity.UR, AttackType.Ranged,
            new Stats { Hp = 680, Atk = 60, Int = 165, Def = 25, Dodge = 10, Move = 1, Crit = 10, CritDmg = 150 });

        public static HeroDef ZhaoYun() => Hero("zhaoyun", "趙雲", "zy", Role.Warrior, Rarity.UR, AttackType.Melee,
            new Stats { Hp = 840, Atk = 210, Def = 40, Dodge = 15, Move = 3, Crit = 20, CritDmg = 170 });

        public static HeroDef HuangZhong() => Hero("huangzhong", "黃忠", "hz", Role.Archer, Rarity.UR, AttackType.Ranged,
            new Stats { Hp = 720, Atk = 200, Def = 30, Dodge = 8, Move = 2, Crit = 20, CritDmg = 170 });

        // ---- 教學關借牌（昏亂 / 斷甲 不在初始套牌裡，教學關借給武將示範）----

        private static CardDef Howl() => Card("zf_howl", "虎吼", 1, TargetRule.Enemy, 1, Shape.Single, CardKeywords.Innate, StunGauge(40));

        private static CardDef Roar() => Card("zf_roar", "當陽橋喝斷", 3, TargetRule.Enemy, 1, Shape.Row,
            CardKeywords.Innate | CardKeywords.Retain, Dmg(1.4), StunGauge(60));

        private static CardDef GuanYuBreak() => Card("gy_break", "斷甲", 1, TargetRule.Enemy, 1, Shape.Single, CardKeywords.Innate,
            Dmg(0.8), Status(StatusType.ArmorBreak, 0.5, 4));

        // ---- R 級基礎小兵（只有普通攻擊）----

        public static HeroDef MilitiaSoldier() => Hero("r_militia", "義勇兵", "r_mil", Role.Warrior, Rarity.R, AttackType.Melee,
            new Stats { Hp = 600, Atk = 85, Def = 25, Dodge = 0, Move = 2, Crit = 5, CritDmg = 150 });

        public static HeroDef MilitiaShield() => Hero("r_shield", "鄉勇盾兵", "r_shd", Role.Tank, Rarity.R, AttackType.Melee,
            new Stats { Hp = 800, Atk = 60, Def = 50, Dodge = 0, Move = 1, Crit = 0, CritDmg = 150 });

        public static HeroDef MilitiaArcher() => Hero("r_archer", "鄉勇弓手", "r_arc", Role.Archer, Rarity.R, AttackType.Ranged,
            new Stats { Hp = 450, Atk = 90, Def = 15, Dodge = 5, Move = 2, Crit = 10, CritDmg = 150 });

        public static HeroDef MilitiaHealer() => Hero("r_healer", "鄉勇醫士", "r_hlr", Role.Healer, Rarity.R, AttackType.Melee,
            new Stats { Hp = 450, Atk = 35, Int = 70, Def = 15, Dodge = 5, Move = 2, Crit = 0, CritDmg = 150 });

        /// <summary>第 6 關的保護目標「馬商張世平」（演義中資助劉備的馬商）：沒有牌，只能被保護。</summary>
        public static HeroDef Villager() => new HeroDef
        {
            Id = "r_villager", Name = "馬商張世平", Role = Role.Tank, Rarity = Rarity.R, AttackType = AttackType.Melee,
            Base = new Stats { Hp = 800, Atk = 0, Def = 0, Move = 1, Crit = 0, CritDmg = 150 },
            Deck = new List<CardDef>(),
        };

        /// <summary>所有可上場武將（編隊用），依稀有度與登場順序排列。</summary>
        public static List<HeroDef> Roster() => new List<HeroDef>
        {
            ZhangFei(), GuanYu(), HuangZhong(), LiuBei(), ZhugeLiang(), ZhaoYun(), PangTong(),
            MilitiaSoldier(), MilitiaShield(), MilitiaArcher(), MilitiaHealer(),
        };

        // ---- 敵人（黃巾）----

        public static EnemyDef YellowTurbanSoldier() => new EnemyDef
        {
            Id = "yt_soldier", Name = "黃巾兵", AttackType = AttackType.Melee, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 300, Atk = 160, Def = 20, Move = 1, Crit = 0, CritDmg = 150 },
        };

        public static EnemyDef YellowTurbanArcher() => new EnemyDef
        {
            Id = "yt_archer", Name = "黃巾弓手", AttackType = AttackType.Ranged, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 200, Atk = 165, Def = 10, Move = 2, Crit = 5, CritDmg = 150 },
        };

        /// <summary>第 2 關用的精準弓手：攻擊高、血量中等，每回合都會重創後排。</summary>
        public static EnemyDef YellowTurbanSharpshooter() => new EnemyDef
        {
            
            Id = "yt_sharpshooter", Name = "黃巾神射手", AttackType = AttackType.Ranged, AttackMultiplier = 1.0,
            Base = new Stats { Hp = 450, Atk = 540, Def = 10, Move = 2, Crit = 5, CritDmg = 150 },
        };

        /// <summary>第 3 關用的鐵甲力士：防禦極高，不破甲幾乎打不動。</summary>
        public static EnemyDef YellowTurbanIronBrute() => new EnemyDef
        {
            Id = "yt_ironbrute", Name = "鐵甲力士", AttackType = AttackType.Melee, AttackMultiplier = 1.2,
            Base = new Stats { Hp = 900, Atk = 220, Def = 400, Move = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第 4 關的妖道：脆皮，每回合治療血量比例最低的友軍；放在後排，要靠弓手才打得到。</summary>
        public static EnemyDef YellowTurbanPriest() => new EnemyDef
        {
            Id = "yt_priest", Name = "黃巾妖道", AttackType = AttackType.Ranged, AttackMultiplier = 0.8,
            Ability = EnemyAbility.Healer, AbilityPower = 1.5,
            Base = new Stats { Hp = 300, Atk = 200, Int = 200, Def = 10, Move = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>精英「黃巾渠帥」：蓄力一回合、下回合放大招；被昏亂（昏亂條滿）會打斷蓄力。</summary>
        public static EnemyDef YellowTurbanChief() => new EnemyDef
        {
            Id = "yt_chief", Name = "黃巾渠帥", AttackType = AttackType.Melee, AttackMultiplier = 1.0,
            Ability = EnemyAbility.Charger, AbilityPower = 8.0, StunGauge = 100, StunGrowth = 0.5,
            Base = new Stats { Hp = 800, Atk = 220, Def = 40, Move = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第 8 關的符水術士：躲在後排，每回合召喚一名黃巾兵。</summary>
        public static EnemyDef YellowTurbanWarlock() => new EnemyDef
        {
            Id = "yt_warlock", Name = "符水術士", AttackType = AttackType.Ranged, AttackMultiplier = 0.8,
            Ability = EnemyAbility.Summoner, Summons = YellowTurbanSoldier(), SummonCap = 7,
            Base = new Stats { Hp = 250, Atk = 160, Def = 10, Move = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第 9 關的副將：和渠帥一樣蓄力大招，但昏亂條較短（60）。</summary>
        public static EnemyDef YellowTurbanLieutenant() => new EnemyDef
        {
            Id = "yt_lieutenant", Name = "黃巾副將", AttackType = AttackType.Melee, AttackMultiplier = 1.0,
            Ability = EnemyAbility.Charger, AbilityPower = 8.0, StunGauge = 60, StunGrowth = 0.5,
            Base = new Stats { Hp = 500, Atk = 220, Def = 40, Move = 1, Crit = 0, CritDmg = 150 },
        };

        /// <summary>第 10 關 BOSS「張角」：蓄力 → 大招 → 召喚 輪流，昏亂條很長。</summary>
        public static EnemyDef ZhangJiao() => new EnemyDef
        {
            Id = "yt_zhangjiao", Name = "張角", AttackType = AttackType.Ranged, AttackMultiplier = 1.0,
            Ability = EnemyAbility.Charger | EnemyAbility.Summoner, AbilityPower = 8.0,
            Summons = YellowTurbanSoldier(), SummonEvery = 3, SummonCap = 5,
            StunGauge = 100, StunGrowth = 0.5,
            Base = new Stats { Hp = 1800, Atk = 220, Def = 40, Move = 1, Crit = 0, CritDmg = 150 },
        };

        public static EnemyDef YellowTurbanBrute() => new EnemyDef
        {
            Id = "yt_brute", Name = "黃巾力士", AttackType = AttackType.Melee, AttackMultiplier = 1.3,
            Base = new Stats { Hp = 700, Atk = 200, Def = 50, Move = 1, Crit = 0, CritDmg = 150 },
        };

        // ---- 第零章敵人（盜匪 / 山賊）：數值與能力沿用黃巾版本，只換 id 與名稱；黃巾留給第一章 ----

        private static EnemyDef Reskin(EnemyDef def, string id, string name)
        {
            def.Id = id;
            def.Name = name;
            return def;
        }

        public static EnemyDef BanditGrunt() => Reskin(YellowTurbanSoldier(), "bandit_grunt", "山賊嘍囉");

        public static EnemyDef BanditArcher() => Reskin(YellowTurbanArcher(), "bandit_archer", "山賊弓手");

        /// <summary>第 2 關：專打後排的獵戶出身山賊。</summary>
        public static EnemyDef BanditMarksman() => Reskin(YellowTurbanSharpshooter(), "bandit_marksman", "獵戶山賊");

        /// <summary>第 3 關：身披搶來鐵甲的悍匪，防禦極高。</summary>
        public static EnemyDef BanditIronBrute() => Reskin(YellowTurbanIronBrute(), "bandit_ironbrute", "披甲悍匪");

        /// <summary>第 4 關：後排治療同夥的野巫醫。</summary>
        public static EnemyDef BanditShaman() => Reskin(YellowTurbanPriest(), "bandit_shaman", "土匪巫醫");

        /// <summary>第 5 關與資源副本：蓄力大招的二當家。</summary>
        public static EnemyDef BanditSecondChief() => Reskin(YellowTurbanChief(), "bandit_second", "二當家");

        public static EnemyDef BanditDeputy() => Reskin(YellowTurbanLieutenant(), "bandit_deputy", "副寨主");

        /// <summary>第 8 關：敲鼓召集小弟的鼓手。</summary>
        public static EnemyDef BanditDrummer()
        {
            var def = Reskin(YellowTurbanWarlock(), "bandit_drummer", "擊鼓匪");
            def.Summons = BanditGrunt();
            return def;
        }

        /// <summary>第 10 關 BOSS：占山為王的山大王（名字暫定）。</summary>
        public static EnemyDef BanditKing()
        {
            var def = Reskin(ZhangJiao(), "bandit_king", "鎮山虎");
            def.Summons = BanditGrunt();
            return def;
        }

        /// <summary>第零章關卡名稱（機制藍圖見 docs/demo-chapter1.md，劇情見 docs/chapter0.md）。</summary>
        public static readonly string[] LevelNames =
        {
            "涿縣村口", "山賊探子", "披甲悍匪", "野巫醫", "二當家",
            "護送馬商", "火燒山寨", "鼓聲召匪", "雙寨主", "鎮山虎",
        };

        /// <summary>已實作的關卡數（其餘在地圖上顯示為尚未開放）。</summary>
        public const int ChapterLevelCount = 10;

        /// <summary>教學關寫死的起手牌序（每次抽牌堆重建時，這幾張排最前面）。</summary>
        private static List<string> TutorialDraw(int level)
        {
            switch (level)
            {
                case 2: return new List<string> { "zf_taunt", "zf_stance", "r_mil_attack", "r_arc_attack", "zf_attack" };
                case 3: return new List<string> { "gy_break", "hz_pierce", "gy_attack", "hz_attack", "r_hlr_attack" };
                case 4: return new List<string> { "hz_snipe", "r_mil_attack", "r_shd_attack", "hz_attack", "r_hlr_attack" };
                case 5: return new List<string> { "zf_howl", "zf_roar", "r_hlr_attack", "r_arc_attack", "zf_attack" };
                case 6: return new List<string> { "lb_shield", "r_arc_attack", "lb_attack", "r_shd_attack", "lb_heal" };
                case 9: return new List<string> { "zf_howl", "hz_snipe", "zf_roar", "r_shd_attack", "r_hlr_attack" };
                case 10: return new List<string> { "zf_howl", "zf_roar", "zy_cleave", "hz_snipe", "lb_shield" };
                case 8: return new List<string> { "zy_cleave", "r_mil_attack", "zy_attack", "r_shd_attack", "r_hlr_attack" };
                case 7: return new List<string> { "pt_fire", "pt_inferno", "r_mil_attack", "r_shd_attack", "r_hlr_attack" };
                default: return new List<string>();
            }
        }

        /// <summary>第一章「黃巾之亂」關卡（教學關：固定隊伍、不開放自動戰鬥）。</summary>
        public static BattleSetup Level(int level, ulong seed = 1, bool tutorialScale = true)
        {
            // 第一章每一關都是教學關：不開放自動戰鬥。
            var setup = new BattleSetup { Seed = seed, AutoAllowed = false };
            if (level == 3) setup.TurnLimit = 11;
            if (level == 7) setup.TurnLimit = 3;
            if (level == 8) setup.TurnLimit = 4; // 援軍源源不絕：四回合內不斬首術士就守不住
            // 教學關：小兵湊數，只讓一兩名武將帶著該關要教的技能卡上場（編隊鎖定，之後再開放）。
            setup.FormationLocked = true;
            // 教學關：牌序寫死、沒有爆擊閃避，結果完全可重現（每次抽牌堆重建，教學卡都排在最前面）。
            setup.NoRandomness = true;
            switch (level)
            {
                case 1: // 劉備（治療）帶著鄉勇小兵：第一場純出牌與費用
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(LiuBei(), HeroPos(2, 1)));
                    break;
                case 2: // 張飛（嘲諷 + 防禦姿態）
                    setup.Heroes.Add(new HeroSlot(ZhangFei(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
                case 4: // 黃忠（狙擊：打血量最低的敵人）專打後排的妖道
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
                case 5: // 張飛借牌（虎吼 / 當陽橋喝斷・先登）：用昏亂條打斷渠帥的蓄力大招
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(WithLoan(ZhangFei(), Howl(), Roar()), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
                case 6: // 劉備（上盾）：用護甲保護後排的鄉民（鄉民受傷（60% 血），所以是「血量比例最低的隊友」）
                {
                    setup.Heroes.Add(new HeroSlot(Villager(), HeroPos(0, 1)) { IsProtected = true, StartHpPercent = 60 });
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(LiuBei(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaArcher(), HeroPos(2, 0)));
                    break;
                }
                case 7: // 龐統（火計 / 火燒連營）：先放火再引爆，火勢蔓延整排敵人
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(PangTong(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
                case 8: // 趙雲（豎劈斬）：縱列穿透，一槍連前排帶後排的術士
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(ZhaoYun(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaSoldier(), HeroPos(2, 0)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(1, 1)));
                    break;
                case 9: // 綜合：張飛借牌（昏亂）＋ 黃忠（後排）＋小兵；雙渠帥蓄力、妖道治療
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(WithLoan(ZhangFei(), Howl(), Roar()), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
                case 10: // BOSS：四名武將齊上（張飛借昏亂牌）
                    setup.Heroes.Add(new HeroSlot(WithLoan(ZhangFei(), Howl(), Roar()), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(ZhaoYun(), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(LiuBei(), HeroPos(2, 1)));
                    break;
                default: // 關羽借牌（斷甲・先登）＋黃忠（破甲箭）
                    setup.Heroes.Add(new HeroSlot(MilitiaShield(), HeroPos(0, 0)));
                    setup.Heroes.Add(new HeroSlot(WithLoan(GuanYu(), GuanYuBreak()), HeroPos(1, 0)));
                    setup.Heroes.Add(new HeroSlot(HuangZhong(), HeroPos(1, 1)));
                    setup.Heroes.Add(new HeroSlot(MilitiaHealer(), HeroPos(2, 1)));
                    break;
            }
            setup.ScriptedDraw = TutorialDraw(level);
            switch (level)
            {
                case 1: // 涿縣村口：出牌與費用（教學）
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(2, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(3, 0)));
                    break;
                case 2: // 山賊探子：兩名神射手專打後排；要靠張飛的挑釁把火力拉到前排
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditMarksman(), EnemyPos(2, 1)));
                    setup.Enemies.Add(new EnemySlot(BanditMarksman(), EnemyPos(3, 1)));
                    break;
                case 3: // 披甲悍匪：鐵甲力士擋在最上路，同路沒人時全隊火力都落在牠身上；要疊破甲才打得動
                    setup.Enemies.Add(new EnemySlot(BanditIronBrute(), EnemyPos(0, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(0, 1)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(3, 0)));
                    break;
                case 4: // 野巫醫：妖道躲在後排持續治療，只有弓手打得到
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(0, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(2, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditShaman(), EnemyPos(2, 1)));
                    break;
                case 5: // 二當家：蓄力 → 大招；昏亂條滿才能打斷
                    setup.Enemies.Add(new EnemySlot(BanditSecondChief(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(0, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(2, 0)));
                    break;
                case 6: // 護送馬商：兩名弓手專打後排的鄉民，鄉民陣亡即失敗
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(4, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditArcher(), EnemyPos(1, 1)));
                    setup.Enemies.Add(new EnemySlot(BanditArcher(), EnemyPos(3, 1)));
                    break;
                case 7: // 火燒山寨：五名山賊嘍囉擠成一排，單打太慢，要靠火勢蔓延
                    for (int lane = 0; lane < 5; lane++)
                        setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(lane, 0)));
                    break;
                case 8: // 鼓聲召匪：術士每回合召喚山賊嘍囉，不處理就會被淹沒
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditDrummer(), EnemyPos(1, 1)));
                    break;
                case 9: // 雙寨主：兩名蓄力的將領，加一名後排治療的妖道
                    setup.Enemies.Add(new EnemySlot(BanditSecondChief(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditDeputy(), EnemyPos(3, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditShaman(), EnemyPos(2, 1)));
                    break;
                case 10: // 鎮山虎：山大王蓄力 → 大招 → 召喚，兩側各一名山賊嘍囉
                    setup.Enemies.Add(new EnemySlot(BanditKing(), EnemyPos(1, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(0, 0)));
                    setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(3, 0)));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            var (hpPct, atkPct) = tutorialScale ? TutorialEnemyScale(level) : (100, 100);
            void Scale(EnemyDef def)
            {
                def.Base.Hp = def.Base.Hp * hpPct / 100;
                def.Base.Atk = def.Base.Atk * atkPct / 100;
                if (def.Summons != null) Scale(def.Summons);
            }
            foreach (var e in setup.Enemies) Scale(e.Def);
            return setup;
        }

        /// <summary>
        /// 各關敵人強度（血量 %, 攻擊 %）：共用 5x5 棋盤後，我方近戰要走位、手牌不再重洗，輸出比舊版低，
        /// 這組數值依自動對戰掃描（照教學打必勝、忽略教學必敗）調整，仍為佔位。
        /// </summary>
        public static (int HpPct, int AtkPct) TutorialEnemyScale(int level)
        {
            switch (level)
            {
                case 1: return (70, 70);
                case 2: return (40, 70);
                case 3: return (40, 70);
                case 4: return (50, 70);
                case 5: return (60, 80);
                case 6: return (90, 63);
                case 7: return (60, 70);
                case 8: return (40, 80);
                case 9: return (50, 80);
                case 10: return (30, 40);
                default: return (100, 100);
            }
        }

        /// <summary>範例關卡：劉關張 + 諸葛亮 對 黃巾兵 / 弓手 / 力士。</summary>
        public static BattleSetup SampleBattle(ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed };
            setup.Heroes.Add(new HeroSlot(ZhangFei(), HeroPos(1, 0)));
            setup.Heroes.Add(new HeroSlot(GuanYu(), HeroPos(2, 0)));
            setup.Heroes.Add(new HeroSlot(LiuBei(), HeroPos(3, 1)));
            setup.Heroes.Add(new HeroSlot(ZhugeLiang(), HeroPos(2, 1)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), EnemyPos(1, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanBrute(), EnemyPos(2, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanSoldier(), EnemyPos(3, 0)));
            setup.Enemies.Add(new EnemySlot(YellowTurbanArcher(), EnemyPos(2, 1)));
            return setup;
        }
    }
}
