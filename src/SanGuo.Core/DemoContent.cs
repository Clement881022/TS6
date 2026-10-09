using System.Collections.Generic;

namespace SanGuo.Core
{
    public static class DemoContent
    {
        public static Position HeroPos(int lane, int row) => new Position(lane + BattleSetup.FormationMinLane, BattleSetup.FormationMinRow + row);

        public static Position EnemyPos(int lane, int row) => new Position(lane, 1 - row);

        public static List<HeroDef> Roster() => HeroRoster.All();

        public static EnemyDef BanditGrunt() => new EnemyDef
        {
            Id = "bandit_grunt", Name = "山賊嘍囉", Role = Role.Warrior, AttackType = AttackType.Melee,
            Base = new Stats { Hp = 450, Atk = 100, Def = 30, Move = 1, Crit = 0, Range = 1 },
        };

        public static EnemyDef BanditArcher() => new EnemyDef
        {
            Id = "bandit_archer", Name = "山賊弓手", Role = Role.Ranger, AttackType = AttackType.Ranged,
            Base = new Stats { Hp = 350, Atk = 100, Def = 20, Move = 2, Crit = 5, Range = 2 },
        };

        public static EnemyDef BanditMarksman() => new EnemyDef
        {
            Id = "bandit_marksman", Name = "獵戶山賊", Role = Role.Ranger, AttackType = AttackType.Ranged,
            Base = new Stats { Hp = 350, Atk = 400, Def = 10, Move = 2, Crit = 5, Range = 3 },
        };

        public static EnemyDef BanditIronBrute() => new EnemyDef
        {
            Id = "bandit_ironbrute", Name = "披甲悍匪", Role = Role.Tank, AttackType = AttackType.Melee, AttackMultiplier = 1.2,
            Base = new Stats { Hp = 500, Atk = 100, Def = 200, Move = 1, Crit = 0, Range = 1 },
        };

        public static EnemyDef BanditShaman() => new EnemyDef
        {
            Id = "bandit_shaman", Name = "土匪巫師", Role = Role.Mage, AttackType = AttackType.Ranged, Magical = true,
            Base = new Stats { Hp = 250, Atk = 40, Int = 190, Def = 0, Move = 1, Crit = 0, Range = 2 },
        };

        public static EnemyDef BanditSecondChief() => new EnemyDef
        {
            Id = "bandit_second", Name = "二當家", Role = Role.Warrior, Tier = EnemyTier.Elite, AttackType = AttackType.Melee,
            ChargeTurns = 1, ChargeInterval = 1, ChargePower = 2.5,
            Base = new Stats { Hp = 600, Atk = 120, Def = 40, Move = 1, Crit = 0, Range = 1 },
        };

        public static EnemyDef BanditDeputy() => new EnemyDef
        {
            Id = "bandit_deputy", Name = "副寨主", Role = Role.Warrior, Tier = EnemyTier.Elite, AttackType = AttackType.Melee,
            ChargeTurns = 1, ChargeInterval = 2, ChargePower = 1.3,
            Base = new Stats { Hp = 500, Atk = 120, Def = 40, Move = 1, Crit = 0, Range = 1 },
        };

        public static EnemyDef BanditKing() => new EnemyDef
        {
            Id = "bandit_king", Name = "鎮山虎", Role = Role.Warrior, Tier = EnemyTier.Boss, AttackType = AttackType.Melee,
            ChargeTurns = 2, ChargeInterval = 2, ChargePower = 1.2,
            Base = new Stats { Hp = 420, Atk = 120, Def = 40, Move = 1, Crit = 0, Range = 1 },
        };

        public static readonly string[] LevelNames =
        {
            "涿縣村口", "山賊探子", "披甲悍匪", "野巫師", "二當家",
            "護送馬商", "火燒山寨", "橫掃千軍", "雙寨主", "鎮山虎",
        };

        public const int ChapterLevelCount = 10;

