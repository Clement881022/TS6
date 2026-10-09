#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class BattlePage : PageBase
    {
        public BattleScreen? Screen { get; private set; }

        protected override Page Id => Page.Battle;
        protected override string Title => "戰鬥";
        protected override void BuildBody(VisualElement body) { }

        public override void Open(VisualElement container)
        {
            Screen = new BattleScreen(container, gameObject.AddComponent<BattleStage>());
        }
    }
}
