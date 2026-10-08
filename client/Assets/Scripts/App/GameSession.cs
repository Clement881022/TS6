#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    /// <summary>已向後端開始的一場戰鬥（體力已扣、種子已取得），由「地圖 / 編隊 / 副本」交給戰鬥場景。</summary>
    public sealed class BattleTicket
    {
        public string StageId = "";
        public ulong Seed;
        public int Chapter;
        public int Level = 1;
        /// <summary>資源副本時不為 null（主線關卡為 null）。</summary>
        public ResourceDungeonDef? Dungeon;
        /// <summary>開放編隊的關卡 / 副本：開始時送給後端的編隊（戰鬥用同一份重建）；教學關為 null。</summary>
        public List<FormationEntry>? Formation;
    }

    /// <summary>
    /// 跨場景共用的狀態：後端、玩家資料快照、編隊、目前選的關卡與待開打的戰鬥。
    /// 場景切換時 static 欄位會保留；第一次用到時才初始化，所以可以從任何場景直接按 Play。
    /// </summary>
    public static class GameSession
    {
        public const int MaxTeamSize = 4;

        private static IGameBackend? _backend;

        public static IGameBackend Backend
        {
            get { EnsureInit(); return _backend!; }
        }

        public static ProfileView View { get; private set; } = new ProfileView();
        public static readonly List<HeroDef> Roster = DemoContent.Roster();
        /// <summary>玩家排好的隊伍與站位（武將 Id → 格子）。</summary>
        public static readonly Dictionary<string, Position> Formation = new Dictionary<string, Position>();
        /// <summary>編隊頁要為哪個關卡 / 副本排兵（主線 "1-9" 或副本 id）。</summary>
        public static string FormationStageId = "";
        /// <summary>目前選的主線關卡（章、關）；地圖顯示 SelectedChapter 這一章。</summary>
        public static int SelectedChapter;
        public static int SelectedLevel = 1;
        /// <summary>false = 還沒選過關卡，第一次進地圖時跳到目前該打的那一章。</summary>
        public static bool StageChosen;
        public static bool OpenSelectedStageOnMap;
        public static BattleTicket? Ticket;
        public static string? ShotDir { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _backend = null;
            View = new ProfileView();
            Formation.Clear();
            FormationStageId = "";
            SelectedChapter = 0;
            SelectedLevel = 1;
            StageChosen = false;
            OpenSelectedStageOnMap = false;
            Ticket = null;
            ShotDir = null;
        }

        public static void EnsureInit()
        {
            if (_backend != null) return;
            // 有 -sanguoServer <網址> 就連伺服器（-sanguoAccount 指定帳號），否則用單機存檔。
            string? server = CommandLineValue("-sanguoServer");
            ShotDir = CommandLineValue("-sanguoShot");
            _backend = server != null
                ? new RemoteBackend(server, CommandLineValue("-sanguoAccount") ?? "dev-" + SystemInfo.deviceUniqueIdentifier)
                : new LocalBackend(persist: ShotDir == null);
            string? levelArg = CommandLineValue("-sanguoLevel");
            // -sanguoLevel 接受 "章-關"（如 3-10）或只給關數（第零章）。
            if (levelArg != null && Campaign.TryParse(levelArg, out int argChapter, out int argLevel)) Select(argChapter, argLevel);
            else if (levelArg != null && int.TryParse(levelArg, out int level)) Select(0, Math.Clamp(level, 1, Campaign.LevelsPerChapter));
        }

        public static string? CommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        /// <summary>向後端重抓玩家資料；失敗回傳 false（畫面保留舊資料）。</summary>
        public static async Task<bool> Refresh()
        {
            try
            {
                var v = await Backend.GetProfile();
                if (v == null) return false;
                View = v;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public static HeroDef? DefOf(string id) => Roster.Find(h => h.Id == id);

        public static string StageIdOf(int chapter, int level) => Campaign.StageId(chapter, level);

        public static bool IsUnlocked(int chapter, int level) => Campaign.IsUnlocked(View.ClearedStages, chapter, level);

        /// <summary>教學關：隊伍固定，不經過編隊畫面。</summary>
        public static bool FormationLocked(int chapter, int level) => DemoMeta.FormationLocked(chapter, level);

        public static void Select(int chapter, int level)
        {
            SelectedChapter = chapter;
            SelectedLevel = level;
            StageChosen = true;
        }

        /// <summary>目前該打的關卡（第一個未通關的關卡，跨章）。</summary>
        public static (int Chapter, int Level) Frontier() => Campaign.Frontier(View.ClearedStages);

        /// <summary>還沒選過關卡就選目前該打的那一關（地圖第一次打開時用）。</summary>
        public static void EnsureSelection()
        {
            if (StageChosen) return;
            var (c, l) = Frontier();
            Select(c, l);
        }

        // 列陣區只有 3x2（欄 1–3、列 3 = 前排、列 4 = 後排）。
        private static readonly (int Lane, int Row)[] FrontCells = { (2, 3), (1, 3), (3, 3) };
        private static readonly (int Lane, int Row)[] BackCells = { (2, 4), (1, 4), (3, 4) };

        /// <summary>已擁有的武將（照名冊順序：UR 在前、R 在後）。</summary>
        public static List<HeroDef> OwnedHeroes() =>
            Roster.Where(d => View.Heroes.ContainsKey(d.Id)).OrderByDescending(d => d.Rarity).ToList();

        /// <summary>
        /// 把編隊整理成合法狀態：丟掉沒擁有的武將；編隊是空的就自動排一隊
        /// （最多 4 人，坦克與戰士站前排，其餘站後排）。
        /// </summary>
        public static void EnsureFormation()
        {
            foreach (var id in Formation.Keys.Where(id => !View.Heroes.ContainsKey(id)).ToList()) Formation.Remove(id);
            if (Formation.Count > 0) return;
            int front = 0, back = 0;
            foreach (var def in OwnedHeroes().Take(MaxTeamSize))
            {
                bool isFront = def.Role == Role.Tank || def.Role == Role.Warrior;
                // 優先放在自己偏好的那一排，滿了就放另一排（3x2 列陣區，上限 4 人一定放得下）。
                var cell = (isFront && front < FrontCells.Length) || back >= BackCells.Length ? FrontCells[front++] : BackCells[back++];
                Formation[def.Id] = new Position(cell.Lane, cell.Row);
            }
        }

        /// <summary>目前編隊轉成送給後端的列表（順序固定：依路、排），客戶端與伺服器用同一份重建戰鬥。</summary>
        public static List<FormationEntry> FormationEntries() =>
            Formation.OrderBy(kv => kv.Value.Lane).ThenBy(kv => kv.Value.Row)
                .Select(kv => new FormationEntry(kv.Key, kv.Value.Lane, kv.Value.Row)).ToList();

        /// <summary>編隊頁用：這個關卡 / 副本的敵人預覽（只看敵人，我方由玩家決定）。</summary>
        public static BattleSetup? EnemyPreview(string stageId)
        {
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (dungeon != null) return DemoMeta.DungeonSetup(dungeon.Id, 1);
            if (stageId == WorldBoss.StageId) return WorldBoss.Setup(WorldBoss.SeasonOf(View.Now), 1);
            return Campaign.TryParse(stageId, out int chapter, out int level)
                ? DemoMeta.OpenLevel(chapter, level, 1) : null;
        }

        /// <summary>
        /// 向後端開始關卡 / 副本（檢查條件、扣體力、取得種子）。成功回傳 null 並設好 Ticket；失敗回傳要顯示的原因。
        /// </summary>
        public static async Task<string?> BeginStage(string stageId)
        {
            try
            {
                List<FormationEntry>? formation = null;
                if (DemoMeta.UsesPlayerFormation(stageId))
                {
                    EnsureFormation();
                    formation = FormationEntries();
                }
                var r = await Backend.StartStage(stageId, formation);
                if (!r.Ok) return UiText.ExplainBackend(r.Code);
                var dungeon = DemoMeta.FindDungeon(stageId);
                if (dungeon == null && Campaign.TryParse(stageId, out int chapter, out int level)) Select(chapter, level);
                Ticket = new BattleTicket { StageId = stageId, Seed = r.Seed, Chapter = SelectedChapter, Level = SelectedLevel, Dungeon = dungeon, Formation = formation };
                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return UiText.ExplainBackend("network");
            }
        }
    }
}
