using static SanGuo.Core.Content.Enemies;

namespace SanGuo.Core.Content
{
    /// <summary>第二章　黃巾烽火（下）：廣宗、陽城、宛城；張寶、張梁，章末 Boss 張角。敵人等級 20→25。</summary>
    public static class Chapter2
    {
        public static readonly string[] Names =
        {
            "盧植被誣", "救董卓", "妖霧", "破妖術", "地公將軍",
            "宛城糧道", "宛城攻城", "人公將軍", "廣宗夜戰", "天公將軍",
        };

        public static readonly int[] Pars = { 11, 6, 10, 10, 13, 7, 8, 11, 13, 15 };

        public static BattleSetup Level(int level, ulong seed)
        {
            var k = new StageKit(2, level, seed);
            switch (level)
            {
                case 1:
                    k.Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtArcher(), 4);
                    break;
                case 2: // 護送：負傷的董卓在右下角
                    k.Protect(HeroRoster.Npc("npc_dongzhuo", "董卓", 700), 4, 4, 7, startHpPercent: 70)
                     .Front(YtSoldier(), 3).Front(YtSoldier(), 4).Near(YtSoldier(), 4).Back(YtArcher(), 2).Back(YtSorcerer(), 3);
                    break;
                case 3: // 四名方士：法術無視防禦，要先殺後排
                    k.Front(YtBrute(), 2).Back(YtSorcerer(), 0).Back(YtSorcerer(), 1).Back(YtSorcerer(), 3).Back(YtSorcerer(), 4);
                    break;
                case 4: // 擊殺指定：施法的祭酒
                    k.Target(YtPriest(), 2, 0).Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3);
                    break;
                case 5: // Boss 張寶（法術）
                    k.Back(ZhangBao(), 2).Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtSorcerer(), 4);
                    break;
                case 6: // 守城：左下角的糧車
                    k.Protect(HeroRoster.Npc("npc_supply", "糧車", 630), 0, 4, 8, Objective.Defend)
                     .Front(YtSoldier(), 0).Front(YtSoldier(), 1).Front(YtCaptain(), 2).Near(YtSoldier(), 0).Back(YtArcher(), 1);
                    break;
                case 7: // 限時攻城
                    k.Limit(12).Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtArcher(), 0).Back(YtSorcerer(), 4);
                    break;
                case 8: // 擊殺指定：張梁（防禦高）
                    k.Target(ZhangLiang(), 2, 1).Front(YtSoldier(), 1).Front(YtSoldier(), 3).Back(YtArcher(), 0);
                    break;
                case 9:
                    k.Front(YtSoldier(), 1).Front(YtCaptain(), 2).Front(YtSoldier(), 3).Back(YtSorcerer(), 0).Back(YtArcher(), 4);
                    break;
                case 10: // Boss 張角：法術蓄力，半血後每回合都蓄力
                    k.Back(ZhangJiao(), 2).Front(YtSoldier(), 1).Front(YtBrute(), 2).Front(YtSoldier(), 3).Back(YtSorcerer(), 4);
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(level));
            }
            return k.Setup;
        }
    }
}
