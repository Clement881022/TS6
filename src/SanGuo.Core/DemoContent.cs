using System.Collections.Generic;

namespace SanGuo.Core
{
    /// <summary>
    /// 第零章（涿縣盜匪教學 10 關）的敵人與關卡配置。武將與卡牌見 <see cref="HeroRoster"/>、<see cref="CardLibrary"/>。
    /// 敵人數值為同職業 1 級基準，實際強度由關卡指定的敵人等級縮放；數值為佔位，依自動對戰驗證調整。
    /// </summary>
    public static class DemoContent
    {
        // ---- 站位：舊關卡資料以「路 / 排」表示，這裡映射到共用 5x5 棋盤 ----

        /// <summary>我方站位：(路 0–2, 排 0 前 / 1 後) → 入場區（欄 1–3、列 3 前 / 4 後）。</summary>
        public static Position HeroPos(int lane, int row) => new Position(lane + BattleSetup.FormationMinLane, BattleSetup.FormationMinRow + row);

        /// <summary>敵方站位：(路 0–4, 排 0 前 / 1 後) → 欄 0–4、列 1 前 / 0 後。</summary>
        public static Position EnemyPos(int lane, int row) => new Position(lane, 1 - row);

        /// <summary>所有可抽取 / 可上場的武將（編隊用），依稀有度與名單順序排列。</summary>
        public static List<HeroDef> Roster() => HeroRoster.All();

        // ---- 第零章敵人（盜匪 / 山賊）----

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

        /// <summary>第 2 關：專打後排的獵戶出身山賊，攻擊高、射程遠。</summary>
        public static EnemyDef BanditMarksman() => new EnemyDef
        {
            Id = "bandit_marksman", Name = "獵戶山賊", Role = Role.Ranger, AttackType = AttackType.Ranged,
            Base = new Stats { Hp = 350, Atk = 400, Def = 10, Move = 2, Crit = 5, Range = 3 },
        };

        /// <summary>第 3 關：身披搶來鐵甲的悍匪，防禦極高，不破甲、不用法術幾乎打不動。</summary>
        public static EnemyDef BanditIronBrute() => new EnemyDef
        {
            Id = "bandit_ironbrute", Name = "披甲悍匪", Role = Role.Tank, AttackType = AttackType.Melee, AttackMultiplier = 1.2,
            Base = new Stats { Hp = 500, Atk = 100, Def = 200, Move = 1, Crit = 0, Range = 1 },
        };

        /// <summary>第 4 關：躲在後排的土匪巫師，法術傷害高但極脆，要優先擊殺。</summary>
        public static EnemyDef BanditShaman() => new EnemyDef
        {
            Id = "bandit_shaman", Name = "土匪巫師", Role = Role.Mage, AttackType = AttackType.Ranged, Magical = true,
            Base = new Stats { Hp = 250, Atk = 40, Int = 190, Def = 0, Move = 1, Crit = 0, Range = 2 },
        };

        /// <summary>第 5 關與資源副本：蓄力大招的二當家（精英）。</summary>
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

        /// <summary>第 10 關 BOSS：占山為王的山大王（名字暫定）。蓄力兩回合的全體大招。</summary>
        public static EnemyDef BanditKing() => new EnemyDef
        {
            Id = "bandit_king", Name = "鎮山虎", Role = Role.Warrior, Tier = EnemyTier.Boss, AttackType = AttackType.Melee,
            ChargeTurns = 2, ChargeInterval = 2, ChargePower = 1.2,
            Base = new Stats { Hp = 420, Atk = 120, Def = 40, Move = 1, Crit = 0, Range = 1 },
        };

        /// <summary>第零章關卡名稱（劇情見 docs/chapter0.md）。</summary>
        public static readonly string[] LevelNames =
        {
            "涿縣村口", "山賊探子", "披甲悍匪", "野巫師", "二當家",
            "護送馬商", "火燒山寨", "橫掃千軍", "雙寨主", "鎮山虎",
        };

        /// <summary>已實作的關卡數（其餘在地圖上顯示為尚未開放）。</summary>
        public const int ChapterLevelCount = 10;

        /// <summary>第零章各關的我方等級：第 1 天結束（第零章完成）時玩家約 12 級。</summary>
        public static int HeroLevelOf(int level) => 2 + level;

        /// <summary>第零章各關的敵人等級（略低於我方等級，教學關重在體驗機制）。</summary>
        public static int EnemyLevelOf(int level) => level;

        /// <summary>教學關寫死的起手牌序（每次抽牌堆重建時，這幾張排最前面）。</summary>
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

