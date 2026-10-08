#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SanGuo.Client
{
    /// <summary>武將美術（取自 TS6Client）：Resources/HeroArt/face_&lt;武將id&gt;（頭像）與 full_&lt;武將id&gt;（全身立繪）。沒有圖的武將回傳 null，畫面改用姓氏圓章。</summary>
    public static class HeroArt
    {
        private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D?>();

        public static Texture2D? Face(string heroId) => Load("HeroArt/face_" + heroId);

        public static Texture2D? Full(string heroId) => Load("HeroArt/full_" + heroId);

        /// <summary>
        /// 角色模型：先找 Resources/Characters/&lt;id&gt;；沒有專屬模型的武將依職業借用通用模型（正式美術到位前的過渡），
        /// 敵人則借用山賊嘍囉。
        /// </summary>
        public static GameObject? Model(string defId)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + defId);
            if (prefab != null) return prefab;
            var hero = SanGuo.Core.HeroRoster.Find(defId);
            string fallback = hero == null ? "bandit_grunt" : FallbackModel(hero.Role);
            return Resources.Load<GameObject>("Characters/" + fallback);
        }

        private static string FallbackModel(SanGuo.Core.Role role)
        {
            switch (role)
            {
                case SanGuo.Core.Role.Tank: return "r_shield";
                case SanGuo.Core.Role.Warrior: return "r_militia";
                case SanGuo.Core.Role.Ranger: return "r_archer";
                case SanGuo.Core.Role.Mage: return "pangtong";
                case SanGuo.Core.Role.Strategist: return "zhugeliang";
                default: return "r_healer";
            }
        }

        private static Texture2D? Load(string path)
        {
            if (!Cache.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                Cache[path] = tex;
            }
            return tex;
        }
    }
}
