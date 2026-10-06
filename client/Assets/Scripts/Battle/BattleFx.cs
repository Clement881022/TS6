#nullable enable
using System;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;
using EventType = SanGuo.Core.EventType;

namespace SanGuo.Client
{
    /// <summary>
    /// 戰鬥演出：把核心產生的事件轉成畫面特效（飄字）與角色模型動作（攻擊 / 施法 / 受擊 / 倒下）。
    /// 核心的狀態是瞬間算完的，這裡把事件排成時間序列依序播放，讓玩家看得懂發生了什麼。
    /// </summary>
    public sealed class BattleFx
    {
        private const float StepSeconds = 0.26f;

        private readonly VisualElement _layer;
        private readonly Func<Side, Position, Vector2> _headPoint;
        private readonly Func<int, CharacterView?> _viewOf;
        private float _nextFreeTime;

        public BattleFx(VisualElement layer, Func<Side, Position, Vector2> headPoint, Func<int, CharacterView?> viewOf)
        {
            _layer = layer;
            _headPoint = headPoint;
            _viewOf = viewOf;
        }

        /// <summary>目前排隊中的演出大約還要多久（秒）；自動戰鬥用來等演出播完。</summary>
        public float PendingSeconds => Mathf.Max(0f, _nextFreeTime - Time.realtimeSinceStartup);

        public void Reset() => _nextFreeTime = 0f;

        public void Play(BattleEvent e)
        {
            switch (e.Type)
            {
                case EventType.CardPlayed:
                    Enqueue(() => _viewOf(e.Source)?.Cast(), 0f);
                    break;
                case EventType.Damage:
                    if (e.Source >= 0) Enqueue(() => _viewOf(e.Source)?.Attack(), 0f);
                    bool crit = e.Text == "crit";
                    Enqueue(() =>
                    {
                        _viewOf(e.Target)?.Hit();
                        Float(e.TargetSide, e.TargetPos, crit ? $"-{e.Value}!" : $"-{e.Value}",
                            crit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.45f, 0.45f), crit ? 52 : 40);
                    }, e.Source >= 0 ? 0.2f : 0f, advance: true);
                    break;
                case EventType.Dodge:
                    Enqueue(() => Float(e.TargetSide, e.TargetPos, "閃避", new Color(0.85f, 0.88f, 0.95f), 36), 0f, advance: true);
                    break;
                case EventType.Heal:
                    if (e.Value > 0)
                        Enqueue(() => Float(e.TargetSide, e.TargetPos, $"+{e.Value}", new Color(0.45f, 0.95f, 0.55f), 40), 0f, advance: true);
                    break;
                case EventType.Armor:
                    Enqueue(() => Float(e.TargetSide, e.TargetPos, $"護甲 +{e.Value}", new Color(0.55f, 0.8f, 1f), 34), 0f, advance: true);
                    break;
                case EventType.StatusApplied:
                    Enqueue(() => Float(e.TargetSide, e.TargetPos, StatusLabel(e.Text), new Color(1f, 0.78f, 0.4f), 34), 0f, advance: true);
                    break;
                case EventType.Death:
                    Enqueue(() =>
                    {
                        _viewOf(e.Target)?.Die();
                        Float(e.TargetSide, e.TargetPos, "倒下", new Color(0.8f, 0.8f, 0.85f), 40);
                    }, 0.1f, advance: true);
                    break;
            }
        }

        private static string StatusLabel(string statusName)
        {
            return Enum.TryParse<StatusType>(statusName, out var type) ? CardText.StatusName(type) : statusName;
        }

        /// <summary>排進時間序列：action 在 (前一個演出結束 + offset) 時執行。</summary>
        private void Enqueue(Action action, float offset, bool advance = false)
        {
            float now = Time.realtimeSinceStartup;
            if (_nextFreeTime < now) _nextFreeTime = now;
            float delay = _nextFreeTime - now + offset;
            _layer.schedule.Execute(action).StartingIn((long)(delay * 1000f));
            if (advance) _nextFreeTime += StepSeconds + offset;
        }

        private void Float(Side side, Position pos, string text, Color color, int size)
        {
            var local = _layer.WorldToLocal(_headPoint(side, pos));

            var label = new Label(text);
            label.AddToClassList("fx-float");
            label.pickingMode = PickingMode.Ignore;
            label.style.color = color;
            label.style.fontSize = size;
            label.style.width = 220;
            label.style.left = local.x - 110;
            label.style.top = local.y - 30;
            _layer.Add(label);

            label.schedule.Execute(() =>
            {
                label.style.translate = new Translate(0, -80);
                label.style.opacity = 0f;
            }).StartingIn(30);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn(1000);
        }
    }
}
