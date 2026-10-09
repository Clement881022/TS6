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
            RefreshHeroBar();
            RefreshHud();
            RefreshOverlay();
            if (_hoverUnit != null)
            {
                if (_hoverUnit.Alive) RenderUnitInfo(_hoverUnit);
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

                tag.Name.text = unit.Protected ? $"{unit.Name} 保護目標" : unit.Name;
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
            if (_tagLayer == null || _field.resolvedStyle.width < 1) return;
            float width = _tagLayer.resolvedStyle.width, height = _tagLayer.resolvedStyle.height;
            if (width < 1 || height < 1) return;
            var occupied = new List<Rect>();
            foreach(var panel in new[]{_logBox,_detail,_unitInfo})
            {
                if(panel.resolvedStyle.display==DisplayStyle.None || panel.worldBound.width<1 || panel.worldBound.height<1)continue;
                var min=_tagLayer.WorldToLocal(panel.worldBound.min);
                var max=_tagLayer.WorldToLocal(panel.worldBound.max);
                occupied.Add(Rect.MinMaxRect(min.x-4,min.y-4,max.x+4,max.y+4));
            }
            var silhouettes = new List<Rect>();
            foreach(var living in _battle.Units.Where(u=>u.Alive))
            {
                var headPoint=_stage.UnitHeadPanel(living);var footPoint=_stage.UnitFootPanel(living);
                if(headPoint==null || footPoint==null)continue;
                var head=_tagLayer.WorldToLocal(headPoint.Value);var foot=_tagLayer.WorldToLocal(footPoint.Value);
                float bodyHeight=Mathf.Abs(foot.y-head.y);
                float halfWidth=bodyHeight*.22f;
                silhouettes.Add(Rect.MinMaxRect(Mathf.Min(head.x,foot.x)-halfWidth,
                    Mathf.Min(head.y,foot.y),Mathf.Max(head.x,foot.x)+halfWidth,Mathf.Max(head.y,foot.y)));
            }
            foreach (var unit in _battle.Units.OrderBy(u => u.Side == Side.Enemy ? 0 : 1))
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                bool below = unit.Side == Side.Player;
                var point = unit.Alive ? (below ? _stage.UnitFootPanel(unit) : _stage.UnitHeadPanel(unit)) : null;
                if (point == null)
                {
                    tag.Root.style.visibility = Visibility.Hidden;
                    if(tag.AnchorLine!=null)tag.AnchorLine.style.visibility=Visibility.Hidden;
                    continue;
                }
                var anchor = _tagLayer.WorldToLocal(point.Value);
                if (anchor.x < 0 || anchor.x > width || anchor.y < 0 || anchor.y > height)
                {
                    tag.Root.style.visibility = Visibility.Hidden;
                    if(tag.AnchorLine!=null)tag.AnchorLine.style.visibility=Visibility.Hidden;
                    continue;
                }
                float tagWidth = below ? HeroTagWidth : TagWidth;
                float measuredHeight = tag.Root.resolvedStyle.height;
                float tagHeight = float.IsNaN(measuredHeight) || measuredHeight < 1 ? (below ? 9f : 82f) : measuredHeight;
                var desired = new Vector2(anchor.x - tagWidth * 0.5f, below ? anchor.y + 5f : anchor.y - tagHeight - 4f);
                Rect placement = default;
                bool found = false;
                for (int ring = 0; ring < (below ? 2 : 3) && !found; ring++)
                    for (int direction = 0; direction < (ring == 0 ? 1 : 4) && !found; direction++)
                    {
                        float dx = below ? 0 : direction == 2 ? -ring * (tagWidth + 6) : direction == 3 ? ring * (tagWidth + 6) : 0;
                        float dy = direction == 0 ? -ring * (tagHeight + 6) : direction == 1 ? ring * (tagHeight + 6) : 0;
                        placement = new Rect(Mathf.Clamp(desired.x + dx, 4, Mathf.Max(4, width - tagWidth - 4)),
                            Mathf.Clamp(desired.y + dy, 4, Mathf.Max(4, height - tagHeight - 4)), tagWidth, tagHeight);
                        found = !occupied.Any(r => r.Overlaps(placement)) && !silhouettes.Any(r=>r.Overlaps(placement));
                    }
                tag.Root.style.visibility = found ? Visibility.Visible : Visibility.Hidden;
                if(tag.AnchorLine!=null)
                {
                    tag.AnchorLine.style.visibility=found ? Visibility.Visible : Visibility.Hidden;
                    if(found)
                    {
                        var start=new Vector2(Mathf.Clamp(anchor.x,placement.xMin,placement.xMax),Mathf.Clamp(anchor.y,placement.yMin,placement.yMax));
                        var delta=anchor-start;
                        tag.AnchorLine.style.left=start.x;tag.AnchorLine.style.top=start.y;
                        tag.AnchorLine.style.height=delta.magnitude;
                        tag.AnchorLine.style.rotate=new Rotate(new Angle(Mathf.Atan2(-delta.x,delta.y)*Mathf.Rad2Deg));
                    }
                }
                if (!found) continue;
                occupied.Add(placement);
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
                    if (intent.MoveTo != null) host.Add(UiIcons.Chip("draw", "→"));
                    host.Add(UiIcons.Chip("damage", intent.Target!.Name));
                    break;
                case Intent.Kind.Charge:
                case Intent.Kind.Charging: host.Add(UiIcons.Chip("charge", "蓄力中")); break;
                case Intent.Kind.Move: host.Add(UiIcons.Chip("draw", "逼近")); break;
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

                art.Add(UiIcons.Icon(KindIcon(card.Def), "sts-card-kind"));
                row.Add(art);

                var description = CardText.Description(card.Def);
                var summary = card.Def.Target == TargetRule.MoveDest ? "" : CardText.Summary(card.Def);
                row.Add(new Label(card.Def.Target == TargetRule.MoveDest ? "" : summary.Length > 0 ? summary : CardText.Target(card.Def, card.Owner?.AttackRange ?? 1)) { pickingMode = PickingMode.Ignore }.WithClass("sts-card-description"));

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
            var card = _pendingCard;
            _detail.Clear();
            if (card == null || !_battle.Hand.Contains(card)) { _detail.style.display = DisplayStyle.None; _field.style.marginRight = 18f; return; }
            _detail.style.display = DisplayStyle.Flex;
            _field.style.marginRight = 354f;
            var def = card.Def;

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("bl-d-head");
            head.Add(Face(card.Owner, "bl-d-face"));
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.AddToClassList("bl-d-titles");
            titles.Add(new Label(def.Name) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-name"));
            titles.Add(new Label(card.Owner == null ? "全隊通用" : card.Owner.Name + (card.Owner.Alive ? "" : "（陣亡）")) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-owner"));
            head.Add(titles);
            var cost = new Label(def.Cost.ToString()) { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("bl-row-cost");
            var costIcon = UiIcons.Get("cost");
            if (costIcon != null) cost.style.backgroundImage = new StyleBackground(costIcon);
            head.Add(cost);
            _detail.Add(head);

            int attackRange = card.Owner != null ? SanGuo.Core.Battle.CardRange(card.Owner, def) : 1;
            _detail.Add(RangeIcon.Build(def, attackRange));
            _detail.Add(new Label(CardText.Target(def, attackRange)) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-target"));
            var desc = CardText.Description(def);
            if (desc.Length > 0) _detail.Add(new Label(desc) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-desc"));

            var check = _battle.CanPlay(card);
            string hint;
            if (check != PlayResult.Ok) hint = Explain(check);
            else if (def.Target == TargetRule.MoveDest)
                hint = _pendingMover == null ? "先點選要移動的武將（棋盤或底部武將列），再點綠色格子" : $"移動 {_pendingMover.Name}：點選綠色格子";
            else if (def.Target == TargetRule.Enemy) hint = "點選棋盤上射程內的敵人施放（範圍內沒有敵人不可施放）";
            else if (def.Target == TargetRule.Ally) hint = "點選射程內的隊友，或直接按「使用」（自動選血量比例最低者）";
            else hint = "";
            if (hint.Length > 0) _detail.Add(new Label(hint) { pickingMode = PickingMode.Ignore }.WithClass(check == PlayResult.Ok ? "bl-d-hint" : "bl-d-warn"));

            if (def.Target != TargetRule.MoveDest && def.Target != TargetRule.Enemy)
            {
                var use = new Button(UseSelected) { text = "使用" };
                use.AddToClassList("bl-d-use");
                use.SetEnabled(check == PlayResult.Ok);
                _detail.Add(use);
            }
            var cancel = new Button(CancelTargeting) { text = "取消" };
            cancel.AddToClassList("bl-d-cancel");
            _detail.Add(cancel);
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

        private void RefreshHeroBar()
        {
            _heroBar.Clear();
            foreach (var unit in _battle.Units.Where(u => u.Side == Side.Player))
            {
                var captured = unit;
                var box = new VisualElement();
                box.AddToClassList("bl-hero");
                if (!unit.Alive) box.AddToClassList("bl-hero-dead");
                if (unit == _pendingMover || (_pendingCard?.Owner == unit)) box.AddToClassList("bl-hero-active");
                else if (_pendingCard?.Def.Target == TargetRule.MoveDest && _pendingMover == null && _battle.CanMoveUnit(unit)) box.AddToClassList("bl-hero-pick");

                box.Add(Face(unit, "bl-hero-face"));
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.AddToClassList("bl-hero-col");

                var title = new VisualElement { pickingMode = PickingMode.Ignore };
                title.AddToClassList("bl-hero-title");
                string roleIcon = unit.Hero != null ? UiIcons.RoleIcon(unit.Hero.Role) : "role_warrior";
                title.Add(UiIcons.Icon(roleIcon, "icon-sm"));
                title.Add(new Label(unit.Name) { pickingMode = PickingMode.Ignore }.WithClass("bl-hero-name"));
                if (unit.Hero != null) title.Add(new Label(CardText.RoleName(unit.Hero.Role)) { pickingMode = PickingMode.Ignore }.WithClass("bl-hero-role"));
                col.Add(title);

                var hpBg = new VisualElement { pickingMode = PickingMode.Ignore };
                hpBg.AddToClassList("bl-hp-bg");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("bl-hp-fill");
                fill.style.width = Length.Percent(unit.MaxHp <= 0 ? 0f : Mathf.Clamp01(unit.Hp / (float)unit.MaxHp) * 100f);
                hpBg.Add(fill);
                hpBg.Add(new Label($"{unit.Hp}/{unit.MaxHp}" + (unit.Shield > 0 ? $"  盾 {unit.Shield}" : "")) { pickingMode = PickingMode.Ignore }.WithClass("bl-hp-text"));
                col.Add(hpBg);

                var stats = new VisualElement { pickingMode = PickingMode.Ignore };
                stats.AddToClassList("bl-stats");
                stats.Add(StatChip("stat_atk", unit.EffectiveAtk, unit.Stats.Atk, main: !IsCaster(unit)));
                stats.Add(StatChip("stat_int", unit.EffectiveInt, unit.Stats.Int, main: IsCaster(unit)));
                stats.Add(StatChip("stat_def", (int)Math.Round(unit.EffectiveDef), unit.Stats.Def));
                stats.Add(StatChip("stat_move", unit.Stats.Move, unit.Stats.Move));
                col.Add(stats);

                var chips = new VisualElement { pickingMode = PickingMode.Ignore };
                chips.AddToClassList("bl-chips");
                foreach (var st in unit.Statuses) chips.Add(UiIcons.Chip(UiIcons.Status(st.Key), ChipText(st.Key, st.Value)));
                foreach (var b in unit.Buffs) chips.Add(UiIcons.Chip(UiIcons.Status(b.Type), $"{b.Power}·{b.Turns}"));
                foreach (var br in unit.DefBreaks) chips.Add(UiIcons.Chip("status_armorbreak", $"{br.Percent * 100:0}%·{br.Turns}"));
                col.Add(chips);

                box.Add(col);
                box.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_pendingCard != null && _pendingCard.Def.Target == TargetRule.MoveDest && captured.Alive) PickMover(_pendingCard, captured);
                });
                _heroBar.Add(box);
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
            _logBox.style.display = _log.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
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
