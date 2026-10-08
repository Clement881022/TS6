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
            public float LastLeft = float.NaN, LastTop = float.NaN;
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
        private int _chapter;
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
        /// <summary>已點下、正在等玩家點選格子的牌：單體敵人牌的施放格（範圍內必須有敵人），或移動卡（先選武將、再選目的地）。</summary>
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
        private VisualElement _hand = null!;
        private readonly List<VisualElement> _handCards = new List<VisualElement>();
        private VisualElement? _hoverCard;
        private VisualElement _detail = null!;    // 選定技能後在右側跳出的卡片詳情
        private VisualElement _heroBar = null!;   // 畫面底部的武將資訊列
        private Button _endButton = null!;
        private Label _title = null!;
        private VisualElement _cost = null!;
        private VisualElement _piles = null!;
        private Button _drawButton = null!, _discardButton = null!;
        private Label _drawCount = null!, _discardCount = null!;
        private VisualElement? _pileView;
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
                _chapter = GameSession.SelectedChapter;
                _level = GameSession.SelectedLevel;
                StartBattle(recording: false);
                _ = BeginStageId(GameSession.StageIdOf(_chapter, _level));
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
            UiKit.ApplyDisplayFont(_title);
            header.Add(_title);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _autoButton = MakeButton("自動", ToggleAuto);
            buttons.Add(_autoButton);
            buttons.Add(MakeButton("重置視角", () => _stage.ResetView()));
            if (Debug.isDebugBuild) buttons.Add(MakeButton("直接勝利", DebugWin));
            buttons.Add(MakeButton("撤退", Leave));
            buttons.Add(MakeButton("重來", () => { _ = BeginStageId(_stageId); }));
            header.Add(buttons);
            _content.Add(header);

            // 戰場區：3D 畫在這塊後面（透明）。
            _field = new VisualElement();
            _field.AddToClassList("field");
            // 左側是手牌清單、底部是武將資訊列：戰場（鏡頭取景範圍）讓出這兩塊。
            _field.style.marginLeft = 264f;
            _field.style.marginRight = 18f;
            _field.style.marginBottom = 310f;
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
            _piles = new VisualElement();
            _piles.AddToClassList("bl-piles");
            _drawButton = PileButton("pile_draw", PileKind.Draw, out _drawCount);
            _discardButton = PileButton("pile_discard", PileKind.Discard, out _discardCount);
            _piles.Add(_drawButton);
            _piles.Add(_discardButton);
            left.Add(_piles);
            _hand = new VisualElement();
            _hand.AddToClassList("sts-hand");
            _hand.RegisterCallback<GeometryChangedEvent>(_ => LayoutHand());
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
            _content.Add(_hand);

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
            _field.Add(_tagLayer);

            // 單位懸停面板：跟著游標、不擋點擊。
            _unitInfo = new VisualElement { pickingMode = PickingMode.Ignore };
            _unitInfo.AddToClassList("unit-info");
            _unitInfo.style.display = DisplayStyle.None;
            _field.Add(_unitInfo);
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
            _chapter = ticket.Chapter;
            _level = ticket.Level;
            _seed = ticket.Seed;
        }

        /// <summary>離開戰鬥：回到進來的頁面（資源副本回副本頁，主線回地圖）。</summary>
        private void Leave() => Nav.Go(IsWorldBoss ? Page.WorldBoss : _dungeon != null ? Page.Dungeons : Page.Map);

        private bool IsWorldBoss => _stageId == WorldBoss.StageId;

        /// <summary>進入主線關卡：可編隊的關卡先到編隊頁（開戰才扣體力）；鎖定編隊的直接開戰。</summary>
        private void EnterLevel(int chapter, int level)
        {
            if (_busy || !Campaign.IsValid(chapter, level)) return;
            GameSession.Select(chapter, level);
            StoryPlayer.ShowBefore(_root, chapter, level, () =>
            {
                if (GameSession.FormationLocked(chapter, level)) _ = BeginStageId(GameSession.StageIdOf(chapter, level));
                else { GameSession.FormationStageId = GameSession.StageIdOf(chapter, level); Nav.Go(Page.Formation); }
            });
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
                ?? Campaign.Setup(_chapter, _level, _seed);
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
            if (_dungeon == null && !IsWorldBoss && _chapter == 0) ShowLevelTutorial();
        }

        /// <summary>第零章各關的戰鬥教學（巴豆妖旁白，每關只講該關要教的機制）。</summary>
        private void ShowLevelTutorial()
        {
            string[]? pages;
            string key = _level == 1 ? "battle1" : "battle" + _level;
            switch (_level)
            {
                case 1:
                    pages = new[]
                    {
                        "戰鬥是回合制出牌。點下方的手牌打出，每張牌要消耗費用，剩餘費用顯示在左下角；沒用完的費用與手牌都會留到下回合（費用、手牌上限各 10）。",
                        "費用用完（或不想出牌）就按「結束回合」，換敵人行動；敵人頭上的圖示是牠下一步的行動預告。",
                        "戰場是敵我共用的 5x5 棋盤。每名武將有攻擊範圍（格數）：坦克與戰士只打得到相鄰的敵人，遊俠、術士、軍師與醫者射程較遠。",
                        "牌庫裡有幾張 0 費的通用「移動」牌（隊伍每有一人就有一張）：點牌後先選要移動的武將，再點綠色的格子走位。攻擊牌射程內要有敵人才能打出，點選敵人所在的格子施放，不能空揮。牌庫抽完會把棄牌洗回去。打倒全部敵人就獲勝！",
                    };
                    break;
                case 2:
                    pages = new[] { "遠程敵人專打最後排。坦克的「嘲諷」會讓全場敵人這回合都以他為目標，把火力從後排拉走。" };
                    break;
                case 3:
                    pages = new[] { "重甲敵人的防禦很高，物理攻擊幾乎打不動。遊俠的「破甲箭」會降低目標防禦，破甲後全隊的物理傷害都會變高。" };
                    break;
                case 4:
                    pages = new[] { "法術傷害不受防禦影響，後排的術士雖然脆弱，卻能一擊重創我方。優先擊殺牠，或用嘲諷把牠的攻擊拉到坦克身上。" };
                    break;
                case 5:
                    pages = new[] { "頭上顯示「蓄力中」的敵人下一次行動會放出全體大招。用坦克的「嘲諷」可以打斷蓄力，打斷後牠得重新蓄力；擊倒牠也能終止蓄力。" };
                    break;
                case 6:
                    pages = new[] { "護送關卡：保護目標撐過指定回合就勝利，陣亡則失敗。醫者的「屏障」會給目標一層護盾，護盾先於生命值吸收傷害，沒有時間限制。" };
                    break;
                case 7:
                    pages = new[] { "術士的「火攻」會疊燃燒層數：每個回合結束受到等同層數的固定傷害，然後層數減半。法術與燃燒都無視防禦，專門對付重甲。" };
                    break;
                case 8:
                    pages = new[] { "戰士的「橫斬」可以同時打到橫向相連的三格。點選敵人排中間那一格，左右兩側也會一起中招。" };
                    break;
                case 10:
                    pages = new[] { "Boss 會蓄力兩回合後放出全體大招，並帶著護衛。大招前用「嘲諷」打斷牠，其餘時間集中火力輸出。" };
                    break;
                default:
                    return;
            }
            Tutorial.Show(_root, key, "戰鬥教學", pages, speaker: "巴豆妖", model: "badou");
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
            if (card.Def.Target != TargetRule.Enemy && card.Def.Target != TargetRule.Ally) return;   // 其餘牌用右側詳情的「使用」按鈕
            var owner = card.Owner!;
            bool selfOk = card.Def.Target == TargetRule.Ally;
            if (!_battle.InBounds(pos) || (pos == owner.Pos && !selfOk) || Position.Distance(owner.Pos, pos) > owner.AttackRange)
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
            var local = _field.WorldToLocal(evt.position);
            float w = _field.layout.width, h = _field.layout.height;
            const float panelW = 330f, panelH = 400f;
            float left = local.x + 28f;
            if (left + panelW > w - 8f) left = local.x - 28f - panelW;
            float top = Mathf.Clamp(local.y - 40f, 8f, Mathf.Max(8f, h - panelH - 8f));
            _unitInfo.style.left = Mathf.Clamp(left, 8f, Mathf.Max(8f, w - panelW - 8f));
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

            string hp = $"生命 {unit.Hp}/{unit.MaxHp}" + (unit.Shield > 0 ? $"　護盾 {unit.Shield}" : "");
            _unitInfo.Add(new Label(hp) { pickingMode = PickingMode.Ignore }.WithClass("ui-hp"));

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("ui-grid");
            var st = unit.Stats;
            // 主屬性排在前面：吃謀略的單位（術士 / 軍師 / 醫者）先顯示謀略。
            bool caster = IsCaster(unit);
            AddStat(grid, "stat_" + (caster ? "int" : "atk"), caster ? st.Int : st.Atk, caster ? unit.EffectiveInt : unit.EffectiveAtk);
            AddStat(grid, "stat_" + (caster ? "atk" : "int"), caster ? st.Atk : st.Int, caster ? unit.EffectiveAtk : unit.EffectiveInt);
            AddStat(grid, "stat_def", st.Def, (int)Math.Round(unit.EffectiveDef));
            AddStat(grid, "stat_move", st.Move, st.Move);
            AddStat(grid, "射程", unit.AttackRange, unit.AttackRange);
            AddStat(grid, "閃避", st.Dodge, unit.EffectiveDodge, "%");
            AddStat(grid, "暴擊", st.Crit, unit.EffectiveCrit, "%");
            AddStat(grid, "暴傷", st.CritDmg, st.CritDmg, "%");
            _unitInfo.Add(grid);

            _unitInfo.Add(new Label("增減益") { pickingMode = PickingMode.Ignore }.WithClass("ui-sec"));
            int before = _unitInfo.childCount;
            foreach (var kv in unit.Statuses) AddStatusRow(UiIcons.Status(kv.Key), StatusLine(kv.Key, kv.Value));
            foreach (var b in unit.Buffs) AddStatusRow(UiIcons.Status(b.Type), BuffLine(b));
            foreach (var br in unit.DefBreaks) AddStatusRow("status_armorbreak", $"破甲　防禦 -{br.Percent * 100:0}%・剩 {br.Turns} 回合");
            if (unit.Side == Side.Enemy && unit.Charging) AddStatusRow("charge", "蓄力中");
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

        /// <summary>主屬性是謀略的單位（術士 / 軍師 / 醫者，或法術攻擊的敵人）。</summary>
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
                if (def.Target == TargetRule.Enemy || def.Target == TargetRule.Ally)
                {
                    for (int lane = 0; lane < _battle.Setup.Lanes; lane++)
                        for (int row = 0; row < _battle.Setup.Rows; row++)
                        {
                            var p = new Position(lane, row);
                            if (p != owner.Pos && Position.Distance(owner.Pos, p) <= owner.AttackRange) _previewRange.Add(p);
                        }
                }
                // 自動選目標的牌（最低血量、全體）直接標出會中招的單位；單體敵人牌由玩家點格，只標射程。
                if (def.Target != TargetRule.Enemy && def.Target != TargetRule.Ally)
                {
                    var targets = _battle.ResolveTargets(owner, def);
                    if (targets != null)
                        foreach (var u in targets) _previewTargets.Add(u.Pos);
                }
                else
                {
                    foreach (var u in _battle.AliveUnits(Side.Enemy))
                        if (Position.Distance(owner.Pos, u.Pos) <= owner.AttackRange) _previewTargets.Add(u.Pos);
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

        /// <summary>每幀把資訊貼到角色頭上（3D 位置 → 面板座標）。</summary>
        private void UpdateTagPositions()
        {
            if (_tagLayer == null || _field.resolvedStyle.width < 1) return;
            float width = _tagLayer.resolvedStyle.width, height = _tagLayer.resolvedStyle.height;
            if (width < 1 || height < 1) return;
            var occupied = new List<Rect>();
            foreach (var unit in _battle.Units.OrderBy(u => u.Side == Side.Enemy ? 0 : 1))
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                bool below = unit.Side == Side.Player;
                var point = unit.Alive ? (below ? _stage.UnitFootPanel(unit) : _stage.UnitHeadPanel(unit)) : null;
                if (point == null) { tag.Root.style.visibility = Visibility.Hidden; continue; }
                var anchor = _tagLayer.WorldToLocal(point.Value);
                if (anchor.x < 0 || anchor.x > width || anchor.y < 0 || anchor.y > height)
                { tag.Root.style.visibility = Visibility.Hidden; continue; }
                float tagWidth = below ? HeroTagWidth : TagWidth;
                float measuredHeight = tag.Root.resolvedStyle.height;
                float tagHeight = float.IsNaN(measuredHeight) || measuredHeight < 1 ? (below ? 40f : 82f) : measuredHeight;
                var desired = new Vector2(anchor.x - tagWidth * 0.5f, below ? anchor.y + 2f : anchor.y - tagHeight - 4f);
                Rect placement = default;
                bool found = false;
                // Keep the complete HUD inside the field, including after zoom/pan.
                // Search nearby free positions before allowing labels to overlap each other.
                for (int ring = 0; ring < 5 && !found; ring++)
                    for (int direction = 0; direction < (ring == 0 ? 1 : 4) && !found; direction++)
                    {
                        float dx = direction == 2 ? -ring * (tagWidth + 6) : direction == 3 ? ring * (tagWidth + 6) : 0;
                        float dy = direction == 0 ? -ring * (tagHeight + 6) : direction == 1 ? ring * (tagHeight + 6) : 0;
                        placement = new Rect(Mathf.Clamp(desired.x + dx, 4, Mathf.Max(4, width - tagWidth - 4)),
                            Mathf.Clamp(desired.y + dy, 4, Mathf.Max(4, height - tagHeight - 4)), tagWidth, tagHeight);
                        found = !occupied.Any(r => r.Overlaps(placement));
                    }
                tag.Root.style.visibility = found ? Visibility.Visible : Visibility.Hidden;
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
                // 蓄力中（含即將開始蓄力）只顯示「蓄力中」，不顯示傷害與範圍。
                case Intent.Kind.Charge:
                case Intent.Kind.Charging: host.Add(UiIcons.Chip("charge", "蓄力中")); break;
                case Intent.Kind.Move: host.Add(UiIcons.Chip("draw", "逼近")); break;
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
                    case EffectType.Shield: host.Add(UiIcons.Chip("armor", pct)); break;
                    case EffectType.ApplyStatus:
                    {
                        bool flat = e.Status == StatusType.DefUp || e.Status == StatusType.DodgeUp;
                        string value = flat ? e.Multiplier.ToString("0") : pct;
                        host.Add(UiIcons.Chip(UiIcons.Status(e.Status), e.Status == StatusType.Burn ? pct
                            : e.Multiplier > 0 ? $"{value}·{e.Amount}" : e.Amount.ToString()));
                    }
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
                row.Add(new Label(card.Owner?.Name ?? "全隊通用") { pickingMode = PickingMode.Ignore }.WithClass("sts-card-owner"));
                var description = CardText.Description(card.Def);
                var summary = CardText.Summary(card.Def);
                row.Add(new Label(summary.Length > 0 ? summary : CardText.Target(card.Def, card.Owner?.AttackRange ?? 1)) { pickingMode = PickingMode.Ignore }.WithClass("sts-card-description"));
                row.Add(new Label(card.Def.Target == TargetRule.MoveDest ? "移動" : card.Def.Basic ? "基本戰技" : "武將戰技") { pickingMode = PickingMode.Ignore }.WithClass("sts-card-type"));
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
            const float cardWidth = 204f;
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

        /// <summary>右側卡片詳情：選定技能後才出現，顯示射程圖、效果、關鍵字與操作提示。</summary>
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

            int attackRange = card.Owner != null ? card.Owner.AttackRange : 1;
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

        /// <summary>圖示 + 數值（有增減益時以綠 / 紅標示）：攻擊、謀略、防禦、移動力。主屬性（該職業實際吃的）放大。</summary>
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

        // ------------------------------------------------------------ 牌堆檢視（抽牌堆 / 棄牌堆，仿殺戮尖塔）

        private enum PileKind { Draw, Discard }

        /// <summary>牌堆按鈕：圖示 + 張數（點開檢視內容）。</summary>
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

        /// <summary>同一張牌（同武將、同名）合併成一格並標 ×N；依武將、名稱排序，所以抽牌堆看不出實際順序。</summary>
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
                hpBg.Add(new Label($"{unit.Hp}/{unit.MaxHp}" + (unit.Shield > 0 ? $"  盾 {unit.Shield}" : "")) { pickingMode = PickingMode.Ignore }.WithClass("bl-hp-text"));
                col.Add(hpBg);

                // 法系把「謀略」放在最前面，其餘把「攻擊」放最前面（與傷害 / 治療實際吃的屬性一致）。
                var stats = new VisualElement { pickingMode = PickingMode.Ignore };
                stats.AddToClassList("bl-stats");
                // 順序固定（攻擊、謀略、防禦、移動力），大小一致；該職業實際吃的屬性（法系 = 謀略）用金色標出。
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
            var actions = _recorder.Actions.ToList();
            _ = FinishStage(_overlay, _stageId, () => GameSession.Backend.FinishStage(_stageId, actions));
        }

        /// <summary>Debug 版本才有：跳過戰鬥直接當作三星勝利結算（後端不重播，只有單機版接受）。</summary>
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

        /// <summary>把操作紀錄交給後端結算（勝負與星數由後端自己重播算出），再顯示結果。</summary>
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
            if (_overlay != overlay) return; // 期間已離開這一局
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
                case EventType.Shield: return $"{NameOf(e.Target)} 獲得護盾 {e.Value}";
                case EventType.StatusApplied: return $"{NameOf(e.Target)} 受到 {StatusFromText(e.Text)}";
                case EventType.Draw: return $"抽了 {e.Value} 張牌";
                case EventType.GainCost: return $"獲得 {e.Value} 費";
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

        // ------------------------------------------------------------ 截圖 / 除錯用

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

        // Screenshot-only hand stress: never recorded or submitted to the backend.
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
            var visible = new List<Rect>();
            foreach (var tag in _tags.Values)
            {
                if (tag.Root.resolvedStyle.visibility != Visibility.Visible) continue;
                var bounds = tag.Root.worldBound;
                var field = _field.worldBound;
                if (bounds.xMin < field.xMin - 1 || bounds.xMax > field.xMax + 1 || bounds.yMin < field.yMin - 1 || bounds.yMax > field.yMax + 1)
                    throw new InvalidOperationException("Battle HUD escaped the field.");
                if (visible.Any(r => r.Overlaps(bounds))) throw new InvalidOperationException("Battle HUD labels overlap.");
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
