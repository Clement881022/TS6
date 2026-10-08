using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    /// <summary>第五章　虎牢關：并州軍、陷陣營；高順，章末 Boss 呂布。敵人等級 33→36。</summary>
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
                    k.Front(Cavalry(), 1).Front(Cavalry(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 1).Back(HorseArcher(), 3);
                    break;
                case 2: // 護送：左下角的傳令兵
                    k.Protect(HeroRoster.Npc("npc_courier", "傳令兵", 560), 0, 4, 7)
                     .Front(Cavalry(), 0).Front(Cavalry(), 1).Near(Cavalry(), 0).Back(HorseArcher(), 2).Back(HorseArcher(), 3);
                    break;
                case 3: // 擊殺指定：兩名校尉都要擊殺
                    k.Target(XlCaptain(), 1, 1).Target(XlCaptain(), 3, 1).Front(Guard(), 2).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 4: // 五騎一排：適合橫排攻擊
                    k.Front(Cavalry(), 0).Front(Cavalry(), 1).Front(Cavalry(), 2).Front(Cavalry(), 3).Front(Cavalry(), 4).Back(HorseArcher(), 2);
                    break;
                case 5: // 擊殺指定：高順（防禦極高）
                    k.Target(GaoShun(), 2, 1).Front(Cavalry(), 1).Front(Cavalry(), 3).Back(HorseArcher(), 0);
                    break;
                case 6: // 守城：右下角的聯軍營門
                    k.Protect(HeroRoster.Npc("npc_camp", "聯軍營門", 770), 4, 4, 8, Objective.Defend)
                     .Front(Guard(), 3).Front(Guard(), 4).Near(Guard(), 4).Back(HorseArcher(), 2).Back(HorseArcher(), 3).Back(XlAdvisor(), 4);
                    break;
                case 7: // 護送：落馬的公孫瓚
                    k.Protect(HeroRoster.Npc("npc_gongsunzan", "公孫瓚", 700), 0, 4, 7, startHpPercent: 60)
                     .Front(Cavalry(), 0).Front(Cavalry(), 1).Near(XlCaptain(), 0).Back(HorseArcher(), 1).Back(HorseArcher(), 2);
                    break;
                case 8:
                    k.Front(Cavalry(), 1).Front(Guard(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 9: // 限時
                    k.Limit(12).Front(Cavalry(), 1).Front(XlCaptain(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 4);
                    break;
                case 10: // Boss 呂布：移動力 2、爆擊高
                    k.Front(LvBu(), 2).Front(Cavalry(), 1).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 2).Back(HorseArcher(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
