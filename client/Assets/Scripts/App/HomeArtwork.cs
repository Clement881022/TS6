#nullable enable
using UnityEngine;
using UnityEngine.UIElements;
namespace SanGuo.Client
{
    public static class HomeArtwork
    {
        private static readonly Rect[] Bounds = {
            new Rect(28, 525, 476, 474),
            new Rect(550, 534, 447, 429),
            new Rect(1067, 578, 439, 366),
            new Rect(28, 87, 461, 405),
            new Rect(560, 51, 372, 438),
            new Rect(1024, 66, 502, 436),
        };
        private static readonly Sprite?[] Sprites = new Sprite?[6];
        public static VisualElement Icon(int index)
        {
            var art = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-fn-icon");
            var texture = Resources.Load<Texture2D>("HomeArt/Navigation");
            if (texture != null)
            {
                Sprites[index] ??= Sprite.Create(texture, Bounds[index], new Vector2(.5f, .5f));
                art.style.backgroundImage = new StyleBackground(Sprites[index]);
            }
            return art;
        }
        public static VisualElement ChallengeTabs(Page active)
        {
            var tabs = new VisualElement().WithClass("challenge-tabs");
            foreach (var item in new[] { (Page.Dungeons, "素材副本"), (Page.WorldBoss, "世界 Boss") })
            {
                var target = item.Item1;
                var button = new Button(() => Nav.Go(target)) { text = item.Item2 };
                button.AddToClassList("challenge-tab");
                if (target == active) button.AddToClassList("active");
                button.name = "challenge-" + target;
                tabs.Add(button);
            }
            return tabs;
        }
    }
}