        public static readonly int[] TurnPar = { 8, 10, 8, 12, 11, 8, 10, 12, 14, 20 };

        public static int HeroLevelOf(int level) => 2 + level;

        public static int EnemyLevelOf(int level) => level;

        private static List<string> TutorialDraw(int level)
        {
            switch (level)
            {
                case 2: return new List<string> { "zf_taunt", "r_swd_attack", "r_arc_attack", "zf_attack", "r_hlr_attack" };
                case 3: return new List<string> { "r_arc_pierce", "gy_attack", "gy_heavy", "r_arc_attack", "r_shd_attack" };
                case 4: return new List<string> { "r_mag_attack", "r_arc_attack", "r_swd_attack", "r_shd_attack", "r_mag_attack" };
                case 5: return new List<string> { "zf_taunt", "zf_attack", "r_arc_attack", "r_hlr_attack", "r_shd_attack" };
                case 6: return new List<string> { "lb_barrier", "r_arc_attack", "lb_attack", "r_shd_attack", "lb_heal" };
                case 7: return new List<string> { "r_mag_fire", "r_mag_attack", "r_shd_attack", "r_swd_attack", "r_hlr_attack" };
                case 8: return new List<string> { "gy_sweep", "r_swd_attack", "gy_attack", "r_shd_attack", "r_hlr_attack" };
                case 10: return new List<string> { "zf_taunt", "gy_sweep", "r_arc_pierce", "lb_barrier", "lb_heal" };
                default: return new List<string>();
            }
        }

        public static BattleSetup Level(int level, ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed, AutoAllowed = false };
            if (level == 3) setup.TurnLimit = 10;
            if (level == 7) setup.TurnLimit = 8;
            if (level == 6)
            {
                setup.Objective = Objective.Escort;
                setup.SurviveTurns = 6;
            }
            setup.FormationLocked = true;
            setup.NoRandomness = true;
            int heroLv = HeroLevelOf(level), enemyLv = EnemyLevelOf(level);
            HeroSlot Hero(HeroDef def, int lane, int row) => new HeroSlot(def, HeroPos(lane, row), heroLv);
            EnemySlot Enemy(EnemyDef def, int lane, int row) => new EnemySlot(def, EnemyPos(lane, row), enemyLv);

            switch (level)
            {
                case 1:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialLiuBei(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 3, 0));
                    break;
                case 2:
                    setup.Heroes.Add(Hero(HeroRoster.TutorialZhangFei(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditMarksman(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditMarksman(), 3, 1));
                    break;
                case 3:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialGuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 3, 1));
                    break;
                case 4:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaMage(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 0, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditShaman(), 2, 1));
                    break;
                case 5:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialZhangFei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditSecondChief(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 0, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    break;
                case 6:
                    setup.Heroes.Add(new HeroSlot(HeroRoster.Villager(), HeroPos(0, 1), heroLv) { IsProtected = true, StartHpPercent = 60 });
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialLiuBei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 4, 0));
                    setup.Enemies.Add(Enemy(BanditArcher(), 1, 1));
                    setup.Enemies.Add(Enemy(BanditArcher(), 3, 1));
                    break;
                case 7:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaMage(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 3, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    break;
                case 8:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialGuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 2, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 1, 1));
                    for (int lane = 0; lane < 5; lane++) setup.Enemies.Add(Enemy(BanditGrunt(), lane, 0));
                    break;
                case 9:
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialZhangFei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditSecondChief(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditDeputy(), 3, 0));
                    setup.Enemies.Add(Enemy(BanditShaman(), 2, 1));
                    break;
                case 10:
                    setup.Heroes.Add(Hero(HeroRoster.TutorialZhangFei(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialGuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.TutorialLiuBei(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditKing(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 0, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 3, 0));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            setup.ScriptedDraw = TutorialDraw(level);
            return setup;
        }
    }
}
