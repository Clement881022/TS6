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
        private async Task FinishStage(VisualElement overlay, string stageId, Func<Task<FinishStageResult>> settle)
        {
            FinishStageResult result;
            try
            {
                result = await settle();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = new FinishStageResult { Code = "network" };
            }
            if (_overlay != overlay) return;
            await GameSession.Refresh();
            if (_overlay != overlay) return;

            overlay.Clear();
            if (!result.Ok)
            {
                var err = new Label(UiText.ExplainBackend(result.Code));
                err.AddToClassList("overlay-text");
                overlay.Add(err);
                overlay.Add(MakeButton(IsWorldBoss ? "返回" : "回地圖", Leave, primary: true));
                return;
            }

            var card = new VisualElement();
            card.AddToClassList("result-card");
            overlay.Add(card);
            if (stageId == WorldBoss.StageId)
            {
                card.Add(new Label(result.Won ? "擊倒 Boss！" : "挑戰結束").WithClass("overlay-text"));
                AddInfo(card, $"造成傷害 {result.Damage:N0}" + (result.NewBest ? "　刷新本季最佳！" : ""));
                AddInfo(card, $"本季最佳 {result.BestDamage:N0}" + (result.Rank > 0 ? $"　目前第 {result.Rank} 名 / {result.Total} 人" : ""));
                var wbRow = new VisualElement();
                wbRow.AddToClassList("result-buttons");
                wbRow.Add(MakeButton("回世界 Boss", () => Nav.Go(Page.WorldBoss), primary: true));
                card.Add(wbRow);
                return;
            }
            var text = new Label(result.Won ? "勝利" : "敗北");
            text.AddToClassList("overlay-text");
            card.Add(text);
            var btnRow = new VisualElement();
            btnRow.AddToClassList("result-buttons");
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (result.Won && dungeon != null)
            {
                var gains = new List<string>();
                if (result.Gold > 0) gains.Add($"金幣 +{result.Gold}");
                if (result.Yuanbao > 0) gains.Add($"元寶 +{result.Yuanbao}");
                foreach (var m in result.Materials) gains.Add($"{UiText.MaterialName(m.Key)} +{m.Value}");
                AddInfo(card, "獎勵：" + string.Join("　", gains));
            }
            else if (result.Won)
            {
                var stars = new VisualElement();
                stars.AddToClassList("result-stars");
                for (int i = 0; i < 3; i++)
                {
                    var star = new VisualElement();
                    star.AddToClassList("result-star");
                    if (i < result.Stars) star.AddToClassList("result-star-on");
                    stars.Add(star);
                }
                card.Add(stars);
                if (result.FirstClear) AddInfo(card, "首次通關");
                if (result.HeroGained != "") AddInfo(card, "獲得武將：" + (DemoContent.Roster().Find(h => h.Id == result.HeroGained)?.Name ?? result.HeroGained));
                if (result.DuplicatesGained.Count > 0)
                    AddInfo(card, "突破材料：" + string.Join("、", result.DuplicatesGained.Select(id => GameSession.DefOf(id)?.Name ?? id)) + " 各 +1");
                AddInfo(card, $"經驗 +{result.Exp}　金幣 +{result.Gold}" + (result.Yuanbao > 0 ? $"　元寶 +{result.Yuanbao}" : ""));
                if (result.LevelsGained > 0) AddInfo(card, $"帳號升級！Lv.{GameSession.View.Level}（體力已回滿）");
            }

            if (result.Won && result.FirstClear && dungeon == null)
                StoryPlayer.ShowAfter(_root, _chapter, _level);

            if (dungeon != null)
            {
                btnRow.Add(MakeButton("再打一次", () => _ = BeginStageId(stageId), primary: true));
                btnRow.Add(MakeButton("回副本", () => Nav.Go(Page.Dungeons)));
                card.Add(btnRow);
                return;
            }
            int chapter = _chapter, level = _level;
            bool hasNext = Campaign.Next(chapter, level, out int nextChapter, out int nextLevel) && result.Won;
            if (hasNext) btnRow.Add(MakeButton(nextChapter != chapter ? "下一章" : "下一關", () => EnterLevel(nextChapter, nextLevel), primary: true));
            btnRow.Add(MakeButton("再打一次", () => EnterLevel(chapter, level), primary: !hasNext));
            btnRow.Add(MakeButton("回地圖", () => Nav.Go(Page.Map)));
            card.Add(btnRow);
        }

        private void PumpEvents()
        {
            for (; _eventCursor < _battle.Events.Count; _eventCursor++)
            {
                var ev = _battle.Events[_eventCursor];
                var text = Describe(ev);
                if (text != null) _log.Add(text);
                _fx.Play(ev);
            }
        }

        private string NameOf(int id) => id >= 0 && id < _battle.Units.Count ? _battle.Units[id].Name : "狀態";

        private string? Describe(BattleEvent e)
        {
            switch (e.Type)
            {
                case EventType.TurnStart: return $"── 第 {e.Value} 回合 ──";
                case EventType.CardPlayed: return $"{NameOf(e.Source)} 打出《{e.Text}》";
                case EventType.Damage:
                    return $"{NameOf(e.Source)} → {NameOf(e.Target)}　傷害 {e.Value}{(e.Text == "crit" ? "（爆擊）" : "")}";
                case EventType.Dodge: return $"{NameOf(e.Target)} 閃避了攻擊";
                case EventType.Heal: return e.Value > 0 ? $"{NameOf(e.Target)} 回復 {e.Value}" : $"{NameOf(e.Target)} 血量已滿";
                case EventType.Shield: return $"{NameOf(e.Target)} 獲得護盾 {e.Value}";
                case EventType.StatusApplied: return $"{NameOf(e.Target)} 受到 {StatusFromText(e.Text)}";
                case EventType.Draw: return $"抽了 {e.Value} 張牌";
                case EventType.GainCost: return e.Text == "kill" ? $"擊敗目標，回 {e.Value} 費" : $"獲得 {e.Value} 費";
                case EventType.PassiveTriggered: return $"{NameOf(e.Source)} 被動「{e.Text}」發動";
                case EventType.Move: return $"{NameOf(e.Source)} 移動 {e.Text}";
                case EventType.EnemyMove: return $"{NameOf(e.Source)} 移動 {e.Text}";
                case EventType.EnemyCharge: return $"{NameOf(e.Source)} 開始蓄力！";
                case EventType.EnemyChargeBreak: return $"{NameOf(e.Target)} 的蓄力被打斷了";
                case EventType.EnemyPhase: return $"{NameOf(e.Source)} 怒氣爆發，蓄力變快了！";
                case EventType.Death: return $"{NameOf(e.Target)} 倒下了";
                case EventType.BattleEnd: return $"戰鬥結束：{e.Text}";
                default: return null;
            }
        }

        private static string StatusFromText(string text)
        {
            return Enum.TryParse<StatusType>(text, out var type) ? CardText.StatusName(type) : text;
        }

        public void DebugSelectFirstPlayable()
        {
            var card = _battle.Hand.FirstOrDefault(c => _battle.CanPlay(c) == PlayResult.Ok);
            if (card != null) OnCardClicked(card);
        }

        public void DebugPlayFirstPlayable()
        {
            if (_recorder == null) return;
            var (card, target, mover) = AutoPlayer.Pick(_battle);
            if (card == null) return;
            _recorder.Play(card, target, mover);
            _pendingCard = null;
            _pendingMover = null;
            ClearPreview();
            PumpEvents();
            Refresh();
        }

        public void DebugAutoFinish()
        {
            for (int i = 0; i < 100 && _recorder != null && _battle.Result == BattleResult.Ongoing; i++) _recorder.PlayAuto();
            PumpEvents();
            Refresh();
        }

        public void DebugPreviewFirstCard()
        {
            var card = _battle.Hand.FirstOrDefault();
            if (card != null)
            {
                Preview(card);
                if (_handCards.Count > 0)
                {
                    _hoverCard = _handCards[0];
                    _hoverCard.AddToClassList("sts-card-hover");
                    _hoverCard.BringToFront();
                    LayoutHand();
                }
            }
        }

        public void DebugReviewZoom(bool enlarged)
        {
            _pendingCard = null; ClearPreview(); RefreshDetail();
            _stage.ResetView();
            if (enlarged) _stage.ZoomBy(10f);
        }

        public void DebugReviewUnitDetails(bool enemy)
        {
            var unit=_battle.Units.FirstOrDefault(u=>u.Alive && (enemy ? u.Side==Side.Enemy : u.DefId=="r_healer"))
                ?? _battle.Units.First(u=>u.Alive && u.Side==Side.Player);
            var foot=_stage.UnitFootPanel(unit);var head=_stage.UnitHeadPanel(unit);
            if(foot==null || head==null)throw new InvalidOperationException("Review unit is not rendered.");
            ShowUnitInfoAt(new Vector2(foot.Value.x,(foot.Value.y+head.Value.y)*.5f));
            if(_hoverUnit!=unit)throw new InvalidOperationException("Unit body hover picked the wrong character.");
            Debug.Log("[shot] Unit body hover and details verified: "+unit.Name);
        }

        public void DebugReviewScenario(int level)
        {
            _chapter = 0; _level = level; _seed = 12345; StartBattle(false);
        }

        public void DebugReviewActions()
        {
            var motions = new HashSet<string>();
            foreach (var unit in _battle.Units.Where(u => u.Side == Side.Player))
            {
                var view = _stage.ViewOf(unit.Id);
                if (view == null) throw new InvalidOperationException("Q-style model missing: " + unit.DefId);
                motions.Add(view.MotionProfile);
                if (view.MotionProfile == "caster") view.Cast(); else view.Attack();
            }
            if (motions.Count < 3) throw new InvalidOperationException("Combat motion profiles are not distinct.");
            Debug.Log("[shot] Distinct combat motion profiles verified: " + string.Join(", ", motions));
        }

        public void DebugReviewLongHand()
        {
            _recorder = null;
            var pool = _battle.Hand.Concat(_battle.DrawPile).Concat(_battle.DiscardPile).Distinct().ToList();
            _battle.Hand.Clear();
            _battle.Hand.AddRange(pool.OrderByDescending(c => c.Def.Effects.Count).Take(10));
            _pendingCard = null; ClearPreview(); Refresh();
        }

        public void DebugCheckLayout()
        {
            _stage.DebugValidateGridPicking();
            _stage.DebugValidateIdleFacing();
            var visible = new List<Rect>();
            foreach (var tag in _tags.Values)
            {
                if (tag.Root.resolvedStyle.visibility != Visibility.Visible) continue;
                var bounds = tag.Root.worldBound;
                var field = _field.worldBound;
                if (bounds.xMin < field.xMin - 1 || bounds.xMax > field.xMax + 1 || bounds.yMin < field.yMin - 1 || bounds.yMax > field.yMax + 1)
                    throw new InvalidOperationException("Battle HUD escaped the field.");
                if (visible.Any(r => r.Overlaps(bounds))) throw new InvalidOperationException("Battle HUD labels overlap.");
                foreach(var panel in new[]{_logBox,_detail,_unitInfo})
                    if(panel.resolvedStyle.display!=DisplayStyle.None && panel.worldBound.Overlaps(bounds))
                        throw new InvalidOperationException("Battle HUD overlaps a visible information panel.");
                visible.Add(bounds);
            }
            foreach (var tile in _handCards)
            {
                var label = tile.Q<Label>(className: "sts-card-description");
                var textSize = label.MeasureTextSize(label.text, label.contentRect.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                if (textSize.y > label.contentRect.height + 2)
                    throw new InvalidOperationException("Card description does not fit: " + label.text);
            }
            if (_handCards.Count > 0)
            {
                var label = _handCards[0].Q<Label>(className: "sts-card-description");
                foreach (var def in GameSession.Roster.SelectMany(h => h.Deck))
                {
                    string text = CardText.Summary(def);
                    var size = label.MeasureTextSize(text, label.contentRect.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                    if (size.y > label.contentRect.height + 2)
                        throw new InvalidOperationException("Roster card summary does not fit: " + def.Id + " " + text);
                }
            }
            Debug.Log("[shot] Battle HUD containment, separation and card text fit verified.");
        }
    }
}
