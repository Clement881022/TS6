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

        private const float TagWidth = 150f;
        private const float HeroTagWidth = 124f;
        private const float TagHeight = 74f;

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
        /// <summary>地磚高亮：會被打到 / 治療的目標（黃）、卡牌射程（淺藍）、移動可到達格（綠）。</summary>
        private readonly HashSet<Position> _previewTargets = new HashSet<Position>();
        private readonly HashSet<Position> _previewRange = new HashSet<Position>();
        private readonly HashSet<Position> _previewReach = new HashSet<Position>();
        /// <summary>已點下、正在等玩家點選格子的牌：單體敵人牌的施放格（可空放），或移動卡（先選武將、再選目的地）。</summary>
        private CardInstance? _pendingCard;
        /// <summary>移動卡已選好的武將（null = 還在選武將）。</summary>
        private Unit? _pendingMover;
        /// <summary>滑鼠懸停的單位（顯示屬性與增減益面板）。</summary>
        private Unit? _hoverUnit;
        private VisualElement _unitInfo = null!;

        private VisualElement _content = null!;
        private VisualElement _field = null!;
        private VisualElement _tagLayer = null!;
        private BattleFx _fx = null!;
        private VisualElement _hand = null!;      // 左側手牌清單（每張牌一列）
        private VisualElement _detail = null!;    // 選定技能後在右側跳出的卡片詳情
        private VisualElement _heroBar = null!;   // 畫面底部的武將資訊列
        private Button _endButton = null!;
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
            buttons.Add(MakeButton("重置視角", () => _stage.ResetView()));
            buttons.Add(MakeButton("撤退", Leave));
            buttons.Add(MakeButton("重來", () => { _ = BeginStageId(_stageId); }));
            header.Add(buttons);
            _content.Add(header);

            // 戰場區：3D 畫在這塊後面（透明）。
            _field = new VisualElement();
            _field.AddToClassList("field");
            // 左側是手牌清單、底部是武將資訊列：戰場（鏡頭取景範圍）讓出這兩塊。
            _field.style.marginLeft = 370f;
            _field.style.marginBottom = 200f;
            _field.RegisterCallback<ClickEvent>(OnFieldClicked);
            _field.RegisterCallback<PointerDownEvent>(OnFieldDown);
            _field.RegisterCallback<PointerMoveEvent>(OnFieldMove);
            _field.RegisterCallback<PointerUpEvent>(OnFieldUp);
            _field.RegisterCallback<WheelEvent>(OnFieldWheel);
            _field.RegisterCallback<PointerLeaveEvent>(_ => HideUnitInfo());
            _content.Add(_field);

            // 左側：結束回合 / 費用 / 牌堆資訊，下面是手牌清單（一張牌一列，仿超時空方舟）。
            var left = new VisualElement { pickingMode = PickingMode.Ignore };
            left.AddToClassList("bl-left");
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("bl-top");
            _endButton = new Button(EndTurn) { text = "結束回合" };
            _endButton.AddToClassList("bl-end");
            top.Add(_endButton);
            _cost = new VisualElement { pickingMode = PickingMode.Ignore };
            _cost.AddToClassList("bl-cost");
            top.Add(_cost);
            left.Add(top);
            _piles = new VisualElement { pickingMode = PickingMode.Ignore };
            _piles.AddToClassList("bl-piles");
            left.Add(_piles);
            _hand = new VisualElement();
            _hand.AddToClassList("bl-list");
            left.Add(_hand);
            _content.Add(left);

            // 右側：選定技能後才出現的卡片詳情。
            _detail = new VisualElement();
            _detail.AddToClassList("bl-detail");
            _detail.style.display = DisplayStyle.None;
            _content.Add(_detail);

            // 底部：武將資訊列。
            _heroBar = new VisualElement();
            _heroBar.AddToClassList("bl-bar");
            _content.Add(_heroBar);

            var log = new VisualElement { pickingMode = PickingMode.Ignore };
            log.AddToClassList("bl-log");
            _logLabel = new Label { pickingMode = PickingMode.Ignore };
            _logLabel.AddToClassList("bl-log-line");
            log.Add(_logLabel);
            _logBox = log;
            _content.Add(log);

            // 角色頭上的資訊層：蓋在最上面但不擋點擊。
            _tagLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _tagLayer.AddToClassList("tag-layer");
            _content.Add(_tagLayer);

            // 單位懸停面板：跟著游標、不擋點擊。
            _unitInfo = new VisualElement { pickingMode = PickingMode.Ignore };
            _unitInfo.AddToClassList("unit-info");
            _unitInfo.style.display = DisplayStyle.None;
            _content.Add(_unitInfo);
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
                tag.Root.style.width = unit.Side == Side.Player ? HeroTagWidth : TagWidth;
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
            ClearPreview();
            _pendingCard = null;
            _pendingMover = null;
            HideUnitInfo();
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
                    "戰場是敵我共用的 5x5 棋盤，每張牌都有攻擊範圍（格數）：近戰只打得到相鄰的敵人，弓手與法師射程較遠。",
                    "牌堆裡有幾張 0 費的通用「移動」牌（隊伍每有一人就有一張）：點牌後先選要移動的武將，再點綠色的格子走位。攻擊牌要自己點選射程內的格子施放，空格也可以點（會打空）。牌抽完就沒有了，不會重洗。打倒全部敵人就獲勝！",
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
            var (card, target, mover) = AutoPlayer.Pick(_battle);
            if (card != null) _recorder!.Play(card, target, mover);
            else _recorder!.EndTurn();
            PumpEvents();
            Refresh();
        }

        // ------------------------------------------------------------ 操作

        /// <summary>點手牌清單的一列 = 選定該技能（右側跳出詳情、標出射程）；再點一次取消。</summary>
        private void OnCardClicked(CardInstance card)
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
            if (_pendingCard == card) { CancelTargeting(); return; }
            CancelTargeting();
            if (!_battle.Hand.Contains(card)) return;
            BeginPending(card);
        }

        /// <summary>詳情面板的「使用」：不需要點格的牌（自身 / 全體 / 自動選目標）直接打出。</summary>
        private void UseSelected()
        {
            var card = _pendingCard;
            if (card == null || card.Def.Target == TargetRule.MoveDest || card.Def.Target == TargetRule.Enemy) return;
            PlayCardAt(card, null, null);
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

        /// <summary>等待指定格子時：點格出牌；移動卡先點武將再點目的地；點場外取消。</summary>
        private void OnFieldClicked(ClickEvent evt)
        {
            if (_dragged) { _dragged = false; return; }   // 拖曳視角結束後的 click 不算點格
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
            if (card.Def.Target != TargetRule.Enemy) return;   // 其餘牌用右側詳情的「使用」按鈕
            var owner = card.Owner!;
            if (!_battle.InBounds(pos) || pos == owner.Pos || Position.Distance(owner.Pos, pos) > card.Def.Range)
            {
                Toast("請點選射程內的格子");
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
            RefreshHeroBar();
            Toast("再點選綠色格子移動");
        }

        // ------------------------------------------------------------ 單位懸停面板

        // ---- 視角：左鍵 / 中鍵拖曳平移、滾輪縮放 ----
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
                if (!_dragged && ((Vector2)evt.position - _dragStart).magnitude > 8f) { _dragged = true; HideUnitInfo(); }
                if (_dragged)
                {
                    _stage.PanBy((Vector2)evt.position - _dragLast);
                    _dragLast = evt.position;
                    return;
                }
            }
            OnFieldHover(evt);
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

        private void OnFieldHover(PointerMoveEvent evt)
        {
            var unit = PickUnit(evt.position);
            if (unit == null) { HideUnitInfo(); return; }
            if (_hoverUnit != unit)
            {
                _hoverUnit = unit;
                RenderUnitInfo(unit);
            }
            // 面板跟著游標，靠近右 / 下緣時翻到另一側，避免被切掉。
            var local = _content.WorldToLocal(evt.position);
            float w = _content.layout.width, h = _content.layout.height;
            const float panelW = 330f, panelH = 400f;
            float left = local.x + 28f;
            if (left + panelW > w - 8f) left = local.x - 28f - panelW;
            float top = Mathf.Clamp(local.y - 40f, 8f, Mathf.Max(8f, h - panelH - 8f));
            _unitInfo.style.left = Mathf.Max(8f, left);
            _unitInfo.style.top = top;
            _unitInfo.style.display = DisplayStyle.Flex;
        }

        private void HideUnitInfo()
        {
            _hoverUnit = null;
            _unitInfo.style.display = DisplayStyle.None;
        }

        /// <summary>游標下的單位：先看角色身體（頭頂到腳下的範圍），沒有再用射線打到的地磚找格上的單位。</summary>
        private Unit? PickUnit(Vector2 panelPoint)
        {
            Unit? best = null;
            float bestDx = float.MaxValue;
            foreach (var unit in _battle.Units)
            {
                if (!unit.Alive) continue;
                var foot = _stage.UnitFootPanel(unit);
                var head = _stage.UnitHeadPanel(unit);
                if (foot == null || head == null) continue;
                float dx = Mathf.Abs(panelPoint.x - foot.Value.x);
                if (dx > 46f || panelPoint.y < head.Value.y - 6f || panelPoint.y > foot.Value.y + 14f) continue;
                if (dx < bestDx) { bestDx = dx; best = unit; }
            }
            if (best != null) return best;
            return _stage.TryPick(panelPoint, out var pos) ? _battle.UnitAt(pos) : null;
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

            string hp = $"生命 {unit.Hp}/{unit.MaxHp}" + (unit.Armor > 0 ? $"　護甲 {unit.Armor}" : "");
            _unitInfo.Add(new Label(hp) { pickingMode = PickingMode.Ignore }.WithClass("ui-hp"));

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("ui-grid");
            var st = unit.Stats;
            // 法系（法師 / 醫療 / 軍師）的治療、法傷與增減益強度吃謀略，其餘吃攻擊：把主屬性排在前面。
            var atk = (CardText.AtkName, st.Atk, unit.EffectiveAtk);
            var intl = (CardText.IntName, st.Int, unit.EffectiveInt);
            var first = unit.IsCaster ? intl : atk;
            var second = unit.IsCaster ? atk : intl;
            AddStat(grid, "stat_" + (unit.IsCaster ? "int" : "atk"), first.Item2, first.Item3);
            AddStat(grid, "stat_" + (unit.IsCaster ? "atk" : "int"), second.Item2, second.Item3);
            AddStat(grid, "stat_def", st.Def, (int)Math.Round(unit.EffectiveDef));
            AddStat(grid, "stat_move", st.Move, st.Move);
            AddStat(grid, "射程", unit.AttackRange, unit.AttackRange);
            AddStat(grid, "閃避", st.Dodge, st.Dodge, "%");
            AddStat(grid, "暴擊", st.Crit, unit.EffectiveCrit, "%");
            AddStat(grid, "暴傷", st.CritDmg, st.CritDmg, "%");
            _unitInfo.Add(grid);

            _unitInfo.Add(new Label("增減益") { pickingMode = PickingMode.Ignore }.WithClass("ui-sec"));
            int before = _unitInfo.childCount;
            foreach (var kv in unit.Statuses) AddStatusRow(UiIcons.Status(kv.Key), StatusLine(kv.Key, kv.Value));
            foreach (var br in unit.DefBreaks) AddStatusRow("status_armorbreak", $"破甲　防禦 -{br.Percent * 100:0}%・剩 {br.Turns} 回合");
            if (unit.Side == Side.Enemy && unit.Ability.HasFlag(EnemyAbility.Charger))
            {
                AddStatusRow("status_stun", $"昏亂條 {unit.StunGauge}/{unit.StunGaugeMax}");
                if (unit.Charging) AddStatusRow("charge", "蓄力中：下回合放大招");
            }
            if (_unitInfo.childCount == before)
                _unitInfo.Add(new Label("目前沒有增減益") { pickingMode = PickingMode.Ignore }.WithClass("ui-none"));
        }

        /// <summary>懸停面板的一格屬性；name 是 stat_* 圖示名，或純文字（閃避 / 暴擊等沒有圖示的屬性）。</summary>
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
                case StatusType.Burn:
                case StatusType.Poison: return $"{name}　每回合 {state.Power} 傷害・剩 {state.Turns} 回合";
                case StatusType.AtkUp: return $"{name}　攻擊 / 謀略 +{state.Power}%・剩 {state.Turns} 回合";
                case StatusType.DefUp: return $"{name}　防禦 +{state.Power}%・剩 {state.Turns} 回合";
                case StatusType.CritUp: return $"{name}　暴擊 +{state.Power}%・剩 {state.Turns} 回合";
                case StatusType.Taunt: return $"{name}　吸引敵人攻擊、受傷 -{DamageCalc.TauntDamageReduction * 100:0}%・剩 {state.Turns} 回合";
                default: return $"{name}・剩 {state.Turns} 回合";
            }
        }

        private static string Explain(PlayResult result)
        {
            switch (result)
            {
                case PlayResult.NotEnoughCost: return "費用不足";
                case PlayResult.NoTarget: return "射程內沒有目標，無法打出（先用「移動」卡走位）";
                case PlayResult.OutOfRange: return "超出射程或無法到達那裡";
                case PlayResult.InvalidMover: return "這名武將現在不能移動";
                case PlayResult.OwnerDead: return "該武將已陣亡";
                case PlayResult.Stunned: return "該武將昏亂，無法行動";
                case PlayResult.BattleOver: return "戰鬥已結束";
                default: return result.ToString();
            }
        }

        private void Preview(CardInstance? card)
        {
            if (_pendingCard != null) return; // 等待選目標時保持高亮
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

        /// <summary>標出這張牌的射程（淺藍）、會被選中的目標（黃）或移動可到達的格子（綠）。</summary>
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
                    // 還沒選武將：標出所有能移動的武將。
                    foreach (var u in _battle.AliveUnits(Side.Player))
                        if (_battle.CanMoveUnit(u)) _previewTargets.Add(u.Pos);
                }
            }
            else if (card.Owner != null)
            {
                var owner = card.Owner;
                if (def.Target == TargetRule.Enemy || def.Target == TargetRule.EnemyLowestHp || def.Target == TargetRule.AllyLowestHp)
                {
                    for (int lane = 0; lane < _battle.Setup.Lanes; lane++)
                        for (int row = 0; row < _battle.Setup.Rows; row++)
                        {
                            var p = new Position(lane, row);
                            if (p != owner.Pos && Position.Distance(owner.Pos, p) <= def.Range) _previewRange.Add(p);
                        }
                }
                // 自動選目標的牌（最低血量、全體）直接標出會中招的單位；單體敵人牌由玩家點格，只標射程。
                if (def.Target != TargetRule.Enemy)
                {
                    var targets = _battle.ResolveTargets(owner, def);
                    if (targets != null)
                        foreach (var u in targets) _previewTargets.Add(u.Pos);
                }
                else
                {
                    foreach (var u in _battle.AliveUnits(Side.Enemy))
                        if (Position.Distance(owner.Pos, u.Pos) <= def.Range) _previewTargets.Add(u.Pos);
                }
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

        /// <summary>地磚顏色：射程（淺藍）、技能目標（黃）、移動可到達格（綠）、等待出牌的武將（白）。</summary>
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
                // 敵方標籤在頭頂；我方在近端，頭頂方向是敵方棋盤，所以標籤放腳下。
                bool below = unit.Side == Side.Player;
                var p = unit.Alive ? (below ? _stage.UnitFootPanel(unit) : _stage.UnitHeadPanel(unit)) : null;
                if (p == null) { tag.Root.style.visibility = Visibility.Hidden; continue; }
                var local = _tagLayer.WorldToLocal(p.Value);
                tag.Root.style.visibility = Visibility.Visible;
                tag.Root.style.left = local.x - (below ? HeroTagWidth : TagWidth) * 0.5f;
                tag.Root.style.top = below ? local.y + 2f : local.y - TagHeight;
            }
        }

        private void FillIntent(VisualElement host, Unit enemy)
        {
            var intent = _battle.GetIntent(enemy);
            switch (intent.Type)
            {
                case Intent.Kind.Attack:
                    if (intent.MoveTo != null) host.Add(UiIcons.Chip("draw", "→"));
                    host.Add(UiIcons.Chip(intent.Big ? "charge" : "damage", intent.Target!.Name));
                    break;
                case Intent.Kind.Charge: host.Add(UiIcons.Chip("charge")); break;
                case Intent.Kind.Heal: host.Add(UiIcons.Chip("heal", intent.Target!.Name)); break;
                case Intent.Kind.Move: host.Add(UiIcons.Chip("draw", "逼近")); break;
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
                    case EffectType.Move: host.Add(UiIcons.Chip("draw", "走位")); break;
                }
            }
        }

        // ------------------------------------------------------------ 手牌清單 / 卡片詳情 / 武將資訊列

        private static string KindIcon(CardDef def) =>
            def.Effects.Any(e => e.Type == EffectType.Damage) ? "damage"
            : def.Effects.Any(e => e.Type == EffectType.Heal) ? "heal"
            : def.Target == TargetRule.MoveDest ? "draw" : "armor";

        private static VisualElement Face(Unit? owner, string cls)
        {
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

        /// <summary>左側手牌清單：一張牌一列（編號、頭像、名稱、費用）；選定的那列會凸出來。</summary>
        private void RefreshCards()
        {
            _hand.Clear();
            int index = 1;
            foreach (var card in _battle.Hand)
            {
                var captured = card;
                var ok = _battle.CanPlay(card);
                var row = new VisualElement();
                row.AddToClassList("bl-row");
                if (card.Def.Target == TargetRule.MoveDest) row.AddToClassList("bl-row-move");
                else if (!card.Def.Basic) row.AddToClassList("bl-row-skill");
                if (ok != PlayResult.Ok) row.AddToClassList("bl-row-disabled");
                if (card == _pendingCard) row.AddToClassList("bl-row-selected");

                row.Add(new Label(index.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("bl-row-index"));
                row.Add(Face(card.Owner, "bl-row-face"));
                var name = new Label(card.Def.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("bl-row-name");
                row.Add(name);
                row.Add(UiIcons.Icon(KindIcon(card.Def), "bl-row-kind"));
                var cost = new Label(card.Def.Cost.ToString()) { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("bl-row-cost");
                var costIcon = UiIcons.Get("cost");
                if (costIcon != null) cost.style.backgroundImage = new StyleBackground(costIcon);
                row.Add(cost);

                row.RegisterCallback<ClickEvent>(_ => OnCardClicked(captured));
                row.RegisterCallback<PointerEnterEvent>(_ => Preview(captured));
                row.RegisterCallback<PointerLeaveEvent>(_ => Preview(null));
                _hand.Add(row);
                index++;
            }
        }

        /// <summary>右側卡片詳情：選定技能後才出現，顯示射程圖、效果、關鍵字與操作提示。</summary>
        private void RefreshDetail()
        {
            var card = _pendingCard;
            _detail.Clear();
            if (card == null || !_battle.Hand.Contains(card)) { _detail.style.display = DisplayStyle.None; return; }
            _detail.style.display = DisplayStyle.Flex;
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

            _detail.Add(RangeIcon.Build(def));
            _detail.Add(new Label(CardText.Target(def)) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-target"));
            var desc = CardText.Description(def);
            if (desc.Length > 0) _detail.Add(new Label(desc) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-desc"));
            string kw = CardText.Keywords(def.Keywords);
            if (kw.Length > 0) _detail.Add(new Label("關鍵字：" + kw) { pickingMode = PickingMode.Ignore }.WithClass("bl-d-kw"));

            var check = _battle.CanPlay(card);
            string hint;
            if (check != PlayResult.Ok) hint = Explain(check);
            else if (def.Target == TargetRule.MoveDest)
                hint = _pendingMover == null ? "先點選要移動的武將（棋盤或底部武將列），再點綠色格子" : $"移動 {_pendingMover.Name}：點選綠色格子";
            else if (def.Target == TargetRule.Enemy) hint = "點選棋盤上射程內的格子施放，空格也可以（會打空）";
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

        /// <summary>圖示 + 數值（有增減益時以綠 / 紅標示）：攻擊、謀略、防禦、移動力。主屬性（該職業實際吃的）放大。</summary>
        private static VisualElement StatChip(string icon, int effective, int baseValue, bool main = false)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("bl-stat-chip");
            chip.Add(UiIcons.Icon(icon, main ? "bl-stat-icon-main" : "bl-stat-icon"));
            var l = new Label(effective.ToString()) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(main ? "bl-stat-main" : "bl-stat");
            if (effective > baseValue) l.AddToClassList("ui-up");
            else if (effective < baseValue) l.AddToClassList("ui-down");
            chip.Add(l);
            return chip;
        }

        /// <summary>底部武將資訊列：頭像、生命、主要屬性（攻擊 / 謀略 / 防禦 / 移動力）與增減益；移動卡選武將時可以點這裡。</summary>
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
                hpBg.Add(new Label($"{unit.Hp}/{unit.MaxHp}" + (unit.Armor > 0 ? $"  盾 {unit.Armor}" : "")) { pickingMode = PickingMode.Ignore }.WithClass("bl-hp-text"));
                col.Add(hpBg);

                // 法系把「謀略」放在最前面，其餘把「攻擊」放最前面（與傷害 / 治療實際吃的屬性一致）。
                var stats = new VisualElement { pickingMode = PickingMode.Ignore };
                stats.AddToClassList("bl-stats");
                var atkStat = ("stat_atk", unit.EffectiveAtk, unit.Stats.Atk);
                var intStat = ("stat_int", unit.EffectiveInt, unit.Stats.Int);
                var first = unit.IsCaster ? intStat : atkStat;
                var second = unit.IsCaster ? atkStat : intStat;
                stats.Add(StatChip(first.Item1, first.Item2, first.Item3, main: true));
                stats.Add(StatChip(second.Item1, second.Item2, second.Item3));
                stats.Add(StatChip("stat_def", (int)Math.Round(unit.EffectiveDef), unit.Stats.Def));
                stats.Add(StatChip("stat_move", unit.Stats.Move, unit.Stats.Move));
                col.Add(stats);

                var chips = new VisualElement { pickingMode = PickingMode.Ignore };
                chips.AddToClassList("bl-chips");
                foreach (var st in unit.Statuses) chips.Add(UiIcons.Chip(UiIcons.Status(st.Key), st.Value.Turns.ToString()));
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
            string where = _dungeon != null && _recorder != null ? _dungeon.Name : $"第 {_level} 關";
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
            _piles.Clear();
            _piles.Add(new Label($"抽牌堆 {_battle.DrawPile.Count}（不重洗）　手牌 {_battle.Hand.Count}/{Battle.MaxHandSize}") { pickingMode = PickingMode.Ignore }.WithClass("bl-pile-text"));
            _logLabel.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 4)));
            _logBox.style.display = _log.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement PileRow(string icon, string label, string count)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("pile-row");
            row.Add(UiIcons.Icon(icon, "icon-sm"));
            row.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("pile-name"));
            row.Add(new Label(count) { pickingMode = PickingMode.Ignore }.WithClass("pile-count"));
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
