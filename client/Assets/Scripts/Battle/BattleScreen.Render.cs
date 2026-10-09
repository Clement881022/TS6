#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;
using EventType = SanGuo.Core.EventType;

namespace SanGuo.Client
{
    public sealed partial class BattleScreen
    {
        private void Toast(string message)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            toast.pickingMode = PickingMode.Ignore;
            _root.Add(toast);
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(1300);
        }

        private void Refresh()
        {
            RefreshTags();
            RefreshTiles();
            RefreshCards();
            RefreshDetail();
            RefreshUnitList();
            RefreshHud();
            RefreshOverlay();
            if (_infoUnit != null)
            {
                if (_infoUnit.Alive) RenderUnitInfo(_infoUnit);
                else HideUnitInfo();
            }
        }

        private void RefreshTiles()
        {
            var states = new List<(Position, TileState)>();
            foreach (var p in _previewRange) states.Add((p, TileState.Range));
            foreach (var p in _previewReach) states.Add((p, TileState.Reach));
            foreach (var p in _previewTargets) states.Add((p, TileState.Target));
            var actor = _pendingMover ?? _pendingCard?.Owner;
            if (actor != null) states.Add((actor.Pos, TileState.Owner));
            _stage.SetTileStates(states);
            _stage.SetMoveArrows(EnemyMovePaths());
        }

        private void RefreshTags()
        {
            foreach (var unit in _battle.Units)
                if (!_tags.ContainsKey(unit.Id)) AddTag(unit);
            foreach (var unit in _battle.Units)
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                tag.Root.style.display = unit.Alive ? DisplayStyle.Flex : DisplayStyle.None;
                if (!unit.Alive) continue;

                tag.Name.text = unit.Name;
                tag.Role.Clear();
                string roleIcon = unit.Hero != null ? UiIcons.RoleIcon(unit.Hero.Role) : unit.AttackType == AttackType.Ranged ? "role_archer" : "role_warrior";
                tag.Role.Add(UiIcons.Icon(roleIcon, "icon-sm"));
                float ratio = unit.MaxHp <= 0 ? 0 : Mathf.Clamp01(unit.Hp / (float)unit.MaxHp);
                tag.HpFill.style.width = Length.Percent(ratio * 100f);
                tag.HpText.text = $"{unit.Hp}/{unit.MaxHp}";

                tag.Extra.Clear();
                if (unit.Shield > 0) tag.Extra.Add(UiIcons.Chip("armor", unit.Shield.ToString()));
                foreach (var st in unit.Statuses) tag.Extra.Add(UiIcons.Chip(UiIcons.Status(st.Key), ChipText(st.Key, st.Value)));
                foreach (var b in unit.Buffs) tag.Extra.Add(UiIcons.Chip(UiIcons.Status(b.Type), $"{b.Power}·{b.Turns}"));
                foreach (var br in unit.DefBreaks) tag.Extra.Add(UiIcons.Chip("status_armorbreak", $"{br.Percent * 100:0}%·{br.Turns}"));
                tag.Extra.style.display = tag.Extra.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

                tag.Intent.Clear();
                if (unit.Side == Side.Enemy) FillIntent(tag.Intent, unit);
                tag.Intent.style.display = unit.Side == Side.Enemy ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateTagPositions()
        {
            if (_tagLayer == null || _tagLayer.resolvedStyle.width < 1) return;
            float width = _tagLayer.resolvedStyle.width, height = _tagLayer.resolvedStyle.height;
            if (width < 1 || height < 1) return;
            foreach (var unit in _battle.Units)
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                var point = unit.Alive ? _stage.UnitTagPanel(unit) : null;
                if (point == null)
                {
                    tag.Root.style.visibility = Visibility.Hidden;
                    continue;
                }
                var anchor = _tagLayer.WorldToLocal(point.Value);
                float tagWidth = TagWidth;
                float measuredHeight = tag.Root.resolvedStyle.height;
                float tagHeight = float.IsNaN(measuredHeight) || measuredHeight < 1 ? 82f : measuredHeight;
                var placement = new Rect(anchor.x - tagWidth * 0.5f, anchor.y - tagHeight, tagWidth, tagHeight);
                tag.Root.style.visibility = Visibility.Visible;
                if (Mathf.Abs(tag.LastLeft - placement.x) < 0.5f && Mathf.Abs(tag.LastTop - placement.y) < 0.5f) continue;
                tag.LastLeft = placement.x; tag.LastTop = placement.y;
                tag.Root.style.left = placement.x;
                tag.Root.style.top = placement.y;
            }
        }
        private void FillIntent(VisualElement host, Unit enemy)
        {
            var intent = _battle.GetIntent(enemy);
            switch (intent.Type)
            {
                case Intent.Kind.Attack:
                    host.Add(UiIcons.Chip("damage", "攻擊"));
                    break;
                case Intent.Kind.Charge:
                case Intent.Kind.Charging: host.Add(UiIcons.Chip("charge", "蓄力中")); break;
                case Intent.Kind.Move: host.Add(UiIcons.Chip("draw", "逼近中")); break;
            }
        }

