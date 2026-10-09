using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    public static class Chapter1
    {
        public static readonly string[] Names =
        {
            "涿郡募兵", "黃巾前哨", "救援鄉民", "力士擋道", "渠帥程遠志",
            "青州解圍", "投奔盧植", "潁川道上", "長社火攻", "波才",
        };

        public static readonly int[] Pars = { 10, 10, 6, 11, 9, 7, 11, 12, 8, 14 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(1, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(YtSoldier(), 1).Front(YtSoldier(), 2).Front(YtSoldier(), 3).Back(YtArcher(), 2);
                    break;
                case 2:
                    k.Front(YtSoldier(), 1).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtArcher(), 4).Back(YtSorcerer(), 2);
                    break;
                case 3:
                    k.Protect(HeroRoster.Npc("npc_refugee", "逃難鄉民", 560), 0, 4, 7, startHpPercent: 80)
                     .Front(YtSoldier(), 0).Front(YtSoldier(), 2).Back(YtArcher(), 1).Back(YtArcher(), 3).Near(YtSoldier(), 4);
                    break;
                case 4:
                    k.Front(YtBrute(), 1).Front(YtSoldier(), 2).Front(YtBrute(), 3).Back(YtArcher(), 2);
                    break;
                case 5:
                    k.Target(ChengYuanzhi(), 2, 1).Front(YtSoldier(), 1).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtArcher(), 4);
                    break;
                case 6:
                    k.Protect(HeroRoster.Npc("npc_gate", "青州城門", 700), 4, 4, 8, Objective.Defend)
                     .Front(YtSoldier(), 2).Front(YtSoldier(), 3).Near(YtSoldier(), 4).Back(YtArcher(), 3).Back(YtSorcerer(), 4);
                    break;
                case 7:
                    k.Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtSorcerer(), 0).Back(YtSorcerer(), 4);
                    break;
                case 8:
                    k.Front(YtSoldier(), 1).Front(YtCaptain(), 2).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtSorcerer(), 4);
                    break;
                case 9:
                    k.Limit(12).Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtSorcerer(), 0).Back(YtSorcerer(), 4);
                    break;
                case 10:
                    k.Front(BoCai(), 2).Front(YtSoldier(), 1).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtSorcerer(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
