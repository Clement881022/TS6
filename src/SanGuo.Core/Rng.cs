namespace SanGuo.Core
{
    /// <summary>
    /// 帶種子的 SplitMix64 隨機數，自行實作以確保 Unity 客戶端與 .NET 伺服器結果完全一致
    /// （System.Random 不保證跨平台 / 版本一致）。
    /// </summary>
    public sealed class Rng
    {
        private ulong _state;

        public Rng(ulong seed)
        {
            _state = seed;
        }

        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>回傳 [0, maxExclusive)。</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>以百分比機率擲骰（0–100）。</summary>
        public bool Roll(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return Next(100) < percent;
        }
    }
}
