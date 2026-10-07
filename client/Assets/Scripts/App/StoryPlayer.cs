#nullable enable
using System;
using System.Collections.Generic;
using SanGuo.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 劇情對白播放（視覺小說式）：沿用新手引導的版面，每句話可換說話者與立繪（有全身立繪用立繪，沒有就用 3D 模型）。
    /// 劇情資料在 <see cref="DemoStory"/>；看過與否由呼叫端決定（戰前＝關卡尚未通關、戰後＝首通）。
    /// 截圖模式（-sanguoShot）不彈，除非加 -sanguoShowTutorial。
    /// </summary>
    public static class StoryPlayer
    {
        private const long CharIntervalMs = 28;

        /// <summary>主線關卡戰前劇情：該關尚未通關才播，播完（或沒有劇情）呼叫 <paramref name="proceed"/>。</summary>
        public static void ShowBefore(VisualElement layer, int level, Action proceed)
        {
            string key = "story_before_" + level;
            bool cleared = GameSession.View.ClearedStages.Contains(GameSession.StageIdOf(level));
            if (cleared || Tutorial.Seen(key) || level < 1 || level > SanGuo.Core.DemoContent.ChapterLevelCount
                || !Show(layer, $"第 {level} 關　{SanGuo.Core.DemoContent.LevelNames[level - 1]}", DemoStory.Before(level), () => { Tutorial.MarkSeen(key); proceed(); }))
                proceed();
        }

        /// <summary>播放對白；播完或略過都呼叫 <paramref name="onDone"/>。沒有對白或截圖模式時直接回傳 false（不呼叫 onDone）。</summary>
        public static bool Show(VisualElement layer, string title, IReadOnlyList<StoryLine> lines, Action? onDone = null)
        {
            if (lines.Count == 0 || Tutorial.Seen("_story_suppress")) return false;

            var overlay = new VisualElement();
            overlay.AddToClassList("overlay");
            overlay.AddToClassList("tut-overlay");

            var stages = new Dictionary<string, ModelStage?>();
            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.Add(portrait);

            var nameplate = new VisualElement { pickingMode = PickingMode.Ignore };
            nameplate.AddToClassList("tut-nameplate");
            nameplate.Add(new VisualElement().WithClass("tut-diamond"));
            var nameLabel = new Label().WithClass("tut-name");
            nameplate.Add(nameLabel);
            nameplate.Add(new VisualElement().WithClass("tut-diamond"));
            overlay.Add(nameplate);

            var band = new VisualElement();
            band.AddToClassList("tut-band");
            band.Add(new Label(title).WithClass("tut-title"));
            var body = new Label().WithClass("tut-body");
            band.Add(body);
            var footer = new VisualElement().WithClass("tut-footer");
            var step = new Label().WithClass("tut-step");
            var action = new VisualElement().WithClass("tut-action");
            footer.Add(step);
            footer.Add(action);
            band.Add(footer);
            var next = new VisualElement().WithClass("tut-next");
            band.Add(next);
            overlay.Add(band);

            int index = 0;
            int shown = 0;
            bool closed = false;
            IVisualElementScheduledItem? typer = null;

            void Close()
            {
                if (closed) return;
                closed = true;
                typer?.Pause();
                foreach (var s in stages.Values) s?.Dispose();
                overlay.RemoveFromHierarchy();
                onDone?.Invoke();
            }

            var skip = UiKit.Btn("略過", Close);
            skip.AddToClassList("tut-skip");
            overlay.Add(skip);
            layer.Add(overlay);

            bool Typing() => shown < lines[index].Text.Length;

            void ShowPortrait(StoryLine line)
            {
                portrait.Clear();
                portrait.style.backgroundImage = StyleKeyword.None;
                portrait.RemoveFromClassList("tut-hero");
                portrait.RemoveFromClassList("tut-model");
                if (line.Portrait == "") return;
                var full = HeroArt.Full(line.Portrait);
                if (full != null)
                {
                    portrait.AddToClassList("tut-hero");
                    portrait.style.backgroundImage = new StyleBackground(full);
                    return;
                }
                if (!stages.TryGetValue(line.Portrait, out var model))
                {
                    model = ModelStage.Create(line.Portrait);
                    stages[line.Portrait] = model;
                }
                if (model == null) return;
                portrait.AddToClassList("tut-model");
                portrait.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(model.Texture));
                model.Cheer();
            }

            void Render()
            {
                var line = lines[index];
                bool last = index == lines.Count - 1;
                shown = 0;
                body.text = "";
                nameplate.style.display = line.Speaker == "" ? DisplayStyle.None : DisplayStyle.Flex;
                nameLabel.text = line.Speaker;
                body.EnableInClassList("story-narration", line.Speaker == "");
                ShowPortrait(line);
                step.text = lines.Count > 1 ? $"{index + 1} / {lines.Count}" : "";
                action.Clear();
                next.style.display = last ? DisplayStyle.None : DisplayStyle.Flex;
                skip.style.display = last ? DisplayStyle.None : DisplayStyle.Flex;
                if (last) action.Add(UiKit.Btn("繼續", Close, primary: true));
                typer?.Pause();
                typer = body.schedule.Execute(() =>
                {
                    if (shown < line.Text.Length)
                    {
                        shown++;
                        body.text = line.Text.Substring(0, shown);
                    }
                }).Every(CharIntervalMs);
            }

            void Advance()
            {
                if (Typing())
                {
                    shown = lines[index].Text.Length;
                    body.text = lines[index].Text;
                    return;
                }
                if (index < lines.Count - 1) { index++; Render(); }
            }

            // 點對話帶任何地方：先把字顯示完，再點才進下一句（最後一句用按鈕結束）
            band.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return;
                Advance();
            });
            next.schedule.Execute(() => next.ToggleInClassList("tut-next-dim")).Every(520);

            Render();
            return true;
        }
    }
}
