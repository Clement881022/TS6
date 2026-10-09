#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;
namespace SanGuo.Client
{
    public sealed partial class BattleScreen
    {
        private VisualElement? _aimLayer;
        private void BuildAimLayer()
        {
            _aimLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _aimLayer.style.position = UnityEngine.UIElements.Position.Absolute;
            _aimLayer.style.left = _aimLayer.style.top = _aimLayer.style.right = _aimLayer.style.bottom = 0;
            _aimLayer.generateVisualContent += DrawEnemyAim;
            _tagLayer.Add(_aimLayer);
        }
        private void DrawEnemyAim(MeshGenerationContext context)
        {
            if (_battle == null || _aimLayer == null) return;
            var painter = context.painter2D;
            painter.lineWidth = 3;
            painter.strokeColor = new Color(1f,.25f,.18f,.25f+.75f*(.5f+.5f*Mathf.Sin(Time.time*Mathf.PI*2)));
            foreach (var enemy in _battle.Units.Where(u => u.Alive && u.Side == Side.Enemy))
            {
                var intent = _battle.GetIntent(enemy);
                if (intent.Type != Intent.Kind.Attack || intent.Target == null || !intent.Target.Alive) continue;
                var source = _stage.UnitHeadPanel(enemy);
                var target = _stage.UnitHeadPanel(intent.Target);
                if (source == null || target == null) continue;
                var a = _aimLayer.WorldToLocal(source.Value) + new Vector2(0,65);
                var b = _aimLayer.WorldToLocal(target.Value) + new Vector2(0,65);
                var control = new Vector2((a.x+b.x)*.5f,Mathf.Min(a.y,b.y)-150);
                int count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a,b)/24),8,64);
                for (int i = 0; i < count; i++)
                {
                    float t0 = i/(float)count, t1 = (i+.55f)/count;
                    Vector2 Point(float t) => (1-t)*(1-t)*a + 2*(1-t)*t*control + t*t*b;
                    painter.BeginPath();painter.MoveTo(Point(t0));painter.LineTo(Point(t1));painter.Stroke();
                }
            }
        }
        private void BuildSkillDetail(CardInstance card)
        {
            var def = card.Def;
            var heading = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("skill-head");
            heading.Add(new Label(def.Cost.ToString()).WithClass("skill-cost"));
            heading.Add(new Label(def.Name).WithClass("skill-title"));
            heading.Add(UiIcons.Icon(CardPresentation.TypeIcon(def),"skill-type"));
            _detail.Add(heading);
            var content = new ScrollView(ScrollViewMode.Vertical).WithClass("skill-scroll");
            content.contentContainer.style.flexShrink = 0;
            _detail.Add(content);
            foreach (var effect in def.Effects)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("skill-effect");
                row.Add(UiIcons.Icon(CardPresentation.EffectIcon(effect),"skill-effect-icon"));
                var values = new VisualElement();
                values.Add(new Label((effect.OnAllies ? "全隊 · " : effect.OnSelf ? "自身 · " : "") + CardPresentation.Label(effect)).WithClass("skill-effect-label"));
                values.Add(new Label(CardPresentation.Value(effect)).WithClass("skill-effect-value"));
                row.Add(values);
                content.Add(row);
                if (effect.BonusPerDebuff > 0) content.Add(new Label($"每個減益 +{effect.BonusPerDebuff*100:0}%").WithClass("skill-extra"));
                if (effect.EliteBossMultiplier > 0) content.Add(new Label($"精英／首領 ×{effect.EliteBossMultiplier:0.##}").WithClass("skill-extra"));
            }
            int range = card.Owner != null ? SanGuo.Core.Battle.CardRange(card.Owner,def) : 1;
            var target = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("skill-target");
            target.Add(RangeIcon.Build(def,range));
            content.Add(target);
            content.Add(new Label(CardText.Target(def,range)).WithClass("skill-target-text"));
            if (def.KillRefund > 0) content.Add(new Label($"擊殺返還 {def.KillRefund} 費").WithClass("skill-extra"));
            foreach (var child in content.contentContainer.Children()) child.style.flexShrink = 0;
        }
        public void DebugShowObjective() => ShowObjective();
        private void ShowObjective()
        {
            _objectiveHint = true;
            RefreshHint();
        }
        private void OnRootPointerDown(PointerDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (_objectiveHint && target != null && target != _objectiveButton && !_objectiveButton.Contains(target))
            {
                _objectiveHint = false;
                RefreshHint();
            }
            if (_infoUnit != null && target != null && !_unitList.Contains(target)) HideUnitInfo();
        }
        private string? CurrentHint()
        {
            if (_objectiveHint) return MapPage.ObjectiveText(_battle.Setup) ?? "目標：全滅敵人";
            var card = _pendingCard;
            if (card == null || !_battle.Hand.Contains(card)) return null;
            var check = _battle.CanPlay(card);
            if (check != PlayResult.Ok) return Explain(check);
            switch (card.Def.Target)
            {
                case TargetRule.MoveDest: return _pendingMover == null ? "點選要移動的武將" : "再點選綠色格子移動";
                case TargetRule.Enemy: return "點選射程內的敵人";
                case TargetRule.Ally: return "點選射程內的隊友";
                default: return null;
            }
        }
        private void RefreshHint()
        {
            var text = CurrentHint();
            _hint.text = text ?? "";
            _hint.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
        }
        private List<List<Position>> EnemyMovePaths()
        {
            var paths = new List<List<Position>>();
            foreach (var enemy in _battle.Units.Where(u => u.Alive && u.Side == Side.Enemy))
            {
                var intent = _battle.GetIntent(enemy);
                if (intent.Type != Intent.Kind.Move || intent.MoveTo == null) continue;
                var reach = _battle.ReachableTiles(enemy);
                var current = intent.MoveTo.Value;
                if (!reach.ContainsKey(current)) continue;
                var path = new List<Position> { current };
                while (reach[current] > 0)
                {
                    bool stepped = false;
                    foreach (var (dl, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var next = new Position(current.Lane + dl, current.Row + dr);
                        if (!reach.TryGetValue(next, out var steps) || steps != reach[current] - 1) continue;
                        current = next;
                        path.Insert(0, current);
                        stepped = true;
                        break;
                    }
                    if (!stepped) break;
                }
                paths.Add(path);
            }
            return paths;
        }
        public void DebugArtSelectCard(int index)
        {
            if (index < 0 || index >= _battle.Hand.Count) throw new InvalidOperationException("Missing card");
            if (_pendingCard == _battle.Hand[index]) CancelTargeting();
            else BeginPending(_battle.Hand[index]);
        }
        public void DebugValidateCommercialArt()
        {
            _stage.DebugValidateGridPicking();
            foreach (var unit in _battle.Units.Where(u => u.Alive))
            {
                var tag = _tags[unit.Id];
                if (!tag.Root.Contains(tag.Name) || !tag.Root.Contains(tag.HpText)) throw new InvalidOperationException("Missing head HUD");
                if (tag.Root.resolvedStyle.visibility != Visibility.Visible) throw new InvalidOperationException("Hidden head HUD: "+unit.Name);
                if (unit.Side == Side.Enemy && (tag.Intent.parent != tag.Name.parent || tag.Intent.parent.IndexOf(tag.Intent) != 0)) throw new InvalidOperationException("Intent order");
            }
            for (int i = 0; i < _handCards.Count; i++)
            {
                var card = _handCards[i];
                if (card.Q(className:"sts-card-description") != null || card.Q(className:"sts-card-category") == null) throw new InvalidOperationException("Card art fields");
                if (card.worldBound.width < 1) throw new InvalidOperationException("Card layout");
            }
            Debug.Log("[commercial] grid picking, all head HUDs, intent order and compact card fields passed");
        }
    }
}
