#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>征戰：章節大地圖與關卡資訊（挑戰 / 掃蕩）。</summary>
    public sealed class MapPage : PageBase
    {
        protected override Page Id => Page.Map;
        protected override string Title => "第一章　黃巾之亂";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            body.Add(UiKit.Hint("打贏一關才會開啟下一關；三星通關後可掃蕩"));

            var mapPanel = UiKit.Panel("map-card");
            var field = new VisualElement();
            field.AddToClassList("map-field");
            int total = DemoContent.LevelNames.Length;
            var centers = new List<Vector2>();
            for (int i = 0; i < total; i++)
                centers.Add(new Vector2(90 + i * 169f, 250 + Mathf.Sin(i * 0.9f) * 140f));

            for (int i = 0; i < total - 1; i++)
            {
                for (int k = 1; k <= 3; k++)
                {
                    var p = Vector2.Lerp(centers[i], centers[i + 1], k / 4f);
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("map-dot");
                    dot.style.left = p.x - 5; dot.style.top = p.y - 5;
                    field.Add(dot);
                }
            }

            for (int i = 0; i < total; i++)
            {
                int level = i + 1;
                bool implemented = level <= DemoContent.ChapterLevelCount;
                int stars = v.StarsOf(GameSession.StageIdOf(level));
                bool cleared = v.ClearedStages.Contains(GameSession.StageIdOf(level));
                bool open = implemented && GameSession.IsUnlocked(level);
                bool boss = level == total;
                float size = boss ? 118 : 92;

                var node = new Button(() => { if (open) OpenStageDetail(level); }) { text = level.ToString() };
                node.AddToClassList("map-node");
                node.AddToClassList(cleared ? "map-node-clear" : open ? "map-node-open" : "map-node-lock");
                if (boss) node.AddToClassList("map-node-boss");
                node.style.width = size; node.style.height = size;
                node.style.borderTopLeftRadius = size / 2; node.style.borderTopRightRadius = size / 2;
                node.style.borderBottomLeftRadius = size / 2; node.style.borderBottomRightRadius = size / 2;
                node.style.left = centers[i].x - size / 2;
                node.style.top = centers[i].y - size / 2;
                field.Add(node);

                string status = cleared ? UiText.Stars(stars) : !implemented ? "未開放" : open ? "可挑戰" : "未解鎖";
                var caption = new Label($"{DemoContent.LevelNames[i]}\n{status}") { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("map-caption");
                caption.style.left = centers[i].x - 80;
                caption.style.top = centers[i].y + size / 2 + 4;
                field.Add(caption);
            }
            mapPanel.Add(field);
            body.Add(mapPanel);
        }

        // ---- 關卡資訊（挑戰 / 掃蕩）----

        private void OpenStageDetail(int level)
        {
            var v = GameSession.View;
            var stage = DemoMeta.Chapter1Stage(level);
            int stars = v.StarsOf(stage.StageId);

            var overlay = new VisualElement();
            overlay.AddToClassList("overlay");
            var panel = UiKit.Panel("popup");
            panel.Add(UiKit.Text($"第 {level} 關　{DemoContent.LevelNames[level - 1]}", "popup-title"));
            panel.Add(UiKit.Text(UiText.Stars(stars), "star-text"));
            panel.Add(UiKit.Text($"消耗體力 {stage.StaminaCost}（現有 {v.Stamina}）", v.Stamina >= stage.StaminaCost ? "txt" : "txt-warn"));
            panel.Add(UiKit.Text($"獎勵：經驗 {stage.Exp}　金幣 {stage.Gold}" +
                (v.ClearedStages.Contains(stage.StageId) ? "" : $"　首通元寶 {stage.FirstClearYuanbao}"), "txt-gold"));
            string par = stage.StarTurnPar > 0 ? $"　★★★ {stage.StarTurnPar} 回合內" : "";
            panel.Add(UiKit.Hint("★ 通關　★★ 無武將陣亡" + par));

            var row = UiKit.Row("row-center");
            row.Add(UiKit.Btn("挑戰", () => EnterLevel(level), primary: true));
            if (stars >= 3)
            {
                row.Add(UiKit.Btn("掃蕩 ×1", () => _ = Sweep(level, 1)));
                row.Add(UiKit.Btn($"掃蕩 ×{PlayerProfile.MaxSweepCount}", () => _ = Sweep(level, PlayerProfile.MaxSweepCount)));
            }
            row.Add(UiKit.Btn("返回", () => overlay.RemoveFromHierarchy()));
            panel.Add(row);
            overlay.Add(panel);
            Host.Add(overlay);
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
