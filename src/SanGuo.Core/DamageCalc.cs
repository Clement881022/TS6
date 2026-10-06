using System;

namespace SanGuo.Core
{
    public static class DamageCalc
    {
        /// <summary>
        /// 乘法減傷（類 LoL）：攻擊 × 倍率 × 100 / (100 + 防禦)，再套用爆擊。至少 1 點。
        /// 防禦傳入有效防禦（已套用破甲 / 防禦 buff）。
        /// </summary>
        public static int Compute(int atk, double multiplier, double def, bool crit, int critDmg)
        {
            double d = atk * multiplier * 100.0 / (100.0 + Math.Max(0, def));
            if (crit) d *= critDmg / 100.0;
            return Math.Max(1, (int)Math.Round(d, MidpointRounding.AwayFromZero));
        }

        /// <summary>治療 / 護甲等非傷害的數值：攻擊 × 倍率。</summary>
        public static int Scale(int atk, double multiplier)
        {
            return Math.Max(0, (int)Math.Round(atk * multiplier, MidpointRounding.AwayFromZero));
        }
    }
}
