#nullable enable
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
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
            var frame = new VisualElement().WithClass("dungeon-frame");
            frame.Add(HomeArtwork.ChallengeTabs(Page.Dungeons));
            body.Add(frame);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("dungeon-scroll");
            var row = scroll.contentContainer;
            row.AddToClassList("dun-row");
            frame.Add(scroll);

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
                head.Add(UiKit.Text(Equipment.UsesShards(d.Tier)
                    ? $"{Equipment.TierLabel(d.Tier)}碎片 · {Equipment.ShardCostOf(d.Tier)}片合成"
                    : $"{Equipment.TierLabel(d.Tier)}裝備 · 掉率 {Equipment.DropChanceOf(d.Tier) * 100:0}%", "dun-sub"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("dun-art");
                var rewards = new VisualElement().WithClass("dun-reward");
                rewards.Add(Reward("item_yuanbao", "元寶", d.Reward.Yuanbao));
                rewards.Add(Reward("item_gold", "銅錢", d.Reward.Gold));
                rewards.Add(Reward("item_expbook", "武將經驗", d.Reward.Materials[HeroGrowth.HeroExp]));
                art.Add(rewards);
                card.Add(art);

                var body2 = new VisualElement { pickingMode = PickingMode.Ignore };
                body2.AddToClassList("card-body");
                var prev = DemoMeta.FindDungeon(d.UnlockStageId);
                if (!unlocked) body2.Add(UiKit.Text(prev != null ? $"需通關第{"零一二三四五"[prev.Tier]}階"
                    : Campaign.TryParse(d.UnlockStageId, out int chapter, out int unlockLevel)
                    ? $"需通關 {chapter}-{unlockLevel}" : "需推進主線", "dun-lock"));
                else body2.Add(UiKit.Text(cleared ? "可掃蕩" : "", "line-sub").WithClass("dun-center"));
                card.Add(body2);

                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                if (!unlocked) btns.Add(UiKit.DoneBtn("尚未解鎖"));
                else
                {
                    btns.Add(UiKit.Btn("戰鬥", () => EnterDungeon(dungeon.Id), primary: true).WithBattleCost(dungeon.Id));
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

        private static VisualElement Reward(string icon, string name, int amount)
        {
            var reward = new VisualElement().WithClass("dun-resource");
            reward.Add(UiKit.ItemTile(icon, amount.ToString()));
            reward.Add(UiKit.Text(name, "dun-resource-name"));
            return reward;
        }
    }
}
