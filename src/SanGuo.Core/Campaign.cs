using System;
using System.Collections.Generic;
using SanGuo.Core.Content;

namespace SanGuo.Core
{
    /// <summary>
    /// 主線章節總表：第零章（涿縣盜匪教學，見 <see cref="DemoContent"/>）與第 1–6 章（GDD 04 §5、06 §1.1）。
    /// 關卡 id 為 "章-關"（"0-1"…"6-10"）。
    /// </summary>
    public static class Campaign
    {
        public const int FirstChapter = 0, LastChapter = 6, LevelsPerChapter = 10;

        /// <summary>各章末的敵人等級（= 該時間點的玩家預期等級，GDD 04 §5.3）；第零章為 12。</summary>
        public static readonly int[] ChapterEndLevel = { 12, 19, 25, 29, 32, 36, 40 };

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

        /// <summary>第三星的限定回合數。</summary>
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

        /// <summary>關卡的基準敵人等級：第零章見 <see cref="DemoContent.EnemyLevelOf"/>；其後由上一章末等級線性升到本章末等級。</summary>
        public static int EnemyLevelOf(int chapter, int level)
        {
            if (chapter == 0) return DemoContent.EnemyLevelOf(level);
            int start = ChapterEndLevel[chapter - 1], end = ChapterEndLevel[chapter];
            return start + (int)Math.Round((end - start) * level / (double)LevelsPerChapter, MidpointRounding.AwayFromZero);
        }

        /// <summary>關卡的戰鬥設定。第 1–6 章由玩家編隊（我方只有護送 / 守城目標，編隊在 <see cref="Meta.FormationRules.Apply"/> 套入）。</summary>
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

        /// <summary>解析 "章-關"；不是主線關卡回傳 false。</summary>
        public static bool TryParse(string stageId, out int chapter, out int level)
        {
            chapter = level = 0;
            int dash = stageId.IndexOf('-');
            if (dash <= 0) return false;
            if (!int.TryParse(stageId.Substring(0, dash), out chapter) || !int.TryParse(stageId.Substring(dash + 1), out level)) return false;
            if (StageId(chapter, level) != stageId || !IsValid(chapter, level)) { chapter = level = 0; return false; }
            return true;
        }

        /// <summary>下一關（跨章）；已是最後一關回傳 false。</summary>
        public static bool Next(int chapter, int level, out int nextChapter, out int nextLevel)
        {
            nextChapter = chapter;
            nextLevel = level + 1;
            if (nextLevel > LevelsPerChapter) { nextChapter++; nextLevel = 1; }
            return IsValid(nextChapter, nextLevel);
        }

        /// <summary>前一關（跨章）；第一關回傳 false。</summary>
        public static bool Previous(int chapter, int level, out int prevChapter, out int prevLevel)
        {
            prevChapter = chapter;
            prevLevel = level - 1;
            if (prevLevel < 1) { prevChapter--; prevLevel = LevelsPerChapter; }
            return IsValid(prevChapter, prevLevel);
        }

        /// <summary>已開放：第一關，或前一關（跨章）已通關。</summary>
        public static bool IsUnlocked(ICollection<string> cleared, int chapter, int level) =>
            IsValid(chapter, level) && (!Previous(chapter, level, out int pc, out int pl) || cleared.Contains(StageId(pc, pl)));

        /// <summary>目前該打的關卡：第一個未通關的關卡；全部通關則回傳最後一關。</summary>
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
    /// <summary>第 1–6 章關卡的組裝工具：敵人等級以 <see cref="Campaign.EnemyLevelOf"/> 為基準，可逐隻微調。</summary>
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

        /// <summary>敵人放在棋盤座標（欄 0–4、列 0–4）；我方入場區（欄 1–3、列 3–4）不可放。</summary>
        public StageKit At(EnemyDef def, int lane, int row, int lvAdd = 0, bool objective = false)
        {
            Setup.Enemies.Add(new EnemySlot(def, new Position(lane, row), _enemyLv + lvAdd) { IsObjective = objective });
            return this;
        }

        /// <summary>後排（列 0）。</summary>
        public StageKit Back(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 0, lvAdd);
        /// <summary>前排（列 1）。</summary>
        public StageKit Front(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 1, lvAdd);
        /// <summary>壓到我方面前（列 2）。</summary>
        public StageKit Near(EnemyDef def, int lane, int lvAdd = 0) => At(def, lane, 2, lvAdd);

        /// <summary>擊殺指定目標：擊殺所有目標即勝利。</summary>
        public StageKit Target(EnemyDef def, int lane, int row, int lvAdd = 0)
        {
            Setup.Objective = Objective.KillTarget;
            return At(def, lane, row, lvAdd, objective: true);
        }

        /// <summary>護送 / 守城：保護目標放在入場區兩側（欄 0 或 4、列 3–4），撐過 <paramref name="turns"/> 回合即勝利。</summary>
        public StageKit Protect(HeroDef npc, int lane, int row, int turns, Objective objective = Objective.Escort, int startHpPercent = 100)
        {
            Setup.Objective = objective;
            Setup.SurviveTurns = turns;
            Setup.Heroes.Add(new HeroSlot(npc, new Position(lane, row), _npcLv) { IsProtected = true, StartHpPercent = startHpPercent });
            return this;
        }

        /// <summary>限時：在限定回合內達成目標。</summary>
        public StageKit Limit(int turns)
        {
            Setup.TurnLimit = turns;
            return this;
        }
    }
}
