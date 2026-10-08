using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    /// <summary>第三章　洛陽風雲：督郵、十常侍、董卓入京；張讓，章末 Boss 李儒。敵人等級 26→29。</summary>
    public static class Chapter3
    {
        public static readonly string[] Names =
        {
            "鞭打督郵", "亡命途中", "洛陽城外", "宮門血案", "段珪",
            "十常侍", "西涼軍入京", "守護城門", "洛陽夜巷", "李儒",
        };

        public static readonly int[] Pars = { 10, 6, 11, 7, 11, 13, 11, 7, 13, 15 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(3, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(HanSoldier(), 1).Front(HanSoldier(), 2).Front(HanSoldier(), 3).Back(HanArcher(), 1).Back(HanArcher(), 3);
                    break;
                case 2: // 護送：同行的鄉勇在左下角
                    k.Protect(HeroRoster.Npc("npc_militia", "受傷鄉勇", 560), 0, 4, 7, startHpPercent: 70)
                     .Front(HanSoldier(), 0).Front(HanSoldier(), 1).Near(HanSoldier(), 0).Back(HanArcher(), 2).Back(HanArcher(), 3);
                    break;
                case 3: // 禁軍擋在前、宦官黨羽在後放法術
                    k.Front(Guard(), 1).Front(Guard(), 3).Back(Eunuch(), 0).Back(Eunuch(), 2).Back(Eunuch(), 4);
                    break;
                case 4: // 限時
                    k.Limit(11).Front(HanSoldier(), 1).Front(Guard(), 2).Front(HanSoldier(), 3).Back(Eunuch(), 0).Back(Eunuch(), 4);
                    break;
                case 5: // 擊殺指定：段珪
                    k.Target(DuanGui(), 2, 0).Front(HanSoldier(), 1).Front(Guard(), 2).Front(HanSoldier(), 3);
                    break;
                case 6: // Boss 張讓
                    k.Back(ZhangRang(), 2).Front(HanSoldier(), 1).Front(Guard(), 2).Front(HanSoldier(), 3).Back(Eunuch(), 4);
                    break;
                case 7: // 西涼鐵騎移動力 2，很快就衝到面前
                    k.Front(Cavalry(), 1).Front(Cavalry(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 8: // 守城：右下角的城門
                    k.Protect(HeroRoster.Npc("npc_gate", "洛陽城門", 770), 4, 4, 8, Objective.Defend)
                     .Front(Cavalry(), 3).Front(Cavalry(), 4).Near(Cavalry(), 4).Back(HorseArcher(), 2).Back(HorseArcher(), 3);
                    break;
                case 9:
                    k.Front(Cavalry(), 1).Front(XlCaptain(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 4);
                    break;
                case 10: // Boss 李儒（法術）
                    k.Back(LiRu(), 2).Front(Cavalry(), 1).Front(HanSoldier(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
