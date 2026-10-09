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
    public sealed class BattleTicket
    {
        public string StageId = "";
        public ulong Seed;
        public int Chapter;
        public int Level = 1;
        public ResourceDungeonDef? Dungeon;
        public List<FormationEntry>? Formation;
    }

    public static class GameSession
    {
        public const int MaxTeamSize = 4;

        private static IGameBackend? _backend;

        public static IGameBackend Backend
        {
            get { EnsureInit(); return _backend!; }
        }

        public static IAccountBackend? Accounts => Backend as IAccountBackend;
        public static AccountInfo? Account { get; set; }

        public static string DisplayName => Account?.Nickname is { Length: > 0 } n ? n : "主公";

        public static ProfileView View { get; private set; } = new ProfileView();
        public static readonly List<HeroDef> Roster = DemoContent.Roster();
        public static readonly Dictionary<string, Position> Formation = new Dictionary<string, Position>();
        public static string FormationStageId = "";
        public static int SelectedChapter;
        public static int SelectedLevel = 1;
        public static bool StageChosen;
        public static bool OpenSelectedStageOnMap;
        public static BattleTicket? Ticket;
        public static string? ShotDir { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _backend = null;
            Account = null;
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
            string? server = CommandLineValue("-sanguoServer");
            ShotDir = CommandLineValue("-sanguoShot");
            _backend = server != null
                ? new RemoteBackend(server, CommandLineValue("-sanguoAccount"))
                : new LocalBackend(persist: ShotDir == null);
            if (_backend is IAccountBackend accounts)
                accounts.SessionLost += () =>
                {
                    Account = null;
                    if (!(PageHost.Current?.ActivePage is LoginPage)) Nav.Go(Page.Login);
                };
            string? levelArg = CommandLineValue("-sanguoLevel");
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

        public static async Task<bool> Refresh()
        {
            try
            {
                var v = await Backend.GetProfile();
                if (v == null) return false;
                View = v;
                if (Account == null && Accounts != null)
                {
                    var a = await Accounts.GetAccount();
                    if (a.Ok) Account = a.Account;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public static HeroDef? DefOf(string id) => Roster.Find(h => h.Id == id);

        public static bool HardMode;

        public static bool HardModeOpen => View.ClearedStages.Contains(HardStages.UnlockStage);

        public static string StageIdOf(int chapter, int level) =>
            HardMode ? HardStages.StageId(chapter, level) : Campaign.StageId(chapter, level);

        public static bool IsUnlocked(int chapter, int level) =>
            HardMode ? HardStages.IsUnlocked(View.ClearedStages, chapter, level) : Campaign.IsUnlocked(View.ClearedStages, chapter, level);

        public static bool FormationLocked(int chapter, int level) => !HardMode && DemoMeta.FormationLocked(chapter, level);

        public static void Select(int chapter, int level)
        {
            SelectedChapter = chapter;
            SelectedLevel = level;
            StageChosen = true;
        }

        public static (int Chapter, int Level) Frontier() => Campaign.Frontier(View.ClearedStages);

        public static void EnsureSelection()
        {
            if (StageChosen) return;
            var (c, l) = Frontier();
            Select(c, l);
        }

        private static readonly (int Lane, int Row)[] FrontCells = { (2, 3), (1, 3), (3, 3) };
        private static readonly (int Lane, int Row)[] BackCells = { (2, 4), (1, 4), (3, 4) };

        public static List<HeroDef> OwnedHeroes() =>
            Roster.Where(d => View.Heroes.ContainsKey(d.Id)).OrderByDescending(d => d.Rarity).ToList();

        public static void EnsureFormation()
        {
            foreach (var id in Formation.Keys.Where(id => !View.Heroes.ContainsKey(id)).ToList()) Formation.Remove(id);
            if (Formation.Count > 0) return;
            int front = 0, back = 0;
            foreach (var def in OwnedHeroes().Take(MaxTeamSize))
            {
                bool isFront = def.Role == Role.Tank || def.Role == Role.Warrior;
                var cell = (isFront && front < FrontCells.Length) || back >= BackCells.Length ? FrontCells[front++] : BackCells[back++];
                Formation[def.Id] = new Position(cell.Lane, cell.Row);
            }
        }

        public static List<FormationEntry> FormationEntries() =>
            Formation.OrderBy(kv => kv.Value.Lane).ThenBy(kv => kv.Value.Row)
                .Select(kv => new FormationEntry(kv.Key, kv.Value.Lane, kv.Value.Row)).ToList();

        public static BattleSetup? EnemyPreview(string stageId)
        {
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (dungeon != null) return DemoMeta.DungeonSetup(dungeon.Id, 1);
            if (stageId == WorldBoss.StageId) return WorldBoss.Setup(WorldBoss.SeasonOf(View.Now), 1);
            if (HardStages.TryParse(stageId, out int hc, out int hl)) return HardStages.Setup(hc, hl, 1);
            return Campaign.TryParse(stageId, out int chapter, out int level)
                ? DemoMeta.OpenLevel(chapter, level, 1) : null;
        }

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
                if (dungeon == null && (Campaign.TryParse(stageId, out int chapter, out int level) || HardStages.TryParse(stageId, out chapter, out level)))
                    Select(chapter, level);
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
