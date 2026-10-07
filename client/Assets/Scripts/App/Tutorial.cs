#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 新手引導：第一次遇到某情境時彈出分頁說明（下一步 / 知道了），看過就記在本機 PlayerPrefs。
    /// 截圖模式（-sanguoShot）不彈，避免擋住畫面。
    /// </summary>
    public static class Tutorial
    {
        private const string Prefix = "sanguo_tut_";

        public static bool Seen(string key)
        {
            if (GameSession.ShotDir != null) return true;
            try { return PlayerPrefs.GetInt(Prefix + key, 0) == 1; }
            catch (Exception) { return false; }
        }

        public static void MarkSeen(string key)
        {
            try { PlayerPrefs.SetInt(Prefix + key, 1); PlayerPrefs.Save(); }
            catch (Exception) { /* 存不了就下次再看一次，不影響遊戲 */ }
        }

        /// <summary>清掉所有引導紀錄（重看一次新手流程用）。</summary>
        public static void ResetAll()
        {
            foreach (var key in new[] { "home", "battle1" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 沒看過才顯示。最後一頁按鈕文字為 doneText，按下後記為看過並呼叫 onDone；跳過只記為看過。
        /// 回傳 true 表示有彈出。
        /// </summary>
        public static bool Show(VisualElement layer, string key, string title, string[] pages,
            string doneText = "知道了", Action? onDone = null)
        {
            if (Seen(key) || pages.Length == 0) return false;

            var overlay = new VisualElement();
            overlay.AddToClassList("overlay");
            overlay.AddToClassList("tut-overlay");
            var card = new VisualElement();
            card.AddToClassList("tut-card");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("tut-title");
            var body = new Label();
            body.AddToClassList("tut-body");
            var step = new Label();
            step.AddToClassList("tut-step");
            var buttons = new VisualElement();
            buttons.AddToClassList("tut-buttons");
            card.Add(titleLabel);
            card.Add(body);
            card.Add(step);
            card.Add(buttons);
            overlay.Add(card);
            layer.Add(overlay);

            int index = 0;
            void Close(bool finished)
            {
                MarkSeen(key);
                overlay.RemoveFromHierarchy();
                if (finished) onDone?.Invoke();
            }
            void Render()
            {
                bool last = index == pages.Length - 1;
                body.text = pages[index];
                step.text = pages.Length > 1 ? $"{index + 1} / {pages.Length}" : "";
                buttons.Clear();
                if (!last) buttons.Add(UiKit.Btn("跳過", () => Close(false)));
                buttons.Add(UiKit.Btn(last ? doneText : "下一步", () =>
                {
                    if (last) Close(true);
                    else { index++; Render(); }
                }, primary: true));
            }
            Render();
            return true;
        }
    }
}
