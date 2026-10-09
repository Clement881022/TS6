#nullable enable
using System;
using System.Collections.Generic;
using SanGuo.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public static class StoryPlayer
    {
        private const long CharIntervalMs = 28;

        public static void ShowIntro(VisualElement layer, Action proceed)
        {
            const string key = "story_intro";
            if (GameSession.View.ClearedStages.Count > 0 || Tutorial.Seen(key)) return;
            Show(layer, "序章　涿縣的清晨", DemoStory.Intro(), () => { Tutorial.MarkSeen(key); proceed(); });
        }

        public static void ShowBefore(VisualElement layer, int chapter, int level, Action proceed)
        {
            string key = chapter == 0 ? "story_before_" + level : $"story_before_{chapter}_{level}";
            bool cleared = GameSession.View.ClearedStages.Contains(GameSession.StageIdOf(chapter, level));
            if (cleared || Tutorial.Seen(key) || !SanGuo.Core.Campaign.IsValid(chapter, level)
                || !Show(layer, StageTitle(chapter, level), CampaignStory.Before(chapter, level), () => { Tutorial.MarkSeen(key); proceed(); }))
                proceed();
        }

        public static void ShowAfter(VisualElement layer, int chapter, int level)
        {
            if (GameSession.HardMode) return;
            if (SanGuo.Core.Campaign.IsValid(chapter, level))
                Show(layer, StageTitle(chapter, level), CampaignStory.After(chapter, level));
        }

        public static string StageTitle(int chapter, int level) => $"{chapter}-{level}　{SanGuo.Core.Campaign.LevelName(chapter, level)}";

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
            var args = Environment.GetCommandLineArgs();
            int dbg = Array.IndexOf(args, "-sanguoStoryLine");
            if (dbg >= 0 && dbg + 1 < args.Length && int.TryParse(args[dbg + 1], out int startAt)) index = Mathf.Clamp(startAt, 0, lines.Count - 1);
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
                portrait.style.backgroundImage = StyleKeyword.None;
                portrait.RemoveFromClassList("story-bust");
                if (line.Portrait == "") return;
                var art = HeroArt.Full(line.Portrait) ?? HeroArt.Face(line.Portrait);
                if (art != null)
                {
                    portrait.AddToClassList("story-bust");
                    portrait.style.backgroundImage = new StyleBackground(art);
                    return;
                }
                if (!stages.TryGetValue(line.Portrait, out var model))
                {
                    model = ModelStage.Create(line.Portrait, 640, 600, bust: true);
                    stages[line.Portrait] = model;
                }
                if (model == null) return;
                portrait.AddToClassList("story-bust");
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
