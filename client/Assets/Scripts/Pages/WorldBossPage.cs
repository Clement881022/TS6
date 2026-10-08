#nullable enable
using System;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 世界 Boss：本季 Boss（模型）、剩餘挑戰次數、本季最佳與名次、前 10 名、上一季結算。
    /// 挑戰走編隊頁 → 戰鬥（10 回合，對 Boss 造成的傷害即分數，由後端重播計算）。
    /// </summary>
    public sealed class WorldBossPage : PageBase
    {
        private WorldBossView? _view;
        private ModelStage? _stage;
        private string _stageBoss = "";

        protected override Page Id => Page.WorldBoss;
        protected override string Title => "世界 Boss";

        private void OnDestroy()
        {
            _stage?.Dispose();
            _stage = null;
        }

        protected override async void OnReady()
        {
            try
            {
                _view = await GameSession.Backend.GetWorldBoss();
                if (_view == null) Toast(UiText.ExplainBackend("network"));
                await GameSession.Refresh(); // 換季結算可能發了元寶
                Rebuild();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast(UiText.ExplainBackend("network"));
            }
        }

        protected override void BuildBody(VisualElement body)
        {
            body.style.flexDirection = FlexDirection.Row;
            body.AddToClassList("page-centered");
            string season = _view?.Season is { Length: > 0 } s ? s : WorldBoss.SeasonOf(GameSession.View.Now);
            var boss = WorldBoss.BossOf(season);

            // ---- 左：Boss 模型與資訊 ----
            var left = UiKit.Panel();
            left.style.width = Length.Percent(42);
            left.style.marginRight = 24;
            left.style.alignItems = Align.Center;
            left.Add(UiKit.Text($"{WorldBoss.SeasonName(season)}　賽季 Boss", "bpanel-title"));
            string art = boss.Art != "" ? boss.Art : boss.Id;
            if (_stageBoss != art)
            {
                _stage?.Dispose();
                _stage = ModelStage.Create(art, 640, 640);
                _stageBoss = art;
            }
            if (_stage != null)
            {
                var model = new Button(() => _stage?.Cheer());
                model.style.width = 380;
                model.style.height = 380;
                model.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_stage.Texture));
                model.style.backgroundColor = new StyleColor(Color.clear);
                model.style.borderTopWidth = model.style.borderBottomWidth = model.style.borderLeftWidth = model.style.borderRightWidth = 0;
                left.Add(model);
            }
            left.Add(UiKit.Text(boss.Name, "stage-title"));
            left.Add(UiKit.Text($"Lv.{WorldBoss.BossLevel}　{CardText.RoleName(boss.Role)}", "line-sub"));
            left.Add(UiKit.Text($"{WorldBoss.TurnLimit} 回合內盡可能造成傷害，以本季單場最高傷害排名。", "line-sub"));
            left.Add(UiKit.Text("每月結算：依名次發放元寶，前 10 名獲得稱號。", "line-sub"));
            body.Add(left);

            // ---- 右：成績、排行榜、挑戰 ----
            var right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.flexShrink = 1;
            right.style.maxHeight = Length.Percent(100);
            right.style.flexDirection = FlexDirection.Column;

            var mine = UiKit.Panel();
            mine.Add(UiKit.Section("本季戰績"));
            if (_view == null) mine.Add(UiKit.Text("讀取中……", "line-sub"));
            else
            {
                mine.Add(UiKit.Text(_view.Best > 0 ? $"最高傷害　{_view.Best:N0}" : "本季尚未出戰", "txt-gold"));
                if (_view.Rank > 0) mine.Add(UiKit.Text($"目前名次　第 {_view.Rank} 名 / {_view.Total} 人", "line-title"));
                if (_view.Title != "") mine.Add(UiKit.Text("稱號　" + _view.Title, "line-title"));
                if (_view.LastSeason != "")
                    mine.Add(UiKit.Text($"{WorldBoss.SeasonName(_view.LastSeason)}結算：第 {_view.LastRank} 名 / {_view.LastTotal} 人，獲得元寶 {_view.LastReward}", "line-sub"));
            }
            var footer = UiKit.Row();
            footer.style.marginTop = 8;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems = Align.Center;
            if (_view != null && !_view.Unlocked) footer.Add(UiKit.DoneBtn("通關第二章後開放"));
            else
            {
                int left2 = _view?.AttemptsLeft ?? 0;
                var count = UiKit.Text($"今日剩餘 {left2} / {WorldBoss.DailyAttempts} 次（不消耗體力）", "line-sub");
                count.style.marginRight = 16;
                footer.Add(count);
                var go = UiKit.Btn("挑戰", Challenge, primary: true).WithClass("btn-lg");
                go.SetEnabled(_view != null && left2 > 0);
                footer.Add(go);
            }
            mine.Add(footer);
            right.Add(mine);

            var board = UiKit.Panel();
            board.style.flexGrow = 1;
            board.style.flexShrink = 1;
            board.style.overflow = Overflow.Hidden;
            board.style.marginTop = 16;
            board.Add(UiKit.Section("群雄榜（前 10 名）"));
            if (_view == null || _view.Top.Count == 0) board.Add(UiKit.Text("目前還沒有人上榜", "line-sub"));
            else
                for (int i = 0; i < _view.Top.Count; i++)
                {
                    var (name, best) = _view.Top[i];
                    var row = UiKit.Row();
                    row.Add(UiKit.Text($"{i + 1}.", i < 3 ? "txt-gold" : "line-title"));
                    var who = UiKit.Text(name, "line-title");
                    who.style.flexGrow = 1;
                    who.style.marginLeft = 12;
                    row.Add(who);
                    row.Add(UiKit.Text($"{best:N0}", "line-title"));
                    board.Add(row);
                }
            right.Add(board);

            body.Add(right);
        }

        private void Challenge()
        {
            if (Busy) return;
            GameSession.FormationStageId = WorldBoss.StageId;
            Nav.Go(Page.Formation);
        }
    }
}
