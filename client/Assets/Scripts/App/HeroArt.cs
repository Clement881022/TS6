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
