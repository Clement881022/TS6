using System;

namespace SanGuo.Core.Meta
{
    public sealed class StaminaClock
    {
        public int Cap = 120;
        public int RegenSeconds = 360;
        public int Current;
        public long Anchor;

        public StaminaClock() { }

        public StaminaClock(int cap, int regenSeconds, long now)
        {
            Cap = cap;
            RegenSeconds = regenSeconds;
            Current = cap;
            Anchor = now;
        }

        public int Get(long now)
        {
            Sync(now);
            return Current;
        }

        public bool TrySpend(int cost, long now)
        {
            Sync(now);
            if (cost < 0 || Current < cost) return false;
            if (Current >= Cap) Anchor = now;
            Current -= cost;
            return true;
        }

        public void SetCap(int cap, long now)
        {
            Sync(now);
            Cap = cap;
        }

        public void Add(int amount, long now)
        {
            Sync(now);
            if (Current < Cap && Current + amount >= Cap) Anchor = now;
            Current += amount;
        }

        public void RefillToCap(long now)
        {
            Sync(now);
            if (Current < Cap)
            {
                Current = Cap;
                Anchor = now;
            }
        }

        public long SecondsToNext(long now)
        {
            Sync(now);
            if (Current >= Cap) return 0;
            return Math.Max(0, RegenSeconds - (now - Anchor));
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