        private static string KindIcon(CardDef def) =>
            def.Effects.Any(e => e.Type == EffectType.Damage) ? "damage"
            : def.Effects.Any(e => e.Type == EffectType.Heal) ? "heal"
            : def.Target == TargetRule.MoveDest ? "draw" : "armor";

        private static VisualElement Face(Unit? owner, string cls)
        {
            if (owner != null) return PortraitArt.Create(owner.DefId, cls, cls == "sts-card-art");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList(cls);
            var portrait = owner != null ? HeroArt.Face(owner.DefId) : null;
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            else
            {
                var icon = UiIcons.Get("draw");
                if (icon != null) face.style.backgroundImage = new StyleBackground(icon);
            }
            return face;
        }

        private void RefreshCards()
        {
            _hand.Clear();
            _handCards.Clear();
            _hoverCard = null;
            foreach (var card in _battle.Hand)
            {
                var captured = card;
                var ok = _battle.CanPlay(card);
                var row = new VisualElement();
                row.AddToClassList("sts-card");
                if (card.Def.Target == TargetRule.MoveDest) row.AddToClassList("sts-card-move");
                else if (!card.Def.Basic) row.AddToClassList("sts-card-skill");
                if (ok != PlayResult.Ok) row.AddToClassList("sts-card-disabled");
                if (card == _pendingCard) row.AddToClassList("sts-card-selected");
                row.Add(new Label(card.Def.Cost.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("sts-card-cost"));
                var name = new Label(card.Def.Name) { pickingMode = PickingMode.Ignore };
                name.WithClass("sts-card-name");
                row.Add(name);
                var art = Face(card.Owner, "sts-card-art");

                row.Add(UiIcons.Icon(CardPresentation.TypeIcon(card.Def), "sts-card-category"));
                row.Add(art);

                var description = CardText.Description(card.Def);

                row.tooltip = card.Def.Name + "\n" + CardText.Target(card.Def, card.Owner?.AttackRange ?? 1) + "\n" + description + (ok != PlayResult.Ok ? "\n" + Explain(ok) : "");

                row.RegisterCallback<ClickEvent>(_ => OnCardClicked(captured));
                row.RegisterCallback<PointerEnterEvent>(_ => { _hoverCard = row; row.AddToClassList("sts-card-hover"); row.BringToFront(); LayoutHand(); Preview(captured); });
                row.RegisterCallback<PointerLeaveEvent>(_ => { if (_hoverCard == row) _hoverCard = null; row.RemoveFromClassList("sts-card-hover"); foreach (var sibling in _handCards) sibling.BringToFront(); LayoutHand(); Preview(null); });
                _hand.Add(row);
                _handCards.Add(row);
            }
            LayoutHand();
        }

        private void LayoutHand()
        {
            int count = _handCards.Count;
            if (count == 0) return;
            float width = _hand.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0) return;
            const float cardWidth = 224f;
            float step = count > 1 ? Mathf.Min(190f, (width - cardWidth - 40f) / (count - 1)) : 0f;
            float start = (width - (cardWidth + step * (count - 1))) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var tile = _handCards[i];
                float offset = i - (count - 1) * 0.5f;
                bool raised = tile == _hoverCard || tile.ClassListContains("sts-card-selected");
                tile.style.left = start + step * i;
                tile.style.top = raised ? 0f : 24f + Mathf.Abs(offset) * 3f;
                tile.style.rotate = new Rotate(new Angle(raised ? 0f : offset * 1.8f, AngleUnit.Degree));
            }
            foreach (var tile in _handCards)
                if (tile.ClassListContains("sts-card-selected")) tile.BringToFront();
            _hoverCard?.BringToFront();
        }

        private void RefreshDetail()
        {
            RefreshHint();
            var card = _pendingCard;
            _detail.Clear();
            if (card == null || !_battle.Hand.Contains(card)) { _detail.style.display = DisplayStyle.None; _field.style.marginRight = 18f; return; }
            _detail.style.display = DisplayStyle.Flex;
            _field.style.marginRight = 354f;
            BuildSkillDetail(card);
        }

        private static VisualElement StatChip(string icon, int effective, int baseValue, bool main = false)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("bl-stat-chip");
            chip.Add(UiIcons.Icon(icon, "bl-stat-icon"));
            var l = new Label(effective.ToString()) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(main ? "bl-stat-main" : "bl-stat");
            if (effective > baseValue) l.AddToClassList("ui-up");
            else if (effective < baseValue) l.AddToClassList("ui-down");
            chip.Add(l);
            return chip;
        }

        private enum PileKind { Draw, Discard }

        private Button PileButton(string icon, PileKind kind, out Label count)
        {
            var b = new Button(() => ShowPile(kind));
            b.AddToClassList("bl-pile-btn");
            b.Add(UiIcons.Icon(icon, "bl-pile-icon"));
            count = new Label("0") { pickingMode = PickingMode.Ignore };
            count.AddToClassList("bl-pile-count");
            b.Add(count);
            return b;
        }

        private void ShowPile(PileKind kind)
        {
            ClosePile();
            var overlay = new VisualElement();
            overlay.AddToClassList("pv-overlay");
            overlay.RegisterCallback<ClickEvent>(e => { if (e.target == overlay) ClosePile(); });

            var panel = new VisualElement();
            panel.AddToClassList("pv-panel");
            var title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.AddToClassList("pv-title-row");
            string name = kind == PileKind.Draw ? "抽牌堆" : "棄牌堆";
            string note = kind == PileKind.Draw ? "（抽完不會重洗，順序不公開）" : "（用過的牌不會回到牌堆）";
            title.Add(new Label(name).WithClass("pv-title"));
            title.Add(new Label(note).WithClass("pv-note"));
            var close = new Button(ClosePile) { text = "關閉" };
            close.AddToClassList("pv-close");
            title.Add(close);
            panel.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pv-scroll");
            scroll.contentContainer.AddToClassList("pv-grid");

            var cards = kind == PileKind.Draw ? _battle.DrawPile : _battle.DiscardPile;
            AddPileGroup(scroll, cards, "");
            if (cards.Count == 0)
                scroll.Add(new Label(kind == PileKind.Draw ? "抽牌堆已空" : "還沒有用過的牌").WithClass("pv-empty"));
            panel.Add(scroll);
            overlay.Add(panel);
            _content.Add(overlay);
            _pileView = overlay;
        }

        private void ClosePile()
        {
            if (_pileView == null) return;
            _pileView.RemoveFromHierarchy();
            _pileView = null;
        }

        private void AddPileGroup(VisualElement parent, List<CardInstance> cards, string tag)
        {
            foreach (var g in cards
                .GroupBy(c => (Owner: c.Owner?.Name ?? "", c.Def.Id))
                .OrderBy(g => g.Key.Owner == "" ? 1 : 0).ThenBy(g => g.Key.Owner).ThenBy(g => g.First().Def.Cost).ThenBy(g => g.First().Def.Name))
            {
                var card = g.First();
                var tile = new VisualElement { pickingMode = PickingMode.Ignore };
                tile.AddToClassList("pv-card");
                if (card.Def.Target == TargetRule.MoveDest) tile.AddToClassList("pv-card-move");
                else if (!card.Def.Basic) tile.AddToClassList("pv-card-skill");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("pv-card-head");
                head.Add(Face(card.Owner, "pv-card-face"));
                var titles = new VisualElement { pickingMode = PickingMode.Ignore };
                titles.AddToClassList("pv-card-titles");
                titles.Add(new Label(card.Def.Name).WithClass("pv-card-name"));
                titles.Add(new Label(card.Owner == null ? "全隊通用" : card.Owner.Name).WithClass("pv-card-owner"));
                head.Add(titles);
                var cost = new Label(card.Def.Cost.ToString()) { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("bl-row-cost");
                var costIcon = UiIcons.Get("cost");
                if (costIcon != null) cost.style.backgroundImage = new StyleBackground(costIcon);
                head.Add(cost);
                tile.Add(head);

                string desc = CardText.Description(card.Def);
                tile.Add(new Label(CardText.Target(card.Def, card.Owner != null ? card.Owner.AttackRange : 1)).WithClass("pv-card-target"));
                if (desc.Length > 0) tile.Add(new Label(desc).WithClass("pv-card-desc"));
                if (g.Count() > 1) tile.Add(new Label("×" + g.Count()).WithClass("pv-count"));
                if (tag.Length > 0) tile.Add(new Label(tag).WithClass("pv-tag"));
                parent.Add(tile);
            }
        }

        private void RefreshUnitList()
        {
            _unitList.Clear();
            var tabs = new VisualElement().WithClass("ul-tabs");
            foreach (var (label, side) in new[] { ("我方", Side.Player), ("敵人", Side.Enemy) })
            {
                var captured = side;
                var tab = new Button(() => { _listSide = captured; HideUnitInfo(); RefreshUnitList(); }) { text = label };
                tab.AddToClassList("ul-tab");
                tab.AddToClassList(side == Side.Player ? "ul-tab-ally" : "ul-tab-enemy");
                tab.EnableInClassList("ul-tab-on", side == _listSide);
                tabs.Add(tab);
            }
            _unitList.Add(tabs);
            foreach (var unit in _battle.Units.Where(u => u.Side == _listSide))
            {
                var captured = unit;
                var row = new VisualElement().WithClass("ul-row");
                row.EnableInClassList("ul-row-dead", !unit.Alive);
                row.EnableInClassList("ul-row-on", unit == _infoUnit);
                var face = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("ul-face");
                var portrait = HeroArt.Face(unit.DefId);
                if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
                else
                {
                    string roleIcon = unit.Hero != null ? UiIcons.RoleIcon(unit.Hero.Role) : unit.AttackType == AttackType.Ranged ? "role_archer" : "role_warrior";
                    var icon = UiIcons.Get(roleIcon);
                    if (icon != null) face.style.backgroundImage = new StyleBackground(icon);
                }
                row.Add(face);
                var column = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("ul-col");
                column.Add(new Label(unit.Name) { pickingMode = PickingMode.Ignore }.WithClass("ul-name"));
                column.Add(new Label($"{unit.Hp}/{unit.MaxHp}") { pickingMode = PickingMode.Ignore }.WithClass("ul-hp-text"));
                var bar = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("ul-hp-bg");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("ul-hp-fill");
                if (unit.Side == Side.Enemy) fill.AddToClassList("ul-hp-fill-enemy");
                fill.style.width = Length.Percent(unit.MaxHp <= 0 ? 0f : Mathf.Clamp01(unit.Hp / (float)unit.MaxHp) * 100f);
                bar.Add(fill);
                column.Add(bar);
                row.Add(column);
                row.RegisterCallback<ClickEvent>(_ => ToggleUnitInfo(captured, row));
                _unitList.Add(row);
            }
        }

        private void RefreshHud()
        {
            string where = _dungeon != null && _recorder != null ? _dungeon.Name
                : IsWorldBoss && _recorder != null ? $"世界 Boss　{WorldBoss.BossOf(WorldBoss.SeasonOf(GameSession.View.Now)).Name}"
                : $"{_chapter}-{_level}　{Campaign.LevelName(_chapter, _level)}";
            _title.text = _battle.Setup.TurnLimit > 0
                ? $"{where}　第 {_battle.Turn} / {_battle.Setup.TurnLimit} 回合"
                : $"{where}　第 {_battle.Turn} 回合";
            _cost.Clear();
            var orb = new VisualElement { pickingMode = PickingMode.Ignore };
            orb.AddToClassList("bl-orb");
            var orbIcon = UiIcons.Get("cost");
            if (orbIcon != null) orb.style.backgroundImage = new StyleBackground(orbIcon);
            _cost.Add(orb);
            _cost.Add(new Label($"{_battle.Cost}") { pickingMode = PickingMode.Ignore }.WithClass("bl-cost-num"));
            _cost.Add(new Label($"/ {_battle.Setup.CostCap}") { pickingMode = PickingMode.Ignore }.WithClass("bl-cost-cap"));
            _drawCount.text = _battle.DrawPile.Count.ToString();
            _discardCount.text = _battle.DiscardPile.Count.ToString();
            _logLabel.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 4)));
            _logBox.style.display = DisplayStyle.None;
        }

        private void RefreshOverlay()
        {
            if (_battle.Result == BattleResult.Ongoing || _overlay != null || _recorder == null || _finishing) return;
            _finishing = true;
            _overlay = new VisualElement();
            _overlay.AddToClassList("overlay");
            var wait = new Label("結算中…");
            wait.AddToClassList("overlay-text");
            _overlay.Add(wait);
            _root.Add(_overlay);
            var actions = _recorder.Actions.ToList();
            _ = FinishStage(_overlay, _stageId, () => GameSession.Backend.FinishStage(_stageId, actions));
        }

        private void DebugWin()
        {
            if (_recorder == null || _overlay != null || _finishing || _busy) return;
            _finishing = true;
            _overlay = new VisualElement();
            _overlay.AddToClassList("overlay");
            var wait = new Label("結算中…");
            wait.AddToClassList("overlay-text");
            _overlay.Add(wait);
            _root.Add(_overlay);
            _ = FinishStage(_overlay, _stageId, () => GameSession.Backend.DebugWin(_stageId));
        }
    }
}
