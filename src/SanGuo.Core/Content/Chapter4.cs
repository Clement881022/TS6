using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    /// <summary>第四章　十八路諸侯：關東聯軍結盟、汜水關；胡軫，章末 Boss 華雄。敵人等級 30→32。</summary>
    public static class Chapter4
    {
        public static readonly string[] Names =
        {
            "陳留起兵", "北平故人", "酸棗會盟", "糧道告急", "胡軫",
            "孫堅敗走", "汜水關外", "折將之恥", "夜探汜水", "華雄",
        };

        public static readonly int[] Pars = { 11, 11, 7, 6, 11, 8, 12, 12, 8, 15 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(4, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(Cavalry(), 1).Front(Cavalry(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 2: // 弓騎齊射
                    k.Front(Cavalry(), 0).Front(Cavalry(), 4).Back(HorseArcher(), 1).Back(HorseArcher(), 2).Back(HorseArcher(), 3);
                    break;
                case 3: // 守城：左下角的聯軍營門
                    k.Protect(HeroRoster.Npc("npc_camp", "聯軍營門", 770), 0, 4, 8, Objective.Defend)
                     .Front(Cavalry(), 0).Front(Cavalry(), 1).Near(Cavalry(), 0).Back(HorseArcher(), 1).Back(XlAdvisor(), 2);
                    break;
                case 4: // 護送：右下角的糧車
                    k.Protect(HeroRoster.Npc("npc_supply", "糧車", 630), 4, 4, 7)
                     .Front(Cavalry(), 3).Front(Cavalry(), 4).Near(Cavalry(), 4).Back(HorseArcher(), 2).Back(HorseArcher(), 4);
                    break;
                case 5: // 擊殺指定：胡軫
                    k.Target(HuZhen(), 2, 1).Front(Guard(), 1).Front(Cavalry(), 3).Back(HorseArcher(), 0);
                    break;
                case 6: // 限時
                    k.Limit(12).Front(Cavalry(), 1).Front(Cavalry(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 7: // 禁軍列陣：防禦高
                    k.Front(Guard(), 1).Front(Cavalry(), 2).Front(Guard(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 4);
                    break;
                case 8: // 兩名校尉蓄力
                    k.Front(XlCaptain(), 1).Front(Guard(), 2).Front(Cavalry(), 3).Back(HorseArcher(), 4);
                    break;
                case 9: // 限時
                    k.Limit(12).Front(Cavalry(), 0).Front(Cavalry(), 2).Front(Cavalry(), 4).Back(HorseArcher(), 1).Back(HorseArcher(), 3);
                    break;
                case 10: // Boss 華雄
                    k.Front(HuaXiong(), 2).Front(Cavalry(), 1).Front(Cavalry(), 3).Back(HorseArcher(), 0).Back(HorseArcher(), 2).Back(HorseArcher(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
