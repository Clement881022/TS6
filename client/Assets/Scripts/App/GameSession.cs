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
        public int Level = 1;
        /// <summary>資源副本時不為 null（主線關卡為 null）。</summary>
        public ResourceDungeonDef? Dungeon;
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
        public static int SelectedLevel = 1;
        public static BattleTicket? Ticket;
        public static string? ShotDir { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _backend = null;
            View = new ProfileView();
            Formation.Clear();
            SelectedLevel = 1;
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
            if (levelArg != null && int.TryParse(levelArg, out int level)) SelectedLevel = Math.Clamp(level, 1, DemoContent.ChapterLevelCount);
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

        public static string StageIdOf(int level) => DemoMeta.StageId(1, level);

        public static bool IsUnlocked(int level) => level == 1 || View.ClearedStages.Contains(StageIdOf(level - 1));

        /// <summary>教學關：隊伍固定，不經過編隊畫面。</summary>
        public static bool FormationLocked(int level) => DemoContent.Level(level, 1).FormationLocked;

        /// <summary>把玩家排好的隊伍與站位套用到關卡設定；第一次使用關卡的預設隊伍。</summary>
        public static void ApplyFormation(BattleSetup setup)
        {
            if (Formation.Count == 0)
                foreach (var h in setup.Heroes) Formation[h.Def.Id] = h.Pos;
            setup.Heroes.Clear();
            foreach (var def in Roster.Where(d => Formation.ContainsKey(d.Id)))
                setup.Heroes.Add(new HeroSlot(def, Formation[def.Id]));
        }

        /// <summary>
        /// 向後端開始關卡 / 副本（檢查條件、扣體力、取得種子）。成功回傳 null 並設好 Ticket；失敗回傳要顯示的原因。
        /// </summary>
        public static async Task<string?> BeginStage(string stageId)
        {
            try
            {
                var r = await Backend.StartStage(stageId);
                if (!r.Ok) return UiText.ExplainBackend(r.Code);
                var dungeon = DemoMeta.FindDungeon(stageId);
                int level = SelectedLevel;
                if (dungeon == null && int.TryParse(stageId.Substring(stageId.IndexOf('-') + 1), out int parsed)) level = parsed;
                SelectedLevel = level;
                Ticket = new BattleTicket { StageId = stageId, Seed = r.Seed, Level = level, Dungeon = dungeon };
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
