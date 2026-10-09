using System;

namespace SanGuo.Core
{
    public static class DamageCalc
    {
        public const int DodgeCap = 50;
        public const int CritCap = 100;

        public static int Physical(int atk, double multiplier, double def, bool crit, int critDmg)
        {
            double d = atk * multiplier * 100.0 / (100.0 + Math.Max(0, def));
            if (crit) d *= critDmg / 100.0;
            return Math.Max(1, (int)Math.Round(d, MidpointRounding.AwayFromZero));
        }

        public static int Magical(int power, double multiplier) =>
            Math.Max(1, (int)Math.Round(power * multiplier, MidpointRounding.AwayFromZero));

        public static int Scale(int power, double multiplier) =>
            Math.Max(0, (int)Math.Round(power * multiplier, MidpointRounding.AwayFromZero));
    }
}
