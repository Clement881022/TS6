#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public static class PortraitArt
    {
        private static readonly Dictionary<string, Vector3> Frames = new Dictionary<string, Vector3>
        {
            { "guanyu", new Vector3(0.590f, 0.340f, 0.400f) },
            { "liubei", new Vector3(0.510f, 0.301f, 0.400f) },
            { "zhangfei", new Vector3(0.590f, 0.395f, 0.410f) },
            { "lvbu", new Vector3(0.560f, 0.495f, 0.420f) },
            { "xiahoudun", new Vector3(0.490f, 0.225f, 0.420f) },
            { "gongsunzan", new Vector3(0.400f, 0.370f, 0.390f) },
            { "zhaoyun", new Vector3(0.480f, 0.345f, 0.410f) },
            { "huangzhong", new Vector3(0.580f, 0.398f, 0.400f) },
            { "pangtong", new Vector3(0.610f, 0.359f, 0.420f) },
            { "zhugeliang", new Vector3(0.480f, 0.372f, 0.420f) },
            { "zhangjiao", new Vector3(0.520f, 0.305f, 0.420f) },
            { "xunyu", new Vector3(0.520f, 0.330f, 0.460f) },
            { "huatuo", new Vector3(0.500f, 0.341f, 0.430f) },
            { "zhoucang", new Vector3(0.572f, 0.465f, 0.650f) },
            { "huangfusong", new Vector3(0.612f, 0.517f, 0.620f) },
            { "huaxiong", new Vector3(0.479f, 0.499f, 0.630f) },
            { "zhujun", new Vector3(0.520f, 0.530f, 0.630f) },
            { "handang", new Vector3(0.492f, 0.487f, 0.600f) },
            { "zoujing", new Vector3(0.576f, 0.445f, 0.600f) },
            { "zhangbao", new Vector3(0.485f, 0.491f, 0.650f) },
            { "yuji", new Vector3(0.490f, 0.450f, 0.650f) },
            { "jianyong", new Vector3(0.512f, 0.325f, 0.650f) },
            { "luzhi", new Vector3(0.550f, 0.310f, 0.650f) },
            { "zhangzhongjing", new Vector3(0.530f, 0.343f, 0.650f) },
            { "ganfuren", new Vector3(0.528f, 0.354f, 0.630f) },
            { "r_sword", new Vector3(0.550f, 0.470f, 0.630f) },
            { "r_shield", new Vector3(0.540f, 0.460f, 0.630f) },
            { "r_archer", new Vector3(0.460f, 0.470f, 0.630f) },
            { "r_mage", new Vector3(0.540f, 0.430f, 0.630f) },
            { "r_healer", new Vector3(0.530f, 0.400f, 0.630f) },
            { "r_strategist", new Vector3(0.550f, 0.340f, 0.630f) },
        };

        public static VisualElement Create(string id, string cls, bool bust = false)
        {
            if (id == "ur_guanyu") id = "guanyu";
            if (id == "ur_zhangfei") id = "zhangfei";
            var frame = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass(cls + " portrait-frame");
            var texture = HeroArt.Full(id);
            if (texture == null || !Frames.TryGetValue(id, out var focus))
            {
                var fallback = bust ? HeroArt.Bust(id) ?? HeroArt.Face(id) : HeroArt.Face(id);
                if (fallback != null) frame.style.backgroundImage = new StyleBackground(fallback);
                return frame;
            }
            var image = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("portrait-source");
            image.style.backgroundImage = new StyleBackground(texture);
            frame.Add(image);
            frame.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float width = frame.contentRect.width, height = frame.contentRect.height;
                if (width <= 0 || height <= 0) return;
                float scale = width / (texture.width * focus.z * (bust ? 1.5f : 1f));
                image.style.width = texture.width * scale;
                image.style.height = texture.height * scale;
                image.style.left = width * .5f - focus.x * texture.width * scale;
                image.style.top = height * .5f - focus.y * texture.height * scale;
            });
            return frame;
        }
    }
}
