using System;
using System.Collections.Generic;
using SanGuo.Core.Content;

namespace SanGuo.Core
{
    public static class Campaign
    {
        public const int FirstChapter = 0, LastChapter = 6, LevelsPerChapter = 10;

        public static readonly int[] ChapterEndLevel = { 12, 28, 34, 38, 42, 47, 54 };

        private static readonly string[] Titles =
        {
            "第零章　涿縣盜匪", "第一章　黃巾烽火（上）", "第二章　黃巾烽火（下）", "第三章　洛陽風雲",
            "第四章　十八路諸侯", "第五章　虎牢關", "第六章　火燒洛陽",
        };

        public static bool IsValid(int chapter, int level) =>
            chapter >= FirstChapter && chapter <= LastChapter && level >= 1 && level <= LevelsPerChapter;

        public static string Title(int chapter) => Titles[chapter];

        public static string[] LevelNames(int chapter)
        {
            switch (chapter)
            {
                case 0: return DemoContent.LevelNames;
                case 1: return Chapter1.Names;
                case 2: return Chapter2.Names;
                case 3: return Chapter3.Names;
                case 4: return Chapter4.Names;
                case 5: return Chapter5.Names;
                case 6: return Chapter6.Names;
                default: throw new ArgumentOutOfRangeException(nameof(chapter));
            }
        }

        public static string LevelName(int chapter, int level) => LevelNames(chapter)[level - 1];

        public static int TurnPar(int chapter, int level)
        {
            switch (chapter)
            {
                case 0: return DemoContent.TurnPar[level - 1];
                case 1: return Chapter1.Pars[level - 1];
                case 2: return Chapter2.Pars[level - 1];
                case 3: return Chapter3.Pars[level - 1];
                case 4: return Chapter4.Pars[level - 1];
                case 5: return Chapter5.Pars[level - 1];
                case 6: return Chapter6.Pars[level - 1];
                default: throw new ArgumentOutOfRangeException(nameof(chapter));
            }
        }

        public static int EnemyLevelOf(int chapter, int level)
        {
            if (chapter == 0) return DemoContent.EnemyLevelOf(level);
            int start = ChapterEndLevel[chapter - 1], end = ChapterEndLevel[chapter];
            return start + (int)Math.Round((end - start) * level / (double)LevelsPerChapter, MidpointRounding.AwayFromZero);
        }

        public static BattleSetup Setup(int chapter, int level, ulong seed = 1)
        {
            switch (chapter)
            {
                case 0: return DemoContent.Level(level, seed);
                case 1: return Chapter1.Level(level, seed);
                case 2: return Chapter2.Level(level, seed);
                case 3: return Chapter3.Level(level, seed);
                case 4: return Chapter4.Level(level, seed);
                case 5: return Chapter5.Level(level, seed);
                case 6: return Chapter6.Level(level, seed);
                default: throw new ArgumentOutOfRangeException(nameof(chapter));
            }
        }

        public static string StageId(int chapter, int level) => $"{chapter}-{level}";

        public static bool TryParse(string stageId, out int chapter, out int level)
        {
            chapter = level = 0;
            int dash = stageId.IndexOf('-');
            if (dash <= 0) return false;
            if (!int.TryParse(stageId.Substring(0, dash), out chapter) || !int.TryParse(stageId.Substring(dash + 1), out level)) return false;
            if (StageId(chapter, level) != stageId || !IsValid(chapter, level)) { chapter = level = 0; return false; }
            return true;
        }

        public static bool Next(int chapter, int level, out int nextChapter, out int nextLevel)
        {
            nextChapter = chapter;
            nextLevel = level + 1;
            if (nextLevel > LevelsPerChapter) { nextChapter++; nextLevel = 1; }
            return IsValid(nextChapter, nextLevel);
        }

        public static bool Previous(int chapter, int level, out int prevChapter, out int prevLevel)
        {
            prevChapter = chapter;
            prevLevel = level - 1;
            if (prevLevel < 1) { prevChapter--; prevLevel = LevelsPerChapter; }
            return IsValid(prevChapter, prevLevel);
        }

        public static bool IsUnlocked(ICollection<string> cleared, int chapter, int level) =>
            IsValid(chapter, level) && (!Previous(chapter, level, out int pc, out int pl) || cleared.Contains(StageId(pc, pl)));

        public static (int Chapter, int Level) Frontier(ICollection<string> cleared)
        {
            for (int c = FirstChapter; c <= LastChapter; c++)
                for (int l = 1; l <= LevelsPerChapter; l++)
                    if (!cleared.Contains(StageId(c, l))) return (c, l);
            return (LastChapter, LevelsPerChapter);
        }
    }
}

namespace SanGuo.Core.Content
{
    internal sealed class StageKit
    {
        public readonly BattleSetup Setup;
        private readonly int _enemyLv, _npcLv;

        public StageKit(int chapter, int level, ulong seed)
        {
            Setup = new BattleSetup { Seed = seed, AutoAllowed = true };
            _enemyLv = Campaign.EnemyLevelOf(chapter, level);
            _npcLv = Campaign.ChapterEndLevel[chapter];
        }

        public StageKit At(EnemyDef def, int lane, int row, int lvAdd = 0, bool objective = false)
        {
            Setup.Enemies.Add(new EnemySlot(def, new Position(lane, row), _enemyLv + lvAdd) { IsObjective = objective });
            return this;
        }

        public StageKit Back(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 0, lvAdd);
        public StageKit Front(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 1, lvAdd);
        public StageKit Near(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 2, lvAdd);

        public StageKit Target(EnemyDef def, int lane, int row, int lvAdd = 0)
        {
            Setup.Objective = Objective.KillTarget;
            return At(def, lane, row, lvAdd, objective: true);
        }

        public StageKit Protect(HeroDef npc, int lane, int row, int turns, Objective objective = Objective.Escort, int startHpPercent = 100)
        {
            Setup.Objective = objective;
            Setup.SurviveTurns = turns;
            Setup.Heroes.Add(new HeroSlot(npc, new Position(lane, row), _npcLv) { IsProtected = true, StartHpPercent = startHpPercent });
            return this;
        }

        public StageKit Limit(int turns)
        {
            Setup.TurnLimit = turns;
            return this;
        }
    }
}
