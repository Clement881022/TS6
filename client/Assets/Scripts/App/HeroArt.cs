#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SanGuo.Client
{
    /// <summary>Q 版武將美術：Resources/ChibiSkin/face_&lt;id&gt; 與 full_&lt;id&gt;。缺圖回傳 null，避免混入舊寫實立繪。</summary>
    public static class HeroArt
    {
        private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D?>();

        public static Texture2D? Face(string heroId) => Load("ChibiSkin/face_" + heroId);

        public static Texture2D? Full(string heroId) => Load("ChibiSkin/full_" + heroId);
        public static Texture2D? Bust(string heroId) => Load("ChibiSkin/bust_" + heroId);

        /// <summary>
        /// 原創 Q 版模型由 tools/blender/chibi_v2.py 匯出到 ChibiModels；
        /// 未登錄角色依職業借用同系列模型。
        /// </summary>
        public static GameObject? Model(string defId)
        {
            var refined = Resources.Load<GameObject>("ProductionCharacters/" + defId);
            if (refined != null) return refined;
            var prefab = Resources.Load<GameObject>("ChibiModels/" + defId);
            if (prefab != null) return prefab;
            var hero = SanGuo.Core.HeroRoster.Find(defId);
            string fallback = hero == null ? "bandit_grunt" : FallbackModel(hero.Role);
            return Resources.Load<GameObject>("ChibiModels/" + fallback);
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
