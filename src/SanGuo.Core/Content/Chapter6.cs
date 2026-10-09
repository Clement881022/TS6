using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    public static class Chapter6
    {
        public static readonly string[] Names =
        {
            "董卓西遷", "洛陽大火", "搶救糧倉", "斷後追兵", "徐榮",
            "滎陽救曹", "西涼鐵壁", "李傕郭汜", "長安道上", "董卓",
        };

        public static readonly int[] Pars = { 11, 6, 7, 8, 13, 6, 12, 12, 8, 16 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(6, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(Cavalry(), 1).Front(Guard(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 2:
                    k.Protect(HeroRoster.Npc("npc_refugee", "洛陽百姓", 560), 4, 4, 7, startHpPercent: 80)
                     .Front(Cavalry(), 3).Front(Cavalry(), 4).Near(Cavalry(), 4).Back(HorseArcher(), 2).Back(HorseArcher(), 3).Back(XlAdvisor(), 1);
                    break;
                case 3:
                    k.Protect(HeroRoster.Npc("npc_granary", "糧倉", 840), 0, 4, 8, Objective.Defend)
                     .Front(Guard(), 0).Front(Guard(), 1).Near(Cavalry(), 0).Back(HorseArcher(), 1).Back(XlAdvisor(), 2);
                    break;
                case 4:
                    k.Limit(12).Front(Cavalry(), 0).Front(Cavalry(), 2).Front(Cavalry(), 4).Back(HorseArcher(), 1).Back(HorseArcher(), 3);
                    break;
                case 5:
                    k.Back(XuRong(), 2).Front(Guard(), 1).Front(Guard(), 2).Front(Guard(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 6:
                    k.Protect(HeroRoster.Npc("npc_caocao", "曹操", 700), 4, 4, 7, startHpPercent: 60)
                     .Front(Cavalry(), 3, -6).Front(Cavalry(), 4, -6).Near(XlCaptain(), 4, -6).Back(HorseArcher(), 2, -6).Back(HorseArcher(), 3, -6);
                    break;
                case 7:
                    k.Front(Guard(), 1).Front(Guard(), 2).Front(Guard(), 3).Back(HorseArcher(), 0);
                    break;
                case 8:
                    k.Front(LiJue(), 1).Front(Cavalry(), 3).Back(GuoSi(), 3);
                    break;
                case 9:
                    k.Limit(12).Front(Cavalry(), 1).Front(XlCaptain(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 4);
                    break;
                case 10:
                    k.Front(DongZhuo(), 2, 4).Front(Guard(), 1).Front(Guard(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 2).Back(HorseArcher(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
