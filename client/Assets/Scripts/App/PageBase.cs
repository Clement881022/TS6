#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 頁面 prefab 上的元件（Resources/Pages/&lt;Page&gt;.prefab）：由 PageHost 載入、切走時連同 prefab 一起銷毀。
    /// 這個基底負責外框、重畫與「執行後端操作 → 重抓資料 → 重畫」的流程；
    /// 子類別只要實作 BuildBody，畫內容區。規則都在後端（SanGuo.Core），這裡只顯示與送出操作。
    /// </summary>
    public abstract class PageBase : MonoBehaviour
    {
        private VisualElement _layer = null!;
        private VisualElement _host = null!;
        private bool _busy;

        protected abstract Page Id { get; }
        protected abstract string Title { get; }
        /// <summary>false = 不顯示底部導覽（流程中的頁面，例如編隊）。</summary>
        protected virtual bool ShowNav => true;
        /// <summary>false = 不用共用的頂欄 / 導覽外框，頁面自己鋪滿整個畫面（主城用）。</summary>
        protected virtual bool UseFrame => true;
        /// <summary>左上角返回鍵回到的頁面（關閉鍵一律回主城）。</summary>
        protected virtual Page BackPage => Page.Home;
        protected abstract void BuildBody(VisualElement body);

        /// <summary>頁面內容區（Rebuild 時會清空重畫）；彈出視窗請加在這裡，會跟著重畫消失。</summary>
        protected VisualElement Host => _host;
        protected bool Busy => _busy;

        /// <summary>PageHost 載入 prefab 後呼叫：container 是這個頁面專用的 UI 容器（切走時整個移除）。</summary>
        public virtual async void Open(VisualElement container)
        {
            _layer = container;
            _host = new VisualElement();
            _host.AddToClassList("page-host");
            _host.AddToClassList("bg-" + Id.ToString().ToLowerInvariant());
            if (Id != Page.Battle) _host.AddToClassList("meta-page"); // Polish.uss 的統一尺寸只作用於非戰鬥頁面
            container.Add(_host);
            Rebuild();
            if (await GameSession.Refresh()) Rebuild();
            else Toast(UiText.ExplainBackend("network"));
            OnReady();
        }

        /// <summary>第一次資料載入完成後呼叫（可用來開啟預設視窗）。</summary>
        protected virtual void OnReady() { }

        public void Rebuild()
        {
            _host.Clear();
            if (!UseFrame) { BuildBody(_host); return; }
            var body = UiKit.Frame(_host, Title, BackPage);
            BuildBody(body);
        }

        protected void Toast(string message) => UiKit.Toast(_layer, message);

        /// <summary>執行一個後端操作：成功就重抓資料並重畫，失敗顯示原因。</summary>
        /// <param name="describe">依結果產生成功提示（需要顯示獎勵數字時用）；優先於 successToast。</param>
        protected async Task Act(Func<Task<BackendResult>> action, string? successToast = null,
            Func<BackendResult, string?>? describe = null)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var r = await action();
                if (!r.Ok) { Toast(UiText.ExplainBackend(r.Code)); return; }
                await GameSession.Refresh();
                string? message = describe != null ? describe(r) : successToast;
                if (message != null) Toast(message);
                Rebuild();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast(UiText.ExplainBackend("network"));
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>向後端開始關卡 / 副本成功就切到戰鬥場景，失敗顯示原因。</summary>
        protected async Task StartBattle(string stageId)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                string? error = await GameSession.BeginStage(stageId);
                if (error != null) { Toast(error); return; }
                Nav.Go(Page.Battle);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>進入主線關卡：可編隊的關卡先到編隊頁（開戰才扣體力）；鎖定編隊的直接開戰。</summary>
        protected void EnterLevel(int level)
        {
            if (_busy || level < 1 || level > SanGuo.Core.DemoContent.ChapterLevelCount) return;
            GameSession.SelectedLevel = level;
            StoryPlayer.ShowBefore(Host, level, () =>
            {
                if (GameSession.FormationLocked(level)) _ = StartBattle(GameSession.StageIdOf(level));
                else { GameSession.FormationStageId = GameSession.StageIdOf(level); Nav.Go(Page.Formation); }
            });
        }

        /// <summary>進入資源副本：先到編隊頁排兵（開戰才扣體力與次數）。</summary>
        protected void EnterDungeon(string dungeonId)
        {
            if (_busy) return;
            GameSession.FormationStageId = dungeonId;
            Nav.Go(Page.Formation);
        }
    }
}
