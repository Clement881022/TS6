using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    public static class Chapter5
    {
        public static readonly string[] Names =
        {
            "兵進虎牢", "虎牢前哨", "穆順方悅", "并州狼騎", "高順",
            "陷陣營", "救公孫瓚", "虎牢城下", "三英合力", "呂布",
        };

        public static readonly int[] Pars = { 11, 6, 10, 12, 11, 7, 6, 12, 8, 15 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(5, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(BzCavalry(), 1).Front(BzCavalry(), 2).Front(BzCavalry(), 3).Back(BzHorseArcher(), 1).Back(BzHorseArcher(), 3);
                    break;
                case 2:
                    k.Protect(HeroRoster.Npc("npc_courier", "傳令兵", 560), 0, 4, 7)
                     .Front(BzCavalry(), 0).Front(BzCavalry(), 1).Near(BzCavalry(), 0).Back(BzHorseArcher(), 2).Back(BzHorseArcher(), 3);
                    break;
                case 3:
                    k.Target(BzCaptain(), 1, 1).Target(BzCaptain(), 3, 1).Front(XianzhenGuard(), 2).Back(BzHorseArcher(), 0).Back(BzHorseArcher(), 4);
                    break;
                case 4:
                    k.Front(BzCavalry(), 0).Front(BzCavalry(), 1).Front(BzCavalry(), 2).Front(BzCavalry(), 3).Front(BzCavalry(), 4).Back(BzHorseArcher(), 2);
                    break;
                case 5:
                    k.Target(GaoShun(), 2, 1).Front(BzCavalry(), 1).Front(BzCavalry(), 3).Back(BzHorseArcher(), 0);
                    break;
                case 6:
                    k.Protect(HeroRoster.Npc("npc_camp", "聯軍營門", 770), 4, 4, 8, Objective.Defend)
                     .Front(XianzhenGuard(), 3).Front(XianzhenGuard(), 4).Near(XianzhenGuard(), 4).Back(BzHorseArcher(), 2).Back(BzHorseArcher(), 3).Back(XlAdvisor(), 4);
                    break;
                case 7:
                    k.Protect(HeroRoster.Npc("npc_gongsunzan", "公孫瓚", 700), 0, 4, 7, startHpPercent: 60)
                     .Front(BzCavalry(), 0).Front(BzCavalry(), 1).Near(BzCaptain(), 0).Back(BzHorseArcher(), 1).Back(BzHorseArcher(), 2);
                    break;
                case 8:
                    k.Front(BzCavalry(), 1).Front(XianzhenGuard(), 2).Front(BzCavalry(), 3).Back(BzHorseArcher(), 0).Back(BzHorseArcher(), 4);
                    break;
                case 9:
                    k.Limit(12).Front(BzCavalry(), 1).Front(BzCaptain(), 2).Front(BzCavalry(), 3).Back(BzHorseArcher(), 4);
                    break;
                case 10:
                    k.Front(LvBu(), 2).Front(BzCavalry(), 1).Front(BzCavalry(), 3).Back(BzHorseArcher(), 0).Back(BzHorseArcher(), 2).Back(BzHorseArcher(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
