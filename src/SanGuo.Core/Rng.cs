namespace SanGuo.Core
{
    public sealed class Rng
    {
        private ulong _state;

        public Rng(ulong seed)
        {
            _state = seed;
        }

        private ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public bool Roll(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return Next(100) < percent;
        }
    }
}
