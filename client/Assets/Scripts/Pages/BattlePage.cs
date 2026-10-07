#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 戰鬥頁：prefab 載入時才建立 3D 戰場（BattleStage）與戰鬥 UI，切走時整個 prefab 銷毀、戰場一起消失。
    /// 戰鬥規則與畫面邏輯在 BattleScreen。
    /// </summary>
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
