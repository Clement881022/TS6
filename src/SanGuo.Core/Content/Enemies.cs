namespace SanGuo.Core.Content
{
    public static class Enemies
    {
        private static EnemyDef Make(string id, string name, Role role, Stats stats, EnemyTier tier = EnemyTier.Normal,
            bool magical = false, string art = "") => new EnemyDef
        {
            Id = id, Name = name, Role = role, Tier = tier, Magical = magical, Art = art,
            AttackType = stats.Range > 1 ? AttackType.Ranged : AttackType.Melee,
            Base = stats,
        };

        private static EnemyDef Charge(this EnemyDef def, int turns, int interval, double power)
        {
            def.ChargeTurns = turns;
            def.ChargeInterval = interval;
            def.ChargePower = power;
            return def;
        }

        private static EnemyDef Phase(this EnemyDef def, int hpPercent, int interval, double power, int turns = 0)
        {
            def.PhaseHpPercent = hpPercent;
            def.Phase2ChargeInterval = interval;
            def.Phase2ChargePower = power;
            def.Phase2ChargeTurns = turns;
            return def;
        }

        public static EnemyDef YtSoldier() => Make("yt_soldier", "黃巾兵", Role.Warrior,
            new Stats { Hp = 460, Atk = 100, Def = 30, Move = 1, Crit = 5, Range = 1 });

        public static EnemyDef YtBrute() => Make("yt_brute", "黃巾力士", Role.Tank,
            new Stats { Hp = 620, Atk = 85, Def = 80, Move = 1, Range = 1 });

        public static EnemyDef YtArcher() => Make("yt_archer", "黃巾弓手", Role.Ranger,
            new Stats { Hp = 340, Atk = 100, Def = 20, Move = 1, Crit = 5, Range = 2 });

        public static EnemyDef YtSorcerer() => Make("yt_warlock", "黃巾方士", Role.Mage,
            new Stats { Hp = 280, Atk = 40, Int = 150, Def = 0, Move = 1, Range = 2 }, magical: true);

        public static EnemyDef YtCaptain() => Make("yt_lieutenant", "黃巾渠帥", Role.Warrior,
            new Stats { Hp = 520, Atk = 110, Def = 40, Move = 1, Crit = 5, Range = 1 }, EnemyTier.Elite).Charge(1, 2, 1.6);

        public static EnemyDef YtPriest() => Make("yt_priest", "太平道祭酒", Role.Mage,
            new Stats { Hp = 360, Atk = 40, Int = 140, Def = 10, Move = 1, Range = 2 }, EnemyTier.Elite, magical: true).Charge(1, 2, 1.3);

        public static EnemyDef ChengYuanzhi() => Make("cheng_yuanzhi", "程遠志", Role.Warrior,
            new Stats { Hp = 560, Atk = 115, Def = 45, Move = 1, Crit = 10, Range = 1 }, EnemyTier.Elite, art: "yt_chief").Charge(1, 2, 1.8);

        public static EnemyDef BoCai() => Make("bo_cai", "波才", Role.Warrior,
            new Stats { Hp = 480, Atk = 115, Def = 50, Move = 1, Crit = 10, Range = 1 }, EnemyTier.Boss, art: "yt_chief")
            .Charge(2, 2, 1.3).Phase(50, 1, 1.5);

        public static EnemyDef ZhangBao() => Make("zhang_bao_yt", "張寶", Role.Mage,
            new Stats { Hp = 380, Atk = 40, Int = 140, Def = 15, Move = 1, Range = 2 }, EnemyTier.Boss, magical: true, art: "yt_priest")
            .Charge(1, 2, 1.1).Phase(50, 1, 1.2);

        public static EnemyDef ZhangLiang() => Make("zhang_liang", "張梁", Role.Tank,
            new Stats { Hp = 700, Atk = 95, Def = 90, Move = 1, Range = 1 }, EnemyTier.Elite, art: "yt_ironbrute").Charge(1, 2, 1.6);

        public static EnemyDef ZhangJiao() => Make("yt_zhangjiao", "張角", Role.Mage,
            new Stats { Hp = 380, Atk = 40, Int = 110, Def = 20, Move = 1, Range = 2 }, EnemyTier.Boss, magical: true)
            .Charge(1, 2, 1.0).Phase(50, 1, 1.1);

        public static EnemyDef HanSoldier() => Make("han_soldier", "官兵", Role.Warrior,
            new Stats { Hp = 470, Atk = 100, Def = 35, Move = 1, Crit = 5, Range = 1 }, art: "bandit_grunt");

        public static EnemyDef HanArcher() => Make("han_archer", "官兵弓手", Role.Ranger,
            new Stats { Hp = 340, Atk = 100, Def = 20, Move = 1, Crit = 5, Range = 2 }, art: "bandit_archer");

        public static EnemyDef Guard() => Make("han_guard", "禁軍甲士", Role.Tank,
            new Stats { Hp = 680, Atk = 85, Def = 85, Move = 1, Range = 1 }, art: "bandit_ironbrute");

        public static EnemyDef Eunuch() => Make("eunuch_agent", "宦官黨羽", Role.Strategist,
            new Stats { Hp = 300, Atk = 40, Int = 145, Def = 10, Move = 1, Range = 2 }, magical: true, art: "bandit_shaman");

        public static EnemyDef Cavalry() => Make("xl_cavalry", "西涼鐵騎", Role.Warrior,
            new Stats { Hp = 500, Atk = 115, Def = 40, Move = 2, Crit = 10, Range = 1 }, art: "bandit_grunt");

        public static EnemyDef HorseArcher() => Make("xl_horsearcher", "西涼弓騎", Role.Ranger,
            new Stats { Hp = 360, Atk = 105, Def = 20, Move = 2, Crit = 10, Range = 2 }, art: "bandit_archer");

        public static EnemyDef XlCaptain() => Make("xl_captain", "西涼校尉", Role.Warrior,
            new Stats { Hp = 560, Atk = 120, Def = 50, Move = 1, Crit = 10, Range = 1 }, EnemyTier.Elite, art: "bandit_second").Charge(1, 2, 1.6);

        public static EnemyDef XlAdvisor() => Make("xl_advisor", "董卓幕僚", Role.Strategist,
            new Stats { Hp = 380, Atk = 40, Int = 150, Def = 15, Move = 1, Range = 2 }, EnemyTier.Elite, magical: true, art: "bandit_shaman").Charge(1, 2, 1.3);

        private static EnemyDef Renamed(this EnemyDef def, string id, string name)
        {
            def.Id = id;
            def.Name = name;
            return def;
        }

        public static EnemyDef BzCavalry() => Cavalry().Renamed("bz_cavalry", "并州狼騎");
        public static EnemyDef BzHorseArcher() => HorseArcher().Renamed("bz_horsearcher", "并州弓騎");
        public static EnemyDef BzCaptain() => XlCaptain().Renamed("bz_captain", "并州校尉");
        public static EnemyDef XianzhenGuard() => Guard().Renamed("xianzhen_guard", "陷陣營甲士");

        public static EnemyDef DuanGui() => Make("duan_gui", "段珪", Role.Strategist,
            new Stats { Hp = 380, Atk = 40, Int = 120, Def = 15, Move = 1, Range = 2 }, EnemyTier.Elite, magical: true, art: "bandit_shaman").Charge(1, 2, 1.4);

        public static EnemyDef ZhangRang() => Make("zhang_rang", "張讓", Role.Strategist,
            new Stats { Hp = 400, Atk = 40, Int = 105, Def = 15, Move = 1, Range = 2 }, EnemyTier.Boss, magical: true, art: "bandit_shaman")
            .Charge(1, 2, 1.1).Phase(50, 1, 1.2);

        public static EnemyDef LiRu() => Make("li_ru", "李儒", Role.Strategist,
            new Stats { Hp = 380, Atk = 40, Int = 100, Def = 20, Move = 1, Range = 2 }, EnemyTier.Boss, magical: true, art: "bandit_shaman")
            .Charge(1, 2, 1.0).Phase(50, 1, 1.1);

        public static EnemyDef HuZhen() => Make("hu_zhen", "胡軫", Role.Warrior,
            new Stats { Hp = 560, Atk = 120, Def = 50, Move = 1, Crit = 10, Range = 1 }, EnemyTier.Elite, art: "bandit_deputy").Charge(1, 2, 1.8);

        public static EnemyDef HuaXiong() => Make("hua_xiong_boss", "華雄", Role.Warrior,
            new Stats { Hp = 500, Atk = 120, Def = 55, Move = 1, Crit = 15, Range = 1 }, EnemyTier.Boss, art: "huaxiong")
            .Charge(2, 2, 1.3).Phase(50, 1, 1.5);

        public static EnemyDef GaoShun() => Make("gao_shun", "高順", Role.Tank,
            new Stats { Hp = 720, Atk = 95, Def = 100, Move = 1, Range = 1 }, EnemyTier.Elite, art: "bandit_ironbrute").Charge(1, 2, 1.6);

        public static EnemyDef LvBu() => Make("lv_bu_boss", "呂布", Role.Warrior,
            new Stats { Hp = 520, Atk = 125, Def = 55, Move = 2, Crit = 20, Range = 1 }, EnemyTier.Boss, art: "lvbu")
            .Charge(2, 2, 1.4).Phase(50, 1, 1.6);

        public static EnemyDef XuRong() => Make("xu_rong", "徐榮", Role.Ranger,
            new Stats { Hp = 420, Atk = 115, Def = 30, Move = 1, Crit = 10, Range = 2 }, EnemyTier.Boss, art: "bandit_marksman")
            .Charge(1, 2, 1.1).Phase(50, 1, 1.3);

        public static EnemyDef LiJue() => Make("li_jue", "李傕", Role.Warrior,
            new Stats { Hp = 560, Atk = 120, Def = 50, Move = 1, Crit = 10, Range = 1 }, EnemyTier.Elite, art: "bandit_second").Charge(1, 2, 1.6);

        public static EnemyDef GuoSi() => Make("guo_si", "郭汜", Role.Ranger,
            new Stats { Hp = 420, Atk = 115, Def = 25, Move = 1, Crit = 10, Range = 2 }, EnemyTier.Elite, art: "bandit_marksman").Charge(1, 2, 1.4);

        public static EnemyDef DongZhuo() => Make("dong_zhuo", "董卓", Role.Tank,
            new Stats { Hp = 620, Atk = 110, Def = 80, Move = 1, Range = 1 }, EnemyTier.Boss, art: "bandit_king")
            .Charge(2, 2, 1.5).Phase(50, 1, 1.6);
    }
}
