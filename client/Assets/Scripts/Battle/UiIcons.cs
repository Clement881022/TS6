#nullable enable
using System.Collections.Generic;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public static class UiIcons
    {
        private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D?>();

        public static Texture2D? Get(string name)
        {
            if (!Cache.TryGetValue(name, out var tex))
            {
                tex = Resources.Load<Texture2D>("ChibiSkin/" + name) ?? Resources.Load<Texture2D>("Icons/" + name);
                Cache[name] = tex;
            }
            return tex;
        }

        public static string Status(StatusType type)
        {
            switch (type)
            {
                case StatusType.IntUp: return "status_atkup";
                case StatusType.DodgeUp: return "status_defup";
                default: return "status_" + type.ToString().ToLowerInvariant();
            }
        }

        public static string RoleIcon(Role role)
        {
            switch (role)
            {
                case Role.Tank: return "role_tank";
                case Role.Warrior: return "role_warrior";
                case Role.Ranger: return "role_archer";
                case Role.Healer: return "role_healer";
                case Role.Strategist: return "role_strategist";
                default: return "role_mage";
            }
        }

        public static VisualElement Icon(string name, string cls = "icon")
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.AddToClassList(cls);
            var tex = Get(name);
            if (tex != null) el.style.backgroundImage = new StyleBackground(tex);
            return el;
        }

        public static VisualElement Chip(string icon, string text = "", string cls = "chip")
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList(cls);
            chip.Add(Icon(icon));
            if (text.Length > 0)
            {
                var l = new Label(text) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("chip-text");
                chip.Add(l);
            }
            return chip;
        }
    }
}
