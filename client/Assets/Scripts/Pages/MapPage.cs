#nullable enable
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>征戰：整張章節地圖，關卡是圓形節點（星數、鎖頭、名牌），點下去開關卡面板（獎勵、敵人、挑戰 / 掃蕩）。</summary>
    public sealed class MapPage : PageBase
    {
        protected override Page Id => Page.Map;
        protected override string Title => "第零章　涿縣盜匪";

        protected override void OnReady()
        {
            if (!GameSession.OpenSelectedStageOnMap) return;
            GameSession.OpenSelectedStageOnMap = false;
            int level = Mathf.Clamp(GameSession.SelectedLevel, 1, DemoContent.ChapterLevelCount);
            if (GameSession.IsUnlocked(level)) OpenStageDetail(level);
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            int total = DemoContent.LevelNames.Length;
            body.style.flexDirection = FlexDirection.Column;

            var area = new VisualElement();
            area.AddToClassList("map-area");
            body.Add(area);

            // 路徑小圓點
            var centers = new Vector2[total];
            for (int i = 0; i < total; i++)
                centers[i] = new Vector2(7f + i * (86f / (total - 1)), 50f + Mathf.Sin(i * 0.9f) * 17f);
            for (int i = 0; i < total - 1; i++)
            {
                for (int k = 1; k <= 4; k++)
                {
                    var p = Vector2.Lerp(centers[i], centers[i + 1], k / 5f);
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("mnode-dot");
                    dot.style.left = Length.Percent(p.x);
                    dot.style.top = Length.Percent(p.y);
                    area.Add(dot);
                }
            }

            int cleared = 0;
            for (int i = 0; i < total; i++)
            {
                int level = i + 1;
                bool implemented = level <= DemoContent.ChapterLevelCount;
                string sid = GameSession.StageIdOf(level);
                int stars = v.StarsOf(sid);
                bool isCleared = v.ClearedStages.Contains(sid);
                if (isCleared) cleared++;
                bool open = implemented && GameSession.IsUnlocked(level);
                bool boss = level == total;

                var holder = new VisualElement { pickingMode = PickingMode.Ignore };
                holder.AddToClassList("mnode-holder");
                holder.style.left = Length.Percent(centers[i].x);
                holder.style.top = Length.Percent(centers[i].y);

                int lv = level;
                var node = new Button(() => { if (open) OpenStageDetail(lv); });
                node.AddToClassList("mnode");
                node.AddToClassList(isCleared ? "mnode-clear" : open ? "mnode-open" : "mnode-lock");
                if (boss) node.AddToClassList("mnode-boss");
                if (open && !isCleared) node.AddToClassList("mnode-current");
                if (open) node.Add(new Label(level.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("mnode-num"));
                else node.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("mnode-lockicon"));
                holder.Add(node);

                if (isCleared) holder.Add(UiKit.StarsRow(stars, 3, "mnode-stars"));
                var plate = new Label(DemoContent.LevelNames[i]) { pickingMode = PickingMode.Ignore };
                plate.AddToClassList("mnode-plate");
                if (!open) plate.AddToClassList("mnode-plate-dim");
                holder.Add(plate);
                area.Add(holder);
            }

            // 底部：章節進度條
            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.AddToClassList("map-info");
            info.Add(UiKit.Text($"章節進度  {cleared}/{DemoContent.ChapterLevelCount}", "txt-gold"));
            info.Add(UiKit.Bar(100f * cleared / DemoContent.ChapterLevelCount, "bar-gold bar-slim"));
            body.Add(info);
        }

        /// <summary>截圖 / 除錯用：直接開啟關卡面板。</summary>
        public void DebugOpenStage(int level) => OpenStageDetail(level);

        // ---- 關卡面板（獎勵 / 敵人 / 挑戰 / 掃蕩）----

        private void OpenStageDetail(int level)
        {
            var v = GameSession.View;
            var stage = DemoMeta.Stage(0, level);
            int stars = v.StarsOf(stage.StageId);
            bool first = !v.ClearedStages.Contains(stage.StageId);
            var setup = DemoContent.Level(level, 1);

            var overlay = new VisualElement();
            overlay.AddToClassList("overlay");
            // 點面板外的暗處也能關閉
            overlay.RegisterCallback<ClickEvent>(e => { if (e.target == overlay) overlay.RemoveFromHierarchy(); });
            var panel = new VisualElement();
            panel.AddToClassList("bpanel");
            panel.AddToClassList("stage-panel");
            panel.Add(new Button(() => overlay.RemoveFromHierarchy()) { text = "×", tooltip = "關閉關卡詳情" }.WithClass("popup-close"));

            var head = new VisualElement();
            head.AddToClassList("stage-head");
            head.Add(UiKit.Text($"{level}　{DemoContent.LevelNames[level - 1]}", "stage-title"));
            head.Add(UiKit.StarsRow(stars, 3, "stars-lg"));
            panel.Add(head);

            var cols = new VisualElement();
            cols.AddToClassList("stage-cols");

            var rewards = new VisualElement();
            rewards.AddToClassList("stage-col");
            rewards.Add(UiKit.Section(first ? "首通獎勵" : "通關獎勵"));
            var tiles = new VisualElement();
            tiles.AddToClassList("stage-tiles");
            tiles.Add(UiKit.ItemTile("item_expbook", stage.Exp.ToString()));
            tiles.Add(UiKit.ItemTile("item_gold", stage.Gold.ToString()));
            if (first && stage.FirstClearYuanbao > 0) tiles.Add(UiKit.ItemTile("item_yuanbao", stage.FirstClearYuanbao.ToString(), "item-first"));
            rewards.Add(tiles);
            rewards.Add(StarCondition(1, "通關", stars));
            rewards.Add(StarCondition(2, "無武將陣亡", stars));
            if (stage.StarTurnPar > 0) rewards.Add(StarCondition(3, $"{stage.StarTurnPar} 回合內通關", stars));
            cols.Add(rewards);

            var foes = new VisualElement();
            foes.AddToClassList("stage-col");
            foes.Add(UiKit.Section("敵方"));
            var names = setup.Enemies.GroupBy(e => e.Def.Name).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);
            foreach (var n in names) foes.Add(UiKit.Text("● " + n, "line-title"));
            cols.Add(foes);
            panel.Add(cols);

            var cost = UiKit.Cost("item_stamina", stage.StaminaCost, v.Stamina);
            cost.AddToClassList("stage-cost");
            panel.Add(cost);

            // 左：掃蕩（三星後才有）；右：戰鬥。關閉靠右上角 ✕ 或點暗處。
            var row = new VisualElement();
            row.AddToClassList("stage-buttons");
            var sweeps = new VisualElement();
            sweeps.AddToClassList("stage-sweeps");
            if (stars >= 3)
            {
                sweeps.Add(UiKit.Btn("掃蕩 ×1", () => _ = Sweep(level, 1)).WithClass("btn-sm"));
                sweeps.Add(UiKit.Btn($"掃蕩 ×{PlayerProfile.MaxSweepCount}", () => _ = Sweep(level, PlayerProfile.MaxSweepCount)).WithClass("btn-sm"));
            }
            row.Add(sweeps);
            row.Add(UiKit.Btn("戰鬥", () => EnterLevel(level), primary: true).WithClass("btn-lg"));
            panel.Add(row);

            overlay.Add(panel);
            Host.Add(overlay);
        }

        /// <summary>星級條件一行：已達成的打勾變綠。</summary>
        private static VisualElement StarCondition(int need, string text, int stars)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("star-cond");
            if (stars >= need) row.AddToClassList("star-cond-on");
            row.Add(new Label(new string('★', need)) { pickingMode = PickingMode.Ignore }.WithClass("star-cond-stars"));
            row.Add(new Label((stars >= need ? "● " : "") + text) { pickingMode = PickingMode.Ignore }.WithClass("star-cond-text"));
            return row;
        }

        private Task Sweep(int level, int count) => Act(
            async () => await GameSession.Backend.Sweep(GameSession.StageIdOf(level), count),
            describe: r =>
            {
                var s = (SweepOutcome)r;
                return $"掃蕩 ×{count}：經驗 +{s.Exp}　金幣 +{s.Gold}" + (s.LevelsGained > 0 ? $"　升 {s.LevelsGained} 級！" : "");
            });
    }
}
