#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SanGuo.Client
{
    public static class HeroArt
    {
        private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D?>();

        public static Texture2D? Face(string heroId) => Load("ChibiSkin/face_" + heroId);

        public static Texture2D? Full(string heroId) => Load("ChibiSkin/full_" + heroId);
        public static Texture2D? Bust(string heroId) => Load("ChibiSkin/bust_" + heroId);

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

        public static bool HasOwnModel(string defId) =>
            Resources.Load<GameObject>("ProductionCharacters/" + defId) != null || Resources.Load<GameObject>("ChibiModels/" + defId) != null;

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
