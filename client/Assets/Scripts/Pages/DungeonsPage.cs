#nullable enable
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>素材副本：五階，與裝備品階 1–5 對應；沒有每日次數限制，通關一次後可掃蕩；各階隨主線進度解鎖。</summary>
    public sealed class DungeonsPage : PageBase
    {
        private readonly List<ResourceDungeonDef> _dungeons = DemoResourceDungeons.Create();

        protected override Page Id => Page.Dungeons;
        protected override string Title => "素材副本";

        public void DebugScrollEnd()
        {
            var scroll = Host.Q<ScrollView>();
            if (scroll != null) scroll.verticalScroller.value = scroll.verticalScroller.highValue;
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            body.style.flexDirection = FlexDirection.Column;
            body.AddToClassList("page-centered");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("dungeon-scroll");
            var row = scroll.contentContainer;
            row.AddToClassList("dun-row");
            body.Add(scroll);

            foreach (var d in _dungeons)
            {
                var dungeon = d;
                bool unlocked = ResourceDungeons.IsUnlocked(v.Raw, d);
                bool cleared = v.ClearedStages.Contains(d.Id);
                bool enough = v.Stamina >= d.StaminaCost;

                var card = new VisualElement();
                card.AddToClassList("dun-card");
                if (!unlocked) card.AddToClassList("dun-closed");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("dun-head");
                head.Add(UiKit.Text(d.Name, "dun-name"));
                head.Add(UiKit.Text($"產出 {d.Tier} 階裝備", "dun-sub"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("dun-art");
                art.Add(UiKit.RewardTiles(d.Reward).WithClass("dun-reward"));
                card.Add(art);

                var body2 = new VisualElement { pickingMode = PickingMode.Ignore };
                body2.AddToClassList("card-body");
                var prev = DemoMeta.FindDungeon(d.UnlockStageId);
                if (!unlocked) body2.Add(UiKit.Text(prev != null ? $"打贏「{prev.Name}」後解鎖"
                    : Campaign.TryParse(d.UnlockStageId, out int chapter, out int unlockLevel)
                    ? $"通關第 {chapter} 章第 {unlockLevel} 關後解鎖" : "推進主線後解鎖", "dun-lock"));
                else body2.Add(UiKit.Text(cleared ? "已通關，可掃蕩" : "尚未通關", "line-sub").WithClass("dun-center"));
                body2.Add(UiKit.Text($"本階裝備掉率 {Equipment.DropChanceOf(d.Tier) * 100:0}%", "line-sub").WithClass("dun-center"));
                var cost = UiKit.Cost("item_stamina", d.StaminaCost, v.Stamina);
                cost.AddToClassList("dun-cost");
                body2.Add(cost);
                card.Add(body2);

                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                if (!unlocked) btns.Add(UiKit.DoneBtn("尚未解鎖"));
                else
                {
                    btns.Add(UiKit.Btn("挑戰", () => EnterDungeon(dungeon.Id), primary: true));
                    if (cleared)
                    {
                        btns.Add(UiKit.Btn("掃蕩 ×1", () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, 1), "掃蕩完成")).WithClass("btn-sm"));
                        int many = System.Math.Max(1, System.Math.Min(ResourceDungeons.MaxSweepCount, v.Stamina / d.StaminaCost));
                        if (many > 1)
                            btns.Add(UiKit.Btn($"掃蕩 ×{many}", () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, many), "掃蕩完成")).WithClass("btn-sm"));
                    }
                    if (!enough) btns.Add(UiKit.Text("體力不足", "line-sub").WithClass("dun-center"));
                }
                card.Add(btns);
                row.Add(card);
            }
        }
    }
}
