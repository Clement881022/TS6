#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public abstract class PageBase : MonoBehaviour
    {
        private VisualElement _layer = null!;
        private VisualElement _host = null!;
        private bool _busy;

        protected abstract Page Id { get; }
        protected abstract string Title { get; }
        protected virtual bool ShowNav => true;
        protected virtual bool UseFrame => true;
        protected virtual Page BackPage => Page.Home;
        protected virtual bool NeedsProfile => true;
        protected abstract void BuildBody(VisualElement body);

        protected VisualElement Host => _host;
        protected bool Busy => _busy;

        public virtual async void Open(VisualElement container)
        {
            _layer = container;
            _host = new VisualElement();
            _host.AddToClassList("page-host");
            _host.AddToClassList("bg-" + Id.ToString().ToLowerInvariant());
            if (Id != Page.Battle) _host.AddToClassList("meta-page");
            container.Add(_host);
            Rebuild();
            if (!NeedsProfile) { OnReady(); return; }
            if (await GameSession.Refresh()) Rebuild();
            else if (this == null || PageHost.Current?.ActivePage != this) return;
            else Toast(UiText.ExplainBackend("network"));
            OnReady();
        }

        protected virtual void OnReady() { }

        public void Rebuild()
        {
            _host.Clear();
            if (!UseFrame) { BuildBody(_host); return; }
            var body = UiKit.Frame(_host, Title, BackPage, Id);
            BuildBody(body);
        }

        protected void Toast(string message) => UiKit.Toast(_layer, message);

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

        protected void EnterLevel(int chapter, int level)
        {
            if (_busy || !SanGuo.Core.Campaign.IsValid(chapter, level)) return;
            GameSession.Select(chapter, level);
            StoryPlayer.ShowBefore(Host, chapter, level, () =>
            {
                if (GameSession.FormationLocked(chapter, level)) _ = StartBattle(GameSession.StageIdOf(chapter, level));
                else { GameSession.FormationStageId = GameSession.StageIdOf(chapter, level); Nav.Go(Page.Formation); }
            });
        }

        protected void EnterDungeon(string dungeonId)
        {
            if (_busy) return;
            GameSession.FormationStageId = dungeonId;
            Nav.Go(Page.Formation);
        }
    }
}
