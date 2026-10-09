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
        private void OnCardClicked(CardInstance card)
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
            if (_pendingCard == card) { CancelTargeting(); return; }
            CancelTargeting();
            if (!_battle.Hand.Contains(card)) return;
            var rule = card.Def.Target;
            if (rule != TargetRule.Enemy && rule != TargetRule.Ally && rule != TargetRule.MoveDest) { PlayCardAt(card, null, null); return; }
            BeginPending(card);
        }

        private void BeginPending(CardInstance card)
        {
            _pendingCard = card;
            _pendingMover = null;
            ShowCardRange(card);
            RefreshCards();
            RefreshDetail();
        }

        private void PlayCardAt(CardInstance card, Position? target, Unit? mover)
        {
            var result = _recorder!.Play(card, target, mover);
            if (result != PlayResult.Ok) { Toast(Explain(result)); Refresh(); return; }
            _pendingCard = null;
            _pendingMover = null;
            ClearPreview();
            PumpEvents();
            Refresh();
        }

        private void CancelTargeting()
        {
            if (_pendingCard == null) return;
            _pendingCard = null;
            _pendingMover = null;
            ClearPreview();
            RefreshTiles();
            RefreshCards();
            RefreshDetail();
        }

        private void OnFieldClicked(ClickEvent evt)
        {
            if (_dragged) { _dragged = false; return; }
            var card = _pendingCard;
            if (card == null || _battle.Result != BattleResult.Ongoing || Blocked) return;
            if (!_stage.TryPick(evt.position, out var pos))
            {
                CancelTargeting();
                return;
            }
            if (card.Def.Target == TargetRule.MoveDest)
            {
                var hero = _battle.UnitAt(Side.Player, pos);
                if (hero != null && hero.Alive) { PickMover(card, hero); return; }
                if (_pendingMover == null) { Toast("先點選要移動的武將（棋盤上的武將或底部武將列）"); return; }
                if (!_battle.ReachableTiles(_pendingMover).ContainsKey(pos)) { Toast("請點選綠色的可移動格"); return; }
                PlayCardAt(card, pos, _pendingMover);
                return;
            }
            if (card.Def.Target != TargetRule.Enemy && card.Def.Target != TargetRule.Ally) return;
            var owner = card.Owner!;
            bool selfOk = card.Def.Target == TargetRule.Ally;
            if (!_battle.InBounds(pos) || (pos == owner.Pos && !selfOk) || Position.Distance(owner.Pos, pos) > SanGuo.Core.Battle.CardRange(owner, card.Def))
            {
                Toast("請點選射程內有敵人的格子");
                return;
            }
            PlayCardAt(card, pos, null);
        }

        private void PickMover(CardInstance card, Unit hero)
        {
            if (!_battle.CanMoveUnit(hero)) { Toast("這名武將現在不能移動"); return; }
            _pendingMover = hero;
            ShowCardRange(card);
            RefreshDetail();
        }

        private bool _dragging, _dragged;
        private Vector2 _dragStart, _dragLast;

        private void OnFieldDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2) return;
            _dragging = true;
            _dragged = false;
            _dragStart = _dragLast = evt.position;
            _field.CapturePointer(evt.pointerId);
        }

        private void OnFieldMove(PointerMoveEvent evt)
        {
            if (_dragging)
            {
                if (!_dragged && ((Vector2)evt.position - _dragStart).magnitude > 8f) _dragged = true;
                if (_dragged)
                {
                    _stage.PanBy((Vector2)evt.position - _dragLast);
                    _dragLast = evt.position;
                }
            }
        }

        private void OnFieldUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_field.HasPointerCapture(evt.pointerId)) _field.ReleasePointer(evt.pointerId);
        }

        private void OnFieldWheel(WheelEvent evt)
        {
            _stage.ZoomBy(Mathf.Pow(1.12f, -Mathf.Sign(evt.delta.y)));
            evt.StopPropagation();
        }

        private void ToggleUnitInfo(Unit unit, VisualElement row)
        {
            if (_infoUnit == unit) { HideUnitInfo(); return; }
            if (!unit.Alive) return;
            _infoUnit = unit;
            RenderUnitInfo(unit);
            var local = _content.WorldToLocal(row.worldBound.position);
            float top = Mathf.Clamp(local.y, 8f, Mathf.Max(8f, _content.layout.height - 440f));
            _unitInfo.style.left = _unitList.layout.xMax + 10f;
            _unitInfo.style.top = top;
            _unitInfo.style.display = DisplayStyle.Flex;
            _unitInfo.BringToFront();
            RefreshUnitList();
        }

        private void HideUnitInfo()
        {
            if (_infoUnit == null) return;
            _infoUnit = null;
            _unitInfo.style.display = DisplayStyle.None;
            RefreshUnitList();
        }

        private void RenderUnitInfo(Unit unit)
        {
            _unitInfo.Clear();
            _unitInfo.EnableInClassList("unit-info-hero", unit.Side == Side.Player);
            _unitInfo.EnableInClassList("unit-info-enemy", unit.Side == Side.Enemy);

            string roleIcon = unit.Hero != null ? UiIcons.RoleIcon(unit.Hero.Role) : unit.AttackType == AttackType.Ranged ? "role_archer" : "role_warrior";
            string roleName = unit.Hero != null ? CardText.RoleName(unit.Hero.Role) : unit.Side == Side.Enemy ? "敵軍" : "";
            var title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.AddToClassList("ui-title");
            title.Add(UiIcons.Icon(roleIcon, "icon-sm"));
            title.Add(new Label(unit.Protected ? $"{unit.Name}（保護目標）" : unit.Name) { pickingMode = PickingMode.Ignore }.WithClass("ui-name"));
            title.Add(new Label(roleName) { pickingMode = PickingMode.Ignore }.WithClass("ui-role"));
            _unitInfo.Add(title);

            string hp = $"生命 {unit.Hp}/{unit.MaxHp}" + (unit.Shield > 0 ? $"　護盾 {unit.Shield}" : "");
            _unitInfo.Add(new Label(hp) { pickingMode = PickingMode.Ignore }.WithClass("ui-hp"));

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("ui-grid");
            var st = unit.Stats;
            bool caster = IsCaster(unit);
            AddStat(grid, "stat_" + (caster ? "int" : "atk"), caster ? st.Int : st.Atk, caster ? unit.EffectiveInt : unit.EffectiveAtk);
            AddStat(grid, "stat_" + (caster ? "atk" : "int"), caster ? st.Atk : st.Int, caster ? unit.EffectiveAtk : unit.EffectiveInt);
            AddStat(grid, "stat_def", st.Def, (int)Math.Round(unit.EffectiveDef));
            AddStat(grid, "stat_move", st.Move, st.Move);
            AddStat(grid, "射程", unit.AttackRange, unit.AttackRange);
            AddStat(grid, "閃避", st.Dodge, unit.EffectiveDodge, "%");
            AddStat(grid, "暴擊", st.Crit, unit.EffectiveCrit, "%");
            AddStat(grid, "暴傷", st.CritDmg, st.CritDmg, "%");
            if (unit.BurnResist > 0)
            {
                int resist = (int)Math.Round(unit.BurnResist * 100);
                AddStat(grid, "燃燒抗性", resist, resist, "%");
            }
            _unitInfo.Add(grid);

            _unitInfo.Add(new Label("增減益") { pickingMode = PickingMode.Ignore }.WithClass("ui-sec"));
            int before = _unitInfo.childCount;
            foreach (var kv in unit.Statuses) AddStatusRow(UiIcons.Status(kv.Key), StatusLine(kv.Key, kv.Value));
            foreach (var b in unit.Buffs) AddStatusRow(UiIcons.Status(b.Type), BuffLine(b));
            foreach (var br in unit.DefBreaks) AddStatusRow("status_armorbreak", $"破甲　防禦 -{br.Percent * 100:0}%・剩 {br.Turns} 回合");
            if (unit.Side == Side.Enemy && unit.Charging) AddStatusRow("charge", "蓄力中");
            if (_unitInfo.childCount == before)
                _unitInfo.Add(new Label("目前沒有增減益") { pickingMode = PickingMode.Ignore }.WithClass("ui-none"));
            if(unit.Side==Side.Enemy)
            {
                _unitInfo.Add(new Label("行動預告") {pickingMode=PickingMode.Ignore}.WithClass("ui-sec"));
                var intentRow=new VisualElement {pickingMode=PickingMode.Ignore};
                intentRow.style.flexDirection=FlexDirection.Row;
                FillIntent(intentRow,unit);_unitInfo.Add(intentRow);
            }
        }

        private static void AddStat(VisualElement grid, string name, int baseValue, int effective, string suffix = "")
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ui-stat");
            if (name.StartsWith("stat_")) row.Add(UiIcons.Icon(name, "ui-stat-icon"));
            else row.Add(new Label(name) { pickingMode = PickingMode.Ignore }.WithClass("ui-stat-name"));
            var val = new Label(effective == baseValue ? $"{effective}{suffix}" : $"{effective}{suffix}（{baseValue}）") { pickingMode = PickingMode.Ignore };
            val.AddToClassList("ui-stat-val");
            if (effective > baseValue) val.AddToClassList("ui-up");
            else if (effective < baseValue) val.AddToClassList("ui-down");
            grid.Add(row);
            row.Add(val);
        }

        private void AddStatusRow(string icon, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ui-status");
            row.Add(UiIcons.Icon(icon, "icon-sm"));
            row.Add(new Label(text) { pickingMode = PickingMode.Ignore }.WithClass("ui-status-text"));
            _unitInfo.Add(row);
        }

        private static string StatusLine(StatusType type, StatusState state)
        {
            string name = CardText.StatusName(type);
            switch (type)
            {
                case StatusType.Burn: return $"{name}　{state.Power} 層，回合結束受 {state.Power} 點傷害後層數減半";
                case StatusType.Taunt: return $"{name}　被迫以嘲諷者為目標・剩 {state.Turns} 回合";
                default: return $"{name}・剩 {state.Turns} 回合";
            }
        }

        private static string BuffLine(Buff b)
        {
            string name = CardText.StatusName(b.Type);
            string value = b.Type == StatusType.AtkUp || b.Type == StatusType.IntUp ? $"+{b.Power}%" : $"+{b.Power}";
            return $"{name}　{value}・剩 {b.Turns} 回合";
        }

        private static bool IsCaster(Unit unit) =>
            unit.Hero != null ? unit.Hero.Role == Role.Mage || unit.Hero.Role == Role.Strategist || unit.Hero.Role == Role.Healer : unit.Magical;

        private static string ChipText(StatusType type, StatusState state) => type == StatusType.Burn ? state.Power.ToString() : state.Turns.ToString();

        private static string Explain(PlayResult result)
        {
            switch (result)
            {
                case PlayResult.NotEnoughCost: return "費用不足";
                case PlayResult.NoTarget: return "射程內沒有目標，無法打出（先用「移動」卡走位）";
                case PlayResult.OutOfRange: return "超出射程或無法到達那裡";
                case PlayResult.InvalidMover: return "這名武將現在不能移動";
                case PlayResult.OwnerDead: return "該武將已陣亡";
                case PlayResult.BattleOver: return "戰鬥已結束";
                default: return result.ToString();
            }
        }

        private void Preview(CardInstance? card)
        {
            if (_pendingCard != null) return;
            ClearPreview();
            if (card != null && _battle.CanPlay(card) != PlayResult.NotInHand) ShowCardRange(card);
            RefreshTiles();
        }

        private void ClearPreview()
        {
            _previewTargets.Clear();
            _previewRange.Clear();
            _previewReach.Clear();
        }

        private void ShowCardRange(CardInstance card)
        {
            ClearPreview();
            var def = card.Def;
            if (def.Target == TargetRule.MoveDest)
            {
                if (_pendingMover != null)
                {
                    foreach (var kv in _battle.ReachableTiles(_pendingMover))
                        if (kv.Value > 0) _previewReach.Add(kv.Key);
                }
                else
                {
                    foreach (var u in _battle.AliveUnits(Side.Player))
                        if (_battle.CanMoveUnit(u)) _previewTargets.Add(u.Pos);
                }
            }
            else if (card.Owner != null)
            {
                var owner = card.Owner;
                if (def.Target == TargetRule.Enemy || def.Target == TargetRule.Ally)
                {
                    for (int lane = 0; lane < _battle.Setup.Lanes; lane++)
                        for (int row = 0; row < _battle.Setup.Rows; row++)
                        {
                            var p = new Position(lane, row);
                            if (p != owner.Pos && Position.Distance(owner.Pos, p) <= SanGuo.Core.Battle.CardRange(owner, def)) _previewRange.Add(p);
                        }
                }
                if (def.Target != TargetRule.Enemy && def.Target != TargetRule.Ally)
                {
                    var targets = _battle.ResolveTargets(owner, def);
                    if (targets != null)
                        foreach (var u in targets) _previewTargets.Add(u.Pos);
                }
                else
                {
                    foreach (var u in _battle.AliveUnits(Side.Enemy))
                        if (Position.Distance(owner.Pos, u.Pos) <= SanGuo.Core.Battle.CardRange(owner, def)) _previewTargets.Add(u.Pos);
                }
            }
            RefreshTiles();
        }
    }
}
