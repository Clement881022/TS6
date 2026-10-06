using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>一份獎勵（任務、副本、七日大獎共用）。</summary>
    public sealed class Reward
    {
        public int Yuanbao;
        public int Gold;
        public int Stamina;
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        /// <summary>直接贈送的武將；已擁有就轉成突破碎片（與抽到重複武將相同）。</summary>
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

        /// <summary>乘上倍數（掃蕩多次用）；武將不乘。</summary>
        public Reward Times(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var r = new Reward(Yuanbao * count, Gold * count, Stamina * count);
            foreach (var m in Materials) r.Materials[m.Key] = m.Value * count;
            return r;
        }
    }
}
