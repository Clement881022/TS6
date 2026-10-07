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
    /// <summary>
    /// 戰鬥畫面的 UI 層（UI Toolkit，全程式碼建立，樣式在 Resources/UI/Battle.uss）。
    /// 戰場本身由 BattleStage 用 3D 畫（45 度俯視）；這裡負責手牌、費用、角色頭上的資訊、操作與提示。
    /// 規則全部在 SanGuo.Core。
    /// </summary>
    public sealed class BattleScreen
    {
        private sealed class UnitTag
        {
            public VisualElement Root = null!;
            public Label Name = null!;
            public VisualElement Role = null!;
            public VisualElement Face = null!;
            public VisualElement HpFill = null!;
            public Label HpText = null!;
            public VisualElement Extra = null!;
            public VisualElement Intent = null!;
        }

        private const float TagWidth = 158f;
        private const float TagHeight = 76f;

        private readonly VisualElement _root;
        private readonly BattleStage _stage;
        private readonly Dictionary<int, UnitTag> _tags = new Dictionary<int, UnitTag>();
        private readonly List<string> _log = new List<string>();

        private Battle _battle = null!;
        /// <summary>null = 沒有向後端開始關卡的預覽戰場（直接開戰鬥場景時的暫時狀態），不能操作。</summary>
        private ReplayRecorder? _recorder;
        private bool _busy;
        private ulong _seed;
        private int _level = 1;
        private string _stageId = "1-1";
        /// <summary>目前進行的是資源副本時不為 null（主線關卡為 null）。</summary>
        private ResourceDungeonDef? _dungeon;
        /// <summary>開放編隊的關卡 / 副本：向後端開始時送出的編隊；教學關為 null。</summary>
        private List<FormationEntry>? _formation;
        private int _eventCursor;
        private bool _auto;
        private HashSet<(Side, int, int)> _previewTargets = new HashSet<(Side, int, int)>();
        /// <summary>已點下、正在等玩家點選敵人的指定目標牌（<see cref="TargetRule.EnemyAny"/>，例如破甲箭）。</summary>
        private CardInstance? _pendingCard;

        private VisualElement _content = null!;
        private VisualElement _field = null!;
        private VisualElement _tagLayer = null!;
        private BattleFx _fx = null!;
        private VisualElement _hand = null!;
        private Label _title = null!;
        private VisualElement _cost = null!;
        private VisualElement _piles = null!;
        private Label _logLabel = null!;
        private VisualElement _logBox = null!;
        private Button _autoButton = null!;
        private VisualElement? _overlay;

        public BattleScreen(VisualElement root, BattleStage stage)
        {
            _root = root;
            _stage = stage;
            _seed = 1;
            BuildStatic();
            // 由地圖 / 編隊 / 副本交棒過來的戰鬥（已向後端開始、體力已扣）；
            // 直接開戰鬥場景（開發用）就自己向後端開始目前選的關卡。
            var ticket = GameSession.Ticket;
            if (ticket != null)
            {
                ApplyTicket(ticket);
                StartBattle(recording: true);
            }
            else
            {
                _level = GameSession.SelectedLevel;
                StartBattle(recording: false);
                _ = BeginStageId(GameSession.StageIdOf(_level));
            }
            _root.schedule.Execute(AutoStep).Every(650);
            _root.schedule.Execute(UpdateTagPositions).Every(16);
        }

        // ------------------------------------------------------------ 建立畫面

        private void BuildStatic()
        {
            _root.AddToClassList("root");
            _content = new VisualElement();
            _content.AddToClassList("content");
            _root.Add(_content);
            _fx = new BattleFx(_content, (side, pos) => _stage.TileHeadPanel(side, pos), id => _stage.ViewOf(id));

            var header = new VisualElement();
            header.AddToClassList("header");
            _title = new Label("三國將星傳");
            _title.AddToClassList("header-title");
            header.Add(_title);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _autoButton = MakeButton("自動", ToggleAuto);
            buttons.Add(_autoButton);
            buttons.Add(MakeButton("撤退", Leave));
            buttons.Add(MakeButton("重來", () => { _ = BeginStageId(_stageId); }));
            header.Add(buttons);
            _content.Add(header);

            // 戰場區：3D 畫在這塊後面（透明）。
            _field = new VisualElement();
            _field.AddToClassList("field");
            _field.RegisterCallback<ClickEvent>(OnFieldClicked);
            _content.Add(_field);

            // 底部：左 = 費用與牌堆，中 = 手牌，右 = 結束回合。
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("bottom");

            var left = new VisualElement { pickingMode = PickingMode.Ignore };
            left.AddToClassList("bottom-left");
            _cost = new VisualElement { pickingMode = PickingMode.Ignore };
            _cost.AddToClassList("cost-label");
            _piles = new VisualElement { pickingMode = PickingMode.Ignore };
            _piles.AddToClassList("pile-label");
            left.Add(_cost);
            left.Add(_piles);
            bottom.Add(left);

            _hand = new VisualElement();
            _hand.AddToClassList("hand");
            bottom.Add(_hand);

            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.AddToClassList("bottom-right");
            var endTurn = new Button(EndTurn) { text = "結束\n回合" };
            endTurn.AddToClassList("end-turn");
            right.Add(endTurn);
            bottom.Add(right);
            _content.Add(bottom);

            var log = new VisualElement { pickingMode = PickingMode.Ignore };
            log.AddToClassList("log");
            _logLabel = new Label { pickingMode = PickingMode.Ignore };
            _logLabel.AddToClassList("log-line");
            log.Add(_logLabel);
            _logBox = log;
            _content.Add(log);

            // 角色頭上的資訊層：蓋在最上面但不擋點擊。
            _tagLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _tagLayer.AddToClassList("tag-layer");
            _content.Add(_tagLayer);
        }

        private static Button MakeButton(string text, Action onClick, bool primary = false)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            if (primary) b.AddToClassList("btn-primary");
            return b;
        }

        private void BuildTags()
        {
            _tagLayer.Clear();
            _tags.Clear();
            foreach (var unit in _battle.Units) AddTag(unit);
        }

        private void AddTag(Unit unit)
        {
            {
                var tag = new UnitTag { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
                tag.Root.AddToClassList("tag");
                tag.Root.AddToClassList(unit.Side == Side.Enemy ? "tag-enemy" : "tag-hero");
                tag.Root.style.width = TagWidth;
                tag.Name = new Label(unit.Name); tag.Name.AddToClassList("tag-name");
                var hpBg = new VisualElement(); hpBg.AddToClassList("tag-hp-bg");
                tag.HpFill = new VisualElement(); tag.HpFill.AddToClassList("tag-hp-fill");
                if (unit.Side == Side.Enemy) tag.HpFill.AddToClassList("tag-hp-fill-enemy");
                hpBg.Add(tag.HpFill);
                // 血量數字直接放在血條裡，省一行高度。
                tag.HpText = new Label { pickingMode = PickingMode.Ignore }; tag.HpText.AddToClassList("tag-hp-text");
                hpBg.Add(tag.HpText);
                tag.Extra = new VisualElement { pickingMode = PickingMode.Ignore }; tag.Extra.AddToClassList("tag-extra");
                tag.Intent = new VisualElement { pickingMode = PickingMode.Ignore }; tag.Intent.AddToClassList("tag-intent");
                tag.Role = new VisualElement { pickingMode = PickingMode.Ignore }; tag.Role.AddToClassList("tag-role");
                tag.Face = new VisualElement { pickingMode = PickingMode.Ignore }; tag.Face.AddToClassList("tag-face");
                var faceTex = HeroArt.Face(unit.DefId);
                if (faceTex != null) tag.Face.style.backgroundImage = new StyleBackground(faceTex);
                else tag.Face.style.display = DisplayStyle.None;
                var nameRow = new VisualElement { pickingMode = PickingMode.Ignore }; nameRow.AddToClassList("tag-name-row");
                nameRow.Add(tag.Role); nameRow.Add(tag.Name);
                var column = new VisualElement { pickingMode = PickingMode.Ignore };
                column.AddToClassList("tag-column");
                column.Add(nameRow);
                column.Add(hpBg);
                var mainRow = new VisualElement { pickingMode = PickingMode.Ignore };
                mainRow.AddToClassList("tag-main");
                mainRow.Add(tag.Face);
                mainRow.Add(column);
                tag.Root.Add(mainRow);
                tag.Root.Add(tag.Extra);
                tag.Root.Add(tag.Intent);
                _tagLayer.Add(tag.Root);
                _tags[unit.Id] = tag;
            }
        }

        // ------------------------------------------------------------ 流程

        private void ApplyTicket(BattleTicket ticket)
        {
            _stageId = ticket.StageId;
            _dungeon = ticket.Dungeon;
            _formation = ticket.Formation;
            _level = ticket.Level;
            _seed = ticket.Seed;
        }

        /// <summary>離開戰鬥：回到進來的頁面（資源副本回副本頁，主線回地圖）。</summary>
        private void Leave() => Nav.Go(_dungeon != null ? Page.Dungeons : Page.Map);

        /// <summary>進入主線關卡：可編隊的關卡先到編隊頁（開戰才扣體力）；鎖定編隊的直接開戰。</summary>
        private void EnterLevel(int level)
        {
            if (_busy || level < 1 || level > DemoContent.ChapterLevelCount) return;
            GameSession.SelectedLevel = level;
            if (GameSession.FormationLocked(level)) _ = BeginStageId(GameSession.StageIdOf(level));
            else { GameSession.FormationStageId = GameSession.StageIdOf(level); Nav.Go(Page.Formation); }
        }

        /// <summary>向後端開始關卡 / 副本（檢查條件、扣體力、取得種子），成功才開打並開始錄操作。</summary>
        private async Task BeginStageId(string stageId)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                string? error = await GameSession.BeginStage(stageId);
                if (error != null) { Toast(error); return; }
                ApplyTicket(GameSession.Ticket!);
                StartBattle(recording: true);
            }
            finally
            {
                _busy = false;
            }
        }

        private void StartBattle(bool recording)
        {
            // 已向後端開始的戰鬥用與伺服器相同的規則重建（含玩家編隊與養成）；開發用預覽走教學版關卡。
            var setup = (recording ? DemoMeta.BuildSetup(_stageId, _seed, GameSession.View.Raw, _formation) : null)
                ?? DemoContent.Level(_level, _seed);
            _battle = new Battle(setup);
            _recorder = recording ? new ReplayRecorder(_battle) : null;
            _finishing = false;
            // 教學關：隊伍固定、不開放自動戰鬥（之後再開放）。
            _autoButton.style.display = setup.AutoAllowed ? DisplayStyle.Flex : DisplayStyle.None;
            if (!setup.AutoAllowed) { _auto = false; _autoButton.EnableInClassList("btn-on", false); }
            _eventCursor = 0;
            _fx.Reset();
            _log.Clear();
            _previewTargets.Clear();
            if (_overlay != null) { _overlay.RemoveFromHierarchy(); _overlay = null; }
            _stage.Bind(_battle, _root, _field);
            BuildTags();
            PumpEvents();
            Refresh();
            if (_level == 1 && _dungeon == null)
                Tutorial.Show(_root, "battle1", "戰鬥教學", new[]
                {
                    "戰鬥是回合制出牌。點下方的手牌打出，每張牌要消耗費用，剩餘費用顯示在左下角。",
                    "費用用完（或不想出牌）就按「結束回合」，敵人才會行動；敵人頭上的圖示是牠下一步的行動預告。",
                    "攻擊只打得到同一路的敵人。打倒全部敵人就獲勝，快試試看吧！",
                }, speaker: "巴豆妖", model: "badou");
        }

        private bool _finishing;

        private bool Blocked => _recorder == null || _busy;

        private static void AddInfo(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("formation-hint");
            parent.Add(l);
        }

        private void EndTurn()
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
            CancelTargeting();
            _recorder!.EndTurn();
            PumpEvents();
            Refresh();
        }

        private void ToggleAuto()
        {
            _auto = !_auto;
            _autoButton.EnableInClassList("btn-on", _auto);
        }

        private void AutoStep()
        {
            if (!_auto || _battle.Result != BattleResult.Ongoing || Blocked) return;
            if (_fx.PendingSeconds > 0.05f) return; // 等上一段演出播完
            var card = _battle.Hand.FirstOrDefault(c => _battle.CanPlay(c) == PlayResult.Ok);
            if (card != null) _recorder!.Play(card);
            else _recorder!.EndTurn();
            PumpEvents();
            Refresh();
        }

        // ------------------------------------------------------------ 操作

        private void OnCardClicked(CardInstance card)
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
            if (_pendingCard == card) { CancelTargeting(); return; } // 再點一次同一張 = 取消
            CancelTargeting();

            // 指定目標的牌：先高亮所有敵人，等玩家點選；場上只剩一個敵人就不用選了。
            if (card.Def.Target == TargetRule.EnemyAny && _battle.CanPlay(card) == PlayResult.Ok
                && _battle.AliveUnits(Side.Enemy).Count > 1)
            {
                _pendingCard = card;
                _previewTargets.Clear();
                foreach (var u in _battle.AliveUnits(Side.Enemy)) _previewTargets.Add((u.Side, u.Pos.Lane, u.Pos.Row));
                RefreshTiles();
                Toast("點選要攻擊的敵人（再點一次卡牌取消）");
                return;
            }
            PlayCardAt(card, null);
        }

        private void PlayCardAt(CardInstance card, Unit? target)
        {
            var result = _recorder!.Play(card, target);
            if (result != PlayResult.Ok) { Toast(Explain(result)); Refresh(); return; }
            _previewTargets.Clear();
            PumpEvents();
            Refresh();
        }

        private void CancelTargeting()
        {
            if (_pendingCard == null) return;
            _pendingCard = null;
            _previewTargets.Clear();
            RefreshTiles();
        }

        /// <summary>等待指定目標時，點戰場上的敵方格就出牌；點別處取消。</summary>
        private void OnFieldClicked(ClickEvent evt)
        {
            var card = _pendingCard;
            if (card == null || _battle.Result != BattleResult.Ongoing || Blocked) return;
            if (!_stage.TryPick(evt.position, out var side, out var pos) || side != Side.Enemy)
            {
                CancelTargeting();
                return;
            }
            var target = _battle.UnitAt(Side.Enemy, pos);
            if (target == null || !target.Alive) { Toast("請點選敵人"); return; }
            _pendingCard = null;
            PlayCardAt(card, target);
        }


        private static string Explain(PlayResult result)
        {
            switch (result)
            {
                case PlayResult.NotEnoughCost: return "費用不足";
                case PlayResult.NoTarget: return "同路沒有目標，無法打出";
                case PlayResult.OwnerDead: return "該武將已陣亡";
                case PlayResult.Stunned: return "該武將昏亂，無法行動";
                case PlayResult.InvalidMove: return "無法移動到那裡";
                case PlayResult.MoveUsed: return "本回合已經移動過了";
                case PlayResult.BattleOver: return "戰鬥已結束";
                default: return result.ToString();
            }
        }

        private void Preview(CardInstance? card)
        {
            if (_pendingCard != null) return; // 等待選目標時保持敵人高亮
            _previewTargets.Clear();
            if (card != null && _battle.CanPlay(card) != PlayResult.NotInHand)
            {
                var targets = _battle.ResolveTargets(card.Owner, card.Def);
                if (targets != null)
                    foreach (var u in targets) _previewTargets.Add((u.Side, u.Pos.Lane, u.Pos.Row));
            }
            RefreshTiles();
        }

        private void Toast(string message)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            toast.pickingMode = PickingMode.Ignore;
            _root.Add(toast);
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(1300);
        }

        // ------------------------------------------------------------ 顯示

        private void Refresh()
        {
            RefreshTags();
            RefreshTiles();
            RefreshHand();
            RefreshHud();
            RefreshOverlay();
        }

        /// <summary>地磚顏色：技能目標預覽（黃）、移動可選武將 / 可到達格（綠）、已選武將（白）。</summary>
        private void RefreshTiles()
        {
            var states = new List<(Side, Position, TileState)>();
            foreach (var key in _previewTargets)
                states.Add((key.Item1, new Position(key.Item2, key.Item3), TileState.Target));

            _stage.SetTileStates(states);
        }

        private void RefreshTags()
        {
            // 戰鬥中新出現的單位（召喚）補上標籤。
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
                if (unit.Armor > 0) tag.Extra.Add(UiIcons.Chip("armor", unit.Armor.ToString()));
                foreach (var st in unit.Statuses) tag.Extra.Add(UiIcons.Chip(UiIcons.Status(st.Key), st.Value.Turns.ToString()));
                foreach (var br in unit.DefBreaks) tag.Extra.Add(UiIcons.Chip("status_armorbreak", $"{br.Percent * 100:0}%·{br.Turns}"));
                if (unit.Side == Side.Enemy && unit.Ability.HasFlag(EnemyAbility.Charger))
                    tag.Extra.Add(UiIcons.Chip("status_stun", $"{unit.StunGauge}/{unit.StunGaugeMax}"));
                tag.Extra.style.display = tag.Extra.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

                tag.Intent.Clear();
                if (unit.Side == Side.Enemy) FillIntent(tag.Intent, unit);
                tag.Intent.style.display = unit.Side == Side.Enemy ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>每幀把資訊貼到角色頭上（3D 位置 → 面板座標）。</summary>
        private void UpdateTagPositions()
        {
            foreach (var unit in _battle.Units)
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                var p = unit.Alive ? _stage.UnitHeadPanel(unit) : null;
                if (p == null) { tag.Root.style.visibility = Visibility.Hidden; continue; }
                var local = _tagLayer.WorldToLocal(p.Value);
                tag.Root.style.visibility = Visibility.Visible;
                tag.Root.style.left = local.x - TagWidth * 0.5f;
                tag.Root.style.top = local.y - TagHeight;
            }
        }

        private void FillIntent(VisualElement host, Unit enemy)
        {
            var intent = _battle.GetIntent(enemy);
            switch (intent.Type)
            {
                case Intent.Kind.Attack: host.Add(UiIcons.Chip(intent.Big ? "charge" : "damage", intent.Target!.Name)); break;
                case Intent.Kind.Charge: host.Add(UiIcons.Chip("charge")); break;
                case Intent.Kind.Heal: host.Add(UiIcons.Chip("heal", intent.Target!.Name)); break;
                case Intent.Kind.Move: host.Add(UiIcons.Chip("draw", (intent.MoveTo!.Value.Lane + 1).ToString())); break;
                case Intent.Kind.Stunned: host.Add(UiIcons.Chip("status_stun")); break;
            }
        }

        private static void FillCardEffects(VisualElement host, CardDef def)
        {
            foreach (var e in def.Effects)
            {
                string pct = $"{e.Multiplier * 100:0}%";
                switch (e.Type)
                {
                    case EffectType.Damage: host.Add(UiIcons.Chip("damage", pct)); break;
                    case EffectType.Heal: host.Add(UiIcons.Chip("heal", pct)); break;
                    case EffectType.Armor: host.Add(UiIcons.Chip("armor", pct)); break;
                    case EffectType.ApplyStatus:
                        host.Add(UiIcons.Chip(UiIcons.Status(e.Status), e.Multiplier > 0 ? $"{pct}·{e.Amount}" : e.Amount.ToString()));
                        break;
                    case EffectType.Draw: host.Add(UiIcons.Chip("draw", e.Amount.ToString())); break;
                    case EffectType.GainCost: host.Add(UiIcons.Chip("cost", "+" + e.Amount)); break;
                }
            }
        }

        private void RefreshHand()
        {
            _hand.Clear();
            foreach (var card in _battle.Hand)
            {
                var ok = _battle.CanPlay(card);
                var el = new VisualElement();
                el.AddToClassList("card");
                bool attack = card.Def.Effects.Any(e => e.Type == EffectType.Damage);
                bool heal = card.Def.Effects.Any(e => e.Type == EffectType.Heal);
                el.AddToClassList(attack ? "card-attack" : heal ? "card-heal" : "card-support");
                if (!card.Def.Basic) el.AddToClassList("card-skill");
                if (ok != PlayResult.Ok) el.AddToClassList("card-disabled");

                // 卡面插圖：出牌武將的頭像（TS6Client 美術），沒有圖就維持純色。
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("card-art");
                var portrait = HeroArt.Face(card.Owner.DefId);
                if (portrait != null) art.style.backgroundImage = new StyleBackground(portrait);

                var cost = new Label(card.Def.Cost.ToString()) { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("card-cost");
                var costIcon = UiIcons.Get("cost");
                if (costIcon != null) cost.style.backgroundImage = new StyleBackground(costIcon);
                art.Add(cost);
                art.Add(UiIcons.Icon(attack ? "damage" : heal ? "heal" : "armor", "card-tag"));
                var owner = new Label(card.Owner.Name + (card.Owner.Alive ? "" : "（陣亡）")) { pickingMode = PickingMode.Ignore };
                owner.AddToClassList("card-owner");
                art.Add(owner);

                var name = new Label(card.Def.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("card-name");
                var target = new Label(CardText.Target(card.Def)) { pickingMode = PickingMode.Ignore };
                target.AddToClassList("card-target");
                var desc = new VisualElement { pickingMode = PickingMode.Ignore };
                desc.AddToClassList("card-desc");
                FillCardEffects(desc, card.Def);
                var kw = new VisualElement { pickingMode = PickingMode.Ignore };
                kw.AddToClassList("card-kw");
                if (card.Def.Keywords.HasFlag(CardKeywords.Exhaust)) kw.Add(UiIcons.Chip("kw_exhaust"));
                if (card.Def.Keywords.HasFlag(CardKeywords.Retain)) kw.Add(UiIcons.Chip("kw_retain"));
                if (card.Def.Keywords.HasFlag(CardKeywords.Innate)) kw.Add(UiIcons.Chip("kw_innate"));

                el.Add(art);
                el.Add(name);
                el.Add(target);
                el.Add(desc);
                el.Add(kw);

                var captured = card;
                el.RegisterCallback<ClickEvent>(_ => OnCardClicked(captured));
                el.RegisterCallback<PointerEnterEvent>(_ => Preview(captured));
                el.RegisterCallback<PointerLeaveEvent>(_ => Preview(null));
                _hand.Add(el);
            }
        }

        private void RefreshHud()
        {
            string where = _dungeon != null && _recorder != null ? _dungeon.Name : $"第 {_level} 關";
            _title.text = _battle.Setup.TurnLimit > 0
                ? $"{where}　第 {_battle.Turn} / {_battle.Setup.TurnLimit} 回合"
                : $"{where}　第 {_battle.Turn} 回合";
            _cost.Clear();
            var orb = new VisualElement { pickingMode = PickingMode.Ignore };
            orb.AddToClassList("cost-orb");
            var orbIcon = UiIcons.Get("cost");
            if (orbIcon != null) orb.style.backgroundImage = new StyleBackground(orbIcon);
            orb.Add(new Label(_battle.Cost.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("cost-num"));
            _cost.Add(orb);
            _cost.Add(new Label($"費用上限 {_battle.Setup.CostCap}") { pickingMode = PickingMode.Ignore }.WithClass("cost-caption"));
            _piles.Clear();
            _piles.Add(PileRow("draw", "抽牌堆", _battle.DrawPile.Count));
            _piles.Add(PileRow("kw_retain", "棄牌堆", _battle.DiscardPile.Count));
            _piles.Add(PileRow("kw_exhaust", "消耗", _battle.ExhaustPile.Count));
            _logLabel.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 4)));
            _logBox.style.display = _log.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement PileRow(string icon, string label, int count)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("pile-row");
            row.Add(UiIcons.Icon(icon, "icon-sm"));
            row.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("pile-name"));
            row.Add(new Label(count.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("pile-count"));
            return row;
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
            _ = FinishStage(_overlay, _stageId, _recorder.Actions.ToList());
        }

        /// <summary>把操作紀錄交給後端結算（勝負與星數由後端自己重播算出），再顯示結果。</summary>
        private async Task FinishStage(VisualElement overlay, string stageId, List<ReplayAction> actions)
        {
            FinishStageResult result;
            try
            {
                result = await GameSession.Backend.FinishStage(stageId, actions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = new FinishStageResult { Code = "network" };
            }
            if (_overlay != overlay) return; // 期間已離開這一局
            await GameSession.Refresh();
            if (_overlay != overlay) return;

            overlay.Clear();
            if (!result.Ok)
            {
                var err = new Label(UiText.ExplainBackend(result.Code));
                err.AddToClassList("overlay-text");
                overlay.Add(err);
                overlay.Add(MakeButton("回地圖", () => Nav.Go(Page.Map), primary: true));
                return;
            }

            var card = new VisualElement();
            card.AddToClassList("result-card");
            overlay.Add(card);
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
                AddInfo(card, $"經驗 +{result.Exp}　金幣 +{result.Gold}" + (result.Yuanbao > 0 ? $"　元寶 +{result.Yuanbao}" : ""));
                if (result.LevelsGained > 0) AddInfo(card, $"帳號升級！Lv.{GameSession.View.Level}（體力已回滿）");
            }

            if (dungeon != null)
            {
                btnRow.Add(MakeButton("再打一次", () => _ = BeginStageId(stageId), primary: true));
                btnRow.Add(MakeButton("回副本", () => Nav.Go(Page.Dungeons)));
                card.Add(btnRow);
                return;
            }
            int level = _level;
            bool hasNext = result.Won && level < DemoContent.ChapterLevelCount;
            if (hasNext) btnRow.Add(MakeButton("下一關", () => EnterLevel(level + 1), primary: true));
            btnRow.Add(MakeButton("再打一次", () => EnterLevel(level), primary: !hasNext));
            btnRow.Add(MakeButton("回地圖", () => Nav.Go(Page.Map)));
            card.Add(btnRow);
        }

        // ------------------------------------------------------------ 戰鬥紀錄

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
                case EventType.Armor: return $"{NameOf(e.Target)} 獲得護甲 {e.Value}";
                case EventType.StatusApplied: return $"{NameOf(e.Target)} 受到 {StatusFromText(e.Text)}";
                case EventType.Draw: return $"抽了 {e.Value} 張牌";
                case EventType.GainCost: return $"獲得 {e.Value} 費";
                case EventType.Move: return $"{NameOf(e.Source)} 移動 {e.Text}";
                case EventType.EnemyMove: return $"{NameOf(e.Source)} 移動 {e.Text}";
                case EventType.EnemySummon: return $"{NameOf(e.Source)} 召喚了 {NameOf(e.Target)}";
                case EventType.EnemyCharge: return $"{NameOf(e.Source)} 開始蓄力！";
                case EventType.StunGauge: return $"{NameOf(e.Target)} 昏亂條 {e.Value}/{e.Text}";
                case EventType.EnemySkip: return $"{NameOf(e.Source)} 昏亂，無法行動";
                case EventType.Death: return $"{NameOf(e.Target)} 倒下了";
                case EventType.BattleEnd: return $"戰鬥結束：{e.Text}";
                default: return null;
            }
        }

        private static string StatusFromText(string text)
        {
            return Enum.TryParse<StatusType>(text, out var type) ? CardText.StatusName(type) : text;
        }

        // ------------------------------------------------------------ 截圖 / 除錯用

                public void DebugPlayFirstPlayable()
        {
            var card = _battle.Hand.FirstOrDefault(c => _battle.CanPlay(c) == PlayResult.Ok);
            if (card != null) OnCardClicked(card);
        }

        /// <summary>截圖用：自動打完這一局（會錄下操作並交給後端結算）。</summary>
        public void DebugAutoFinish()
        {
            for (int i = 0; i < 100 && _recorder != null && _battle.Result == BattleResult.Ongoing; i++) _recorder.PlayAuto();
            PumpEvents();
            Refresh();
        }

        public void DebugPreviewFirstCard()
        {
            var card = _battle.Hand.FirstOrDefault();
            if (card != null) Preview(card);
        }
    }
}
