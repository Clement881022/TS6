using System;

namespace SanGuo.Core
{
    public static class DamageCalc
    {
        /// <summary>閃避率上限（GDD 待決事項的暫定值）。</summary>
        public const int DodgeCap = 50;
        public const int CritCap = 100;

        /// <summary>
        /// 物理傷害（乘法減傷）：攻擊 × 倍率 × 100 / (100 + 防禦)，爆擊再乘爆擊傷害。至少 1 點。
        /// 防禦傳入有效防禦（已套用防禦增益與破甲）。
        /// </summary>
        public static int Physical(int atk, double multiplier, double def, bool crit, int critDmg)
        {
            double d = atk * multiplier * 100.0 / (100.0 + Math.Max(0, def));
            if (crit) d *= critDmg / 100.0;
            return Math.Max(1, (int)Math.Round(d, MidpointRounding.AwayFromZero));
        }

        /// <summary>法術傷害：謀略 × 倍率，不受防禦影響、不爆擊。至少 1 點。</summary>
        public static int Magical(int power, double multiplier) =>
            Math.Max(1, (int)Math.Round(power * multiplier, MidpointRounding.AwayFromZero));

        /// <summary>治療 / 護盾 / 燃燒層數等非傷害數值：威力 × 倍率。</summary>
        public static int Scale(int power, double multiplier) =>
            Math.Max(0, (int)Math.Round(power * multiplier, MidpointRounding.AwayFromZero));
    }
}
