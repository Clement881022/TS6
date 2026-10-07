using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>Demo 的養成面內容（卡池、關卡獎勵；數值為建議值，之後改由 JSON 載入）。</summary>
    public static class DemoMeta
    {
        public const string StandardPoolId = "standard";
        public const string NewbiePoolId = "newbie";

        /// <summary>Demo 目前只有 UR 與 R（尚無 SR），所以 SR 機率為 0、十連不保底 SR。</summary>
        public static List<GachaPool> Pools()
        {
            var roster = DemoContent.Roster();
            var ur = roster.Where(h => h.Rarity == Rarity.UR).Select(h => h.Id).ToList();
            var r = roster.Where(h => h.Rarity == Rarity.R && h.Id != "r_villager").Select(h => h.Id).ToList();
            return new List<GachaPool>
            {
                new GachaPool
                {
                    Id = StandardPoolId, Name = "常駐招募", UrRateBp = 300, SrRateBp = 0,
                    UrHeroes = ur, RHeroes = r, TenPullGuaranteesSr = false,
                },
                new GachaPool
                {
                    Id = NewbiePoolId, Name = "新手招募", UrRateBp = 300, SrRateBp = 0,
                    UrHeroes = ur, RHeroes = r, TenPullGuaranteesSr = false, FirstTenGuaranteesUr = true,
                },
            };
        }

        public static string StageId(int chapter, int level) => $"{chapter}-{level}";

        /// <summary>第一章關卡獎勵：教學關（1–4）2 點體力、其餘 8 點（見 days-1-7.md 6）。</summary>
        public static StageReward Chapter1Stage(int level) => new StageReward
        {
            StageId = StageId(1, level),
            Chapter = 1,
            StaminaCost = level <= 4 ? 2 : 8,
            Exp = 20 + 10 * level,
            Gold = 200 + 100 * level,
            FirstClearYuanbao = level == DemoContent.ChapterLevelCount ? 300 : 60,
            StarTurnPar = 12,
        };

        public static ResourceDungeonDef? FindDungeon(string id) =>
            DemoResourceDungeons.Create().Find(d => d.Id == id);

        /// <summary>第一章前 8 關是教學關（固定隊伍）；從這一關起改用玩家的編隊與養成。</summary>
        public const int FirstOpenFormationLevel = 9;

        /// <summary>主線關卡編號（"1-3" → 3）；不是主線關卡回傳 0。</summary>
        public static int LevelOf(string stageId)
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return level;
            return 0;
        }

        /// <summary>這個關卡 / 副本是否由玩家編隊上場（資源副本與教學後的主線關卡）。</summary>
        public static bool UsesPlayerFormation(string stageId) =>
            FindDungeon(stageId) != null || LevelOf(stageId) >= FirstOpenFormationLevel;

        /// <summary>教學關：隊伍固定，不經過編隊畫面。</summary>
        public static bool FormationLocked(int level) => level < FirstOpenFormationLevel;

        /// <summary>
        /// 敵人強度（血量 %, 攻擊 %）：教學版的敵人是照固定隊伍與寫死牌序調的，
        /// 換成玩家隨機抽牌與新手隊伍後要整體放低（建議值，之後依實測調整）。
        /// </summary>
        public static (int HpPct, int AtkPct) EnemyScale(string stageId)
        {
            switch (stageId)
            {
                case "res_exp": return (60, 70);
                case "res_card": return (90, 90);
                case "1-9": return (75, 85);
                case "1-10": return (100, 100);
                default: return (100, 100);
            }
        }

        private static void ScaleEnemies(BattleSetup setup, string stageId)
        {
            var (hp, atk) = EnemyScale(stageId);
            if (hp == 100 && atk == 100) return;
            void Scale(EnemyDef def)
            {
                def.Base.Hp = def.Base.Hp * hp / 100;
                def.Base.Atk = def.Base.Atk * atk / 100;
                if (def.Summons != null) Scale(def.Summons);
            }
            foreach (var e in setup.Enemies) Scale(e.Def);
        }

        /// <summary>
        /// 開放編隊的主線關卡：沿用教學版的敵人配置，但我方改由玩家編隊決定，
        /// 並取消教學專用的限制（寫死牌序、無爆擊閃避、禁用自動戰鬥）。我方在套用編隊前是空的。
        /// </summary>
        public static BattleSetup OpenLevel(int level, ulong seed)
        {
            var setup = DemoContent.Level(level, seed);
            setup.Heroes.Clear();
            setup.FormationLocked = false;
            setup.NoRandomness = false;
            setup.ScriptedDraw = new List<string>();
            setup.AutoAllowed = true;
            return setup;
        }

        /// <summary>
        /// 資源副本的戰鬥設定：每個副本有自己的敵人配置，我方由玩家編隊決定（套用編隊前是空的），開放自動戰鬥。
        /// </summary>
        public static BattleSetup DungeonSetup(string dungeonId, ulong seed)
        {
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true };
            switch (dungeonId)
            {
                case "res_exp": // 校場操練：鐵甲力士擋路，後排妖道持續治療
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanIronBrute(), new Position(2, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(3, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanPriest(), new Position(2, 1)));
                    break;
                case "res_card": // 兵器鋪：渠帥與副將蓄力，要靠昏亂或集火打斷
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanChief(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanLieutenant(), new Position(3, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(2, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(0, 0)));
                    break;
                default: // res_gold 糧倉護衛：黃巾兵衝陣，兩名弓手在後排放箭
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(1, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(2, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanSoldier(), new Position(3, 0)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanArcher(), new Position(1, 1)));
                    setup.Enemies.Add(new EnemySlot(DemoContent.YellowTurbanArcher(), new Position(3, 1)));
                    break;
            }
            return setup;
        }

        /// <summary>
        /// 關卡 id（如 "1-3"）或資源副本 id 對應的戰鬥設定；種子由伺服器發放。
        /// 開放編隊的關卡 / 副本要給玩家資料與編隊（否則回傳 null）；教學關不需要。
        /// </summary>
        public static BattleSetup? BuildSetup(string stageId, ulong seed,
            PlayerProfile? profile = null, IReadOnlyList<FormationEntry>? formation = null)
        {
            BattleSetup setup;
            var dungeon = FindDungeon(stageId);
            if (dungeon != null) setup = DungeonSetup(dungeon.Id, seed);
            else
            {
                int level = LevelOf(stageId);
                if (level == 0) return null;
                setup = level >= FirstOpenFormationLevel ? OpenLevel(level, seed) : DemoContent.Level(level, seed);
            }
            ScaleEnemies(setup, stageId);
            if (setup.FormationLocked) return setup;
            if (profile == null || formation == null || FormationRules.Validate(profile, formation, setup.Lanes, setup.Rows) != null)
                return null;
            FormationRules.Apply(setup, profile, formation);
            return setup;
        }

        public static StageReward? FindStage(string stageId)
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return Chapter1Stage(level);
            return null;
        }
    }
}
