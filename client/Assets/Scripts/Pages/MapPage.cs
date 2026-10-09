#nullable enable
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>征戰：一章一張地圖（底部可切換章節），關卡是圓形節點（星數、鎖頭、名牌），點下去開關卡面板（獎勵、敵人、挑戰 / 掃蕩）。</summary>
    public sealed class MapPage : PageBase
    {
        protected override Page Id => Page.Map;
        protected override string Title => (GameSession.HardMode ? "困難　" : "") + Campaign.Title(Chapter);

        private static int Chapter => GameSession.SelectedChapter;

        protected override void OnReady()
        {
            // 第一次進地圖：跳到目前該打的那一章。
            if (!GameSession.StageChosen) { GameSession.EnsureSelection(); Rebuild(); }
            if (!GameSession.OpenSelectedStageOnMap) return;
            GameSession.OpenSelectedStageOnMap = false;
            if (GameSession.IsUnlocked(Chapter, GameSession.SelectedLevel)) OpenStageDetail(GameSession.SelectedLevel);
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var levelNames = Campaign.LevelNames(Chapter);
            int total = levelNames.Length;
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
                string sid = GameSession.StageIdOf(Chapter, level);
                int stars = v.StarsOf(sid);
                bool isCleared = v.ClearedStages.Contains(sid);
                if (isCleared) cleared++;
                bool open = GameSession.IsUnlocked(Chapter, level);
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
                var plate = new Label(levelNames[i]) { pickingMode = PickingMode.Ignore };
                plate.AddToClassList("mnode-plate");
                if (!open) plate.AddToClassList("mnode-plate-dim");
                holder.Add(plate);
                area.Add(holder);
            }

            // 底部：章節切換與進度條（下一章要等該章第一關開放）
            var info = new VisualElement();
            info.AddToClassList("map-info");
            info.style.flexDirection = FlexDirection.Row;
            info.style.alignItems = Align.Center;
            int prev = Chapter - 1, next = Chapter + 1;
            var prevBtn = UiKit.Btn("◀ 上一章", () => SwitchChapter(prev)).WithClass("btn-sm");
            prevBtn.SetEnabled(prev >= (GameSession.HardMode ? HardStages.FirstChapter : Campaign.FirstChapter));
            info.Add(prevBtn);
            var progress = new VisualElement { pickingMode = PickingMode.Ignore };
            progress.style.flexGrow = 1;
            progress.style.marginLeft = progress.style.marginRight = 12;
            progress.Add(UiKit.Text($"章節進度  {cleared}/{total}", "txt-gold"));
            progress.Add(UiKit.Bar(100f * cleared / total, "bar-gold bar-slim"));
            info.Add(progress);
            var nextBtn = UiKit.Btn("下一章 ▶", () => SwitchChapter(next)).WithClass("btn-sm");
            nextBtn.SetEnabled(next <= Campaign.LastChapter && GameSession.IsUnlocked(next, 1));
            info.Add(nextBtn);
            // 普通／困難切換（主線全通後開放；困難主線從第 1 章開始）
            if (GameSession.HardModeOpen || GameSession.HardMode)
            {
                var mode = UiKit.Btn(GameSession.HardMode ? "切換：普通" : "切換：困難", ToggleHard).WithClass("btn-sm");
                mode.style.marginLeft = 12;
                info.Add(mode);
            }
            body.Add(info);
        }

        private void ToggleHard()
        {
            GameSession.HardMode = !GameSession.HardMode;
            if (GameSession.HardMode)
            {
                // 跳到第一個還沒打過的困難關
                for (int c = HardStages.FirstChapter; c <= Campaign.LastChapter; c++)
                    for (int l = 1; l <= Campaign.LevelsPerChapter; l++)
                        if (!GameSession.View.ClearedStages.Contains(HardStages.StageId(c, l))) { GameSession.Select(c, l); Rebuild(); return; }
                GameSession.Select(Campaign.LastChapter, Campaign.LevelsPerChapter);
            }
            else
            {
                var (fc, fl) = GameSession.Frontier();
                GameSession.Select(fc, fl);
            }
            Rebuild();
        }

        private void SwitchChapter(int chapter)
        {
            if (chapter < Campaign.FirstChapter || chapter > Campaign.LastChapter || !GameSession.IsUnlocked(chapter, 1)) return;
            if (GameSession.HardMode) { GameSession.Select(chapter, 1); Rebuild(); return; }
            // 切到目前該打的那一章時停在該關，其餘停在第 1 關。
            var (fc, fl) = GameSession.Frontier();
            GameSession.Select(chapter, chapter == fc ? fl : 1);
            Rebuild();
        }

        /// <summary>截圖 / 除錯用：直接開啟關卡面板。</summary>
        public void DebugOpenStage(int level) => OpenStageDetail(level);

        // ---- 關卡面板（獎勵 / 敵人 / 挑戰 / 掃蕩）----

        private void OpenStageDetail(int level)
        {
            var v = GameSession.View;
            int chapter = Chapter;
            var stage = DemoMeta.FindStage(GameSession.StageIdOf(chapter, level))!;
            int stars = v.StarsOf(stage.StageId);
            bool first = !v.ClearedStages.Contains(stage.StageId);
            var setup = GameSession.EnemyPreview(stage.StageId) ?? Campaign.Setup(chapter, level, 1);

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
            head.Add(UiKit.Text($"{(GameSession.HardMode ? "困難 " : "")}{chapter}-{level}　{Campaign.LevelName(chapter, level)}", "stage-title"));
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
            foreach (var m in stage.Materials)
            {
                int n = m.Value + (first && stage.FirstClearMaterials.TryGetValue(m.Key, out int extra) ? extra : 0);
                tiles.Add(UiKit.ItemTile(m.Key == HeroGrowth.HeroExp ? "item_expbook" : "item_shard", n.ToString())); // 與獎勵列一致：將魂用碎片圖示
            }
            rewards.Add(tiles);
            rewards.Add(StarCondition(1, "通關", stars));
            rewards.Add(StarCondition(2, "無武將陣亡", stars));
            if (stage.StarTurnPar > 0) rewards.Add(StarCondition(3, $"{stage.StarTurnPar} 回合內通關", stars));
            cols.Add(rewards);

            var foes = new VisualElement();
            foes.AddToClassList("stage-col");
            foes.Add(UiKit.Section("敵方"));
            var objective = ObjectiveText(setup);
            if (objective != null) foes.Add(UiKit.Text(objective, "txt-gold"));
            if (GameSession.HardMode && HardStages.BannedRole(chapter, level) is Role banned)
                foes.Add(UiKit.Text($"條件：禁用{CardText.RoleName(banned)}", "txt-gold"));
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
                sweeps.Add(UiKit.Btn("掃蕩 ×1", () => _ = Sweep(chapter, level, 1)).WithClass("btn-sm"));
                sweeps.Add(UiKit.Btn($"掃蕩 ×{PlayerProfile.MaxSweepCount}", () => _ = Sweep(chapter, level, PlayerProfile.MaxSweepCount)).WithClass("btn-sm"));
            }
            row.Add(sweeps);
            row.Add(UiKit.Btn("戰鬥", () => EnterLevel(chapter, level), primary: true).WithClass("btn-lg"));
            panel.Add(row);

            overlay.Add(panel);
            Host.Add(overlay);
        }

        /// <summary>關卡目標（全滅以外才顯示）：護送 / 守城 / 擊殺指定 / 限時。</summary>
        private static string? ObjectiveText(BattleSetup setup)
        {
            string? text = setup.Objective switch
            {
                Objective.Escort => $"目標：護送{ProtectedName(setup)}撐過 {setup.SurviveTurns} 回合",
                Objective.Defend => $"目標：守住{ProtectedName(setup)} {setup.SurviveTurns} 回合",
                Objective.KillTarget => "目標：擊殺 " + string.Join("、", setup.Enemies.Where(e => e.IsObjective).Select(e => e.Def.Name).Distinct()),
                _ => null,
            };
            if (setup.TurnLimit > 0) text = (text ?? "目標：全滅敵人") + $"（限 {setup.TurnLimit} 回合）";
            return text;
        }

        private static string ProtectedName(BattleSetup setup) => setup.Heroes.FirstOrDefault(h => h.IsProtected)?.Def.Name ?? "目標";

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

        private Task Sweep(int chapter, int level, int count) => Act(
            async () => await GameSession.Backend.Sweep(GameSession.StageIdOf(chapter, level), count),
            describe: r =>
            {
                var s = (SweepOutcome)r;
                return $"掃蕩 ×{count}：經驗 +{s.Exp}　金幣 +{s.Gold}" + (s.LevelsGained > 0 ? $"　升 {s.LevelsGained} 級！" : "");
            });
    }
}
