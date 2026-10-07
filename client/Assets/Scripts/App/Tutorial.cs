#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 新手引導（視覺小說式對話）：底部半透明對話帶 + 菱形名牌 + 右側角色立繪，逐字顯示，點一下繼續；
    /// 看過就記在本機 PlayerPrefs。截圖模式（-sanguoShot）不彈，避免擋住畫面。
    /// </summary>
    public static class Tutorial
    {
        private const string Prefix = "sanguo_tut_";
        private const long CharIntervalMs = 28;

        public static bool Seen(string key)
        {
            if (GameSession.ShotDir != null) return !ForceShow;
            try { return PlayerPrefs.GetInt(Prefix + key, 0) == 1; }
            catch (Exception) { return false; }
        }

        /// <summary>截圖模式加 -sanguoShowTutorial 才會彈教學（驗證畫面用）。</summary>
        private static readonly bool ForceShow = Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoShowTutorial") >= 0;

        public static void MarkSeen(string key)
        {
            if (GameSession.ShotDir != null) return;
            try { PlayerPrefs.SetInt(Prefix + key, 1); PlayerPrefs.Save(); }
            catch (Exception) { /* 存不了就下次再看一次，不影響遊戲 */ }
        }

        /// <summary>清掉所有引導紀錄（重看一次新手流程用）。</summary>
        public static void ResetAll()
        {
            foreach (var key in new[] { "home", "battle1", "story_intro", "story_before_1", "story_before_2", "story_before_3", "story_before_4", "story_before_5", "story_before_6", "story_before_7", "story_before_8", "story_before_9", "story_before_10" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 沒看過才顯示。title 是對話標題；speaker 是名牌上的說話者（預設用 title）；heroId 有全身立繪時站在右側。
        /// 最後一頁的按鈕文字為 doneText，按下後記為看過並呼叫 onDone；跳過只記為看過。回傳 true 表示有彈出。
        /// </summary>
        public static bool Show(VisualElement layer, string key, string title, string[] pages,
            string doneText = "知道了", Action? onDone = null, string? speaker = null, string? heroId = null, string? model = null)
        {
            if (Seen(key) || pages.Length == 0) return false;

            var overlay = new VisualElement();
            overlay.AddToClassList("overlay");
            overlay.AddToClassList("tut-overlay");

            ModelStage? stage = model != null ? ModelStage.Create(model) : null;
            var full = stage == null && heroId != null ? HeroArt.Full(heroId) : null;
            if (stage != null)
            {
                var m = new VisualElement { pickingMode = PickingMode.Ignore };
                m.AddToClassList("tut-model");
                m.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(stage.Texture));
                overlay.Add(m);
            }
            else if (full != null)
            {
                var hero = new VisualElement { pickingMode = PickingMode.Ignore };
                hero.AddToClassList("tut-hero");
                hero.style.backgroundImage = new StyleBackground(full);
                overlay.Add(hero);
            }

            var nameplate = new VisualElement { pickingMode = PickingMode.Ignore };
            nameplate.AddToClassList("tut-nameplate");
            nameplate.Add(new VisualElement().WithClass("tut-diamond"));
            nameplate.Add(new Label(speaker ?? title).WithClass("tut-name"));
            nameplate.Add(new VisualElement().WithClass("tut-diamond"));
            overlay.Add(nameplate);

            var band = new VisualElement();
            band.AddToClassList("tut-band");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("tut-title");
            var body = new Label();
            body.AddToClassList("tut-body");
            var footer = new VisualElement();
            footer.AddToClassList("tut-footer");
            var step = new Label();
            step.AddToClassList("tut-step");
            var next = new VisualElement();
            next.AddToClassList("tut-next");
            var action = new VisualElement();
            action.AddToClassList("tut-action");
            footer.Add(step);
            footer.Add(action);
            band.Add(titleLabel);
            band.Add(body);
            band.Add(footer);
            band.Add(next);
            overlay.Add(band);

            int index = 0;
            int shown = 0;
            IVisualElementScheduledItem? typer = null;

            var skip = UiKit.Btn("略過", () => Close(false));
            skip.AddToClassList("tut-skip");
            overlay.Add(skip);
            layer.Add(overlay);

            void Close(bool finished)
            {
                typer?.Pause();
                stage?.Dispose();
                MarkSeen(key);
                overlay.RemoveFromHierarchy();
                if (finished) onDone?.Invoke();
            }
            bool Typing() => shown < pages[index].Length;
            void Advance()
            {
                if (Typing())
                {
                    shown = pages[index].Length;
                    body.text = pages[index];
                    return;
                }
                if (index < pages.Length - 1) { index++; Render(); stage?.Cheer(); }
            }
            void Render()
            {
                bool last = index == pages.Length - 1;
                shown = 0;
                body.text = "";
                step.text = pages.Length > 1 ? $"{index + 1} / {pages.Length}" : "";
                action.Clear();
                next.style.display = last ? DisplayStyle.None : DisplayStyle.Flex;
                skip.style.display = last ? DisplayStyle.None : DisplayStyle.Flex;
                if (last) action.Add(UiKit.Btn(doneText, () => Close(true), primary: true));
                typer?.Pause();
                typer = body.schedule.Execute(() =>
                {
                    if (shown < pages[index].Length)
                    {
                        shown++;
                        body.text = pages[index].Substring(0, shown);
                    }
                }).Every(CharIntervalMs);
            }

            // 點對話帶任何地方：先把字顯示完，再點才進下一頁（最後一頁用按鈕結束）
            band.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return;
                Advance();
            });
            // ▽ 提示閃爍
            next.schedule.Execute(() => next.ToggleInClassList("tut-next-dim")).Every(520);

            Render();
            return true;
        }
    }
}
