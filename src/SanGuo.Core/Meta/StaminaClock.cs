using System;

namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 體力：每 <see cref="RegenSeconds"/> 秒回 1 點，回到上限後停止；禮包 / 升級補給可超過上限（不會自然衰減）。
    /// 時間一律由呼叫端傳入（Unix 秒），客戶端用本機時間預覽、伺服器以自己的時間為準。
    /// </summary>
    public sealed class StaminaClock
    {
        public int Cap = 120;
        public int RegenSeconds = 360;
        public int Current;
        /// <summary>下一點體力開始計時的時間點（Unix 秒）。</summary>
        public long Anchor;

        public StaminaClock() { }

        public StaminaClock(int cap, int regenSeconds, long now)
        {
            Cap = cap;
            RegenSeconds = regenSeconds;
            Current = cap;
            Anchor = now;
        }

        /// <summary>依時間流逝補回體力並回傳目前值。</summary>
        public int Get(long now)
        {
            Sync(now);
            return Current;
        }

        public bool TrySpend(int cost, long now)
        {
            Sync(now);
            if (cost < 0 || Current < cost) return false;
            // 從滿值開始消耗時，回復計時從現在起算。
            if (Current >= Cap) Anchor = now;
            Current -= cost;
            return true;
        }

        /// <summary>調整上限（升級時）：先結算已回復的體力，再套用新上限；目前體力不變。</summary>
        public void SetCap(int cap, long now)
        {
            Sync(now);
            Cap = cap;
        }

        /// <summary>發放體力（任務 / 免費補給 / 購買），可超過上限。</summary>
        public void Add(int amount, long now)
        {
            Sync(now);
            if (Current < Cap && Current + amount >= Cap) Anchor = now;
            Current += amount;
        }

        /// <summary>補到上限（升級回滿）；已高於上限則不變。</summary>
        public void RefillToCap(long now)
        {
            Sync(now);
            if (Current < Cap)
            {
                Current = Cap;
                Anchor = now;
            }
        }

        /// <summary>距離下一點體力的秒數；已滿則為 0。</summary>
        public long SecondsToNext(long now)
        {
            Sync(now);
            if (Current >= Cap) return 0;
            return Math.Max(0, RegenSeconds - (now - Anchor));
        }

        /// <summary>距離補滿的秒數；已滿則為 0。</summary>
        public long SecondsToFull(long now)
        {
            Sync(now);
            if (Current >= Cap) return 0;
            return SecondsToNext(now) + (long)(Cap - Current - 1) * RegenSeconds;
        }

        private void Sync(long now)
        {
            if (Current >= Cap)
            {
                Anchor = now;
                return;
            }
            if (now < Anchor)
            {
                // 時鐘倒退（改系統時間）：不給也不扣，重設計時起點。
                Anchor = now;
                return;
            }
            long gained = (now - Anchor) / RegenSeconds;
            if (gained <= 0) return;
            if (Current + gained >= Cap)
            {
                Current = Cap;
                Anchor = now;
            }
            else
            {
                Current += (int)gained;
                Anchor += gained * RegenSeconds;
            }
        }
    }
}