        /// <summary>第零章關卡（教學關：固定隊伍、不開放自動戰鬥）。</summary>
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
            // 教學關：隊伍固定、牌序寫死、沒有爆擊閃避，結果完全可重現。
            setup.FormationLocked = true;
            setup.NoRandomness = true;
            int heroLv = HeroLevelOf(level), enemyLv = EnemyLevelOf(level);
            HeroSlot Hero(HeroDef def, int lane, int row) => new HeroSlot(def, HeroPos(lane, row), heroLv);
            EnemySlot Enemy(EnemyDef def, int lane, int row) => new EnemySlot(def, EnemyPos(lane, row), enemyLv);

            switch (level)
            {
                case 1: // 劉備（治療）帶著義勇兵：第一場純出牌與費用
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.LiuBei(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 3, 0));
                    break;
                case 2: // 山賊探子：神射手專打後排；要靠張飛的嘲諷把火力拉到前排
                    setup.Heroes.Add(Hero(HeroRoster.ZhangFei(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditMarksman(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditMarksman(), 3, 1));
                    break;
                case 3: // 披甲悍匪：防禦極高，要靠弓兵的破甲箭才打得動
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.GuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 3, 1));
                    break;
                case 4: // 野巫師：巫師躲在後排放法術，優先擊殺牠
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaMage(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 0, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditShaman(), 2, 1));
                    break;
                case 5: // 二當家：蓄力 → 全體大招；嘲諷可打斷蓄力
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.ZhangFei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditSecondChief(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 0, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    break;
                case 6: // 護送馬商：弓手專打後排的馬商，用屏障保護他撐過數回合
                    setup.Heroes.Add(new HeroSlot(HeroRoster.Villager(), HeroPos(0, 1), heroLv) { IsProtected = true, StartHpPercent = 60 });
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.LiuBei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 2, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 4, 0));
                    setup.Enemies.Add(Enemy(BanditArcher(), 1, 1));
                    setup.Enemies.Add(Enemy(BanditArcher(), 3, 1));
                    break;
                case 7: // 火燒山寨：悍匪不怕刀劍，術士的法術無視防禦，燃燒持續燒
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaMage(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditIronBrute(), 3, 0));
                    setup.Enemies.Add(Enemy(BanditGrunt(), 2, 0));
                    break;
                case 8: // 橫掃千軍：五名山賊擠成一排，關羽的橫斬一刀三人
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.GuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaSword(), 2, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 1, 1));
                    for (int lane = 0; lane < 5; lane++) setup.Enemies.Add(Enemy(BanditGrunt(), lane, 0));
                    break;
                case 9: // 綜合：兩名蓄力的將領
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaShield(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.ZhangFei(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaHealer(), 2, 1));
                    setup.Enemies.Add(Enemy(BanditSecondChief(), 1, 0));
                    setup.Enemies.Add(Enemy(BanditDeputy(), 3, 0));
                    setup.Enemies.Add(Enemy(BanditShaman(), 2, 1));
                    break;
                case 10: // BOSS：山大王蓄力兩回合放全體大招，兩側各一名嘍囉
                    setup.Heroes.Add(Hero(HeroRoster.ZhangFei(), 0, 0));
                    setup.Heroes.Add(Hero(HeroRoster.GuanYu(), 1, 0));
                    setup.Heroes.Add(Hero(HeroRoster.MilitiaArcher(), 1, 1));
                    setup.Heroes.Add(Hero(HeroRoster.LiuBei(), 2, 1));
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

        /// <summary>範例關卡：劉關張 + 義勇謀士 對 山賊。</summary>
        public static BattleSetup SampleBattle(ulong seed = 1)
        {
            var setup = new BattleSetup { Seed = seed };
            setup.Heroes.Add(new HeroSlot(HeroRoster.ZhangFei(), HeroPos(1, 0), 10));
            setup.Heroes.Add(new HeroSlot(HeroRoster.GuanYu(), HeroPos(2, 0), 10));
            setup.Heroes.Add(new HeroSlot(HeroRoster.LiuBei(), HeroPos(0, 1), 10));
            setup.Heroes.Add(new HeroSlot(HeroRoster.MilitiaStrategist(), HeroPos(2, 1), 10));
            setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(1, 0), 8));
            setup.Enemies.Add(new EnemySlot(BanditIronBrute(), EnemyPos(2, 0), 8));
            setup.Enemies.Add(new EnemySlot(BanditGrunt(), EnemyPos(3, 0), 8));
            setup.Enemies.Add(new EnemySlot(BanditArcher(), EnemyPos(2, 1), 8));
            return setup;
        }
    }
}
