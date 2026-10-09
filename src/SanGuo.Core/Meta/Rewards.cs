using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public sealed class Reward
    {
        public int Yuanbao;
        public int Gold;
        public int Stamina;
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public List<string> Heroes = new List<string>();

        public Reward() { }

        public Reward(int yuanbao = 0, int gold = 0, int stamina = 0)
        {
            Yuanbao = yuanbao;
            Gold = gold;
            Stamina = stamina;
        }

        public Reward With(string material, int amount)
        {
            Materials[material] = Materials.TryGetValue(material, out int n) ? n + amount : amount;
            return this;
        }

        public Reward WithHero(string heroId)
        {
            Heroes.Add(heroId);
            return this;
        }

        public Reward Times(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var r = new Reward(Yuanbao * count, Gold * count, Stamina * count);
            foreach (var m in Materials) r.Materials[m.Key] = m.Value * count;
            return r;
        }
    }
}
