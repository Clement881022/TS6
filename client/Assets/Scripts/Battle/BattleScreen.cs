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
            public VisualElement? AnchorLine;
            public float LastLeft = float.NaN, LastTop = float.NaN;
        }

        private const float TagWidth = 200f;
        private const float HeroTagWidth = 200f;

        private readonly VisualElement _root;
        private readonly BattleStage _stage;
        private readonly Dictionary<int, UnitTag> _tags = new Dictionary<int, UnitTag>();
        private readonly List<string> _log = new List<string>();

        private Battle _battle = null!;
        private ReplayRecorder? _recorder;
        private bool _busy;
        private ulong _seed;
        private int _chapter;
        private int _level = 1;
        private string _stageId = "1-1";
        private ResourceDungeonDef? _dungeon;
        private List<FormationEntry>? _formation;
        private int _eventCursor;
        private bool _auto;
        private readonly HashSet<Position> _previewTargets = new HashSet<Position>();
        private readonly HashSet<Position> _previewRange = new HashSet<Position>();
        private readonly HashSet<Position> _previewReach = new HashSet<Position>();
        private CardInstance? _pendingCard;
        private Unit? _pendingMover;
        private Unit? _infoUnit;
        private VisualElement _unitInfo = null!;
        private VisualElement _unitList = null!;
        private Side _listSide = Side.Player;
        private Label _hint = null!;
        private Button _objectiveButton = null!;
        private bool _objectiveHint;

        private VisualElement _content = null!;
        private VisualElement _field = null!;
        private VisualElement _tagLayer = null!;
        private BattleFx _fx = null!;
        private VisualElement _hand = null!;
        private readonly List<VisualElement> _handCards = new List<VisualElement>();
        private VisualElement? _hoverCard;
        private VisualElement _detail = null!;
        private Button _endButton = null!;
        private Label _title = null!;
        private VisualElement _cost = null!;
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
            _root.schedule.Execute(() => { UpdateTagPositions(); _aimLayer?.MarkDirtyRepaint(); }).Every(16);
        }

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
            var buttons = new VisualElement();
            var menuPanel = new VisualElement().WithClass("battle-menu-panel");
            menuPanel.style.display = DisplayStyle.None;
            var menu = MakeButton("選單", () => { menuPanel.style.display = menuPanel.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; menuPanel.BringToFront(); });
            menu.AddToClassList("battle-menu");
            buttons.Add(menu);
            _autoButton = MakeButton("自動", ToggleAuto);
            menuPanel.Add(_autoButton);
            menuPanel.Add(MakeButton("重置視角", () => _stage.ResetView()));
            _objectiveButton = MakeButton("勝利目標", () => { menuPanel.style.display = DisplayStyle.None; ShowObjective(); });
            menuPanel.Add(_objectiveButton);
            menuPanel.Add(MakeButton("撤退", Leave));
            menuPanel.Add(MakeButton("重來", () => { _ = BeginStageId(_stageId); }));
            _content.Add(menuPanel);
            header.Add(buttons);
            _content.Add(header);

            _field = new VisualElement();
            _field.AddToClassList("field");
            _field.style.marginLeft = 24f;
            _field.style.marginRight = 18f;
            _field.style.marginTop = 108f;
            _field.style.marginBottom = 316f;
            _field.RegisterCallback<ClickEvent>(OnFieldClicked);
            _field.RegisterCallback<PointerDownEvent>(OnFieldDown);
            _field.RegisterCallback<PointerMoveEvent>(OnFieldMove);
            _field.RegisterCallback<PointerUpEvent>(OnFieldUp);
            _field.RegisterCallback<WheelEvent>(OnFieldWheel);
            _content.Add(_field);
            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);

            _hint = new Label { pickingMode = PickingMode.Ignore }.WithClass("bl-hint");
            _hint.style.display = DisplayStyle.None;
            _content.Add(_hint);

            var leftDock = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("bl-dock-left");
            _cost = new VisualElement { pickingMode = PickingMode.Ignore };
            _cost.AddToClassList("bl-cost");
            leftDock.Add(_cost);
            _drawButton = PileButton("pile_draw", PileKind.Draw, out _drawCount);
            leftDock.Add(_drawButton);
            _content.Add(leftDock);

            var rightDock = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("bl-dock-right");
            _endButton = new Button(EndTurn) { text = "結束回合" };
            _endButton.AddToClassList("bl-end");
            rightDock.Add(_endButton);
            _discardButton = PileButton("pile_discard", PileKind.Discard, out _discardCount);
            rightDock.Add(_discardButton);
            _content.Add(rightDock);

            _hand = new VisualElement();
            _hand.AddToClassList("sts-hand");
            _hand.RegisterCallback<GeometryChangedEvent>(_ => LayoutHand());

            _detail = new VisualElement();
            _detail.AddToClassList("bl-detail");
            _detail.style.display = DisplayStyle.None;
            _content.Add(_detail);

            _unitList = new VisualElement().WithClass("ul-panel");
            _content.Add(_unitList);
            _content.Add(_hand);

            var log = new VisualElement { pickingMode = PickingMode.Ignore };
            log.AddToClassList("bl-log");
            _logLabel = new Label { pickingMode = PickingMode.Ignore };
            _logLabel.AddToClassList("bl-log-line");
            log.Add(_logLabel);
            _logBox = log;
            _content.Add(log);

            _tagLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _tagLayer.AddToClassList("tag-layer");
            _field.Add(_tagLayer);

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
            BuildAimLayer();
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
                if (unit.Side == Side.Enemy) tag.Root.Add(tag.Intent);
                tag.Root.Add(mainRow);
                hpBg.Remove(tag.HpText);
                tag.Root.Add(tag.HpText);
                tag.Root.Add(tag.Extra);
                _tagLayer.Add(tag.Root);
                _tags[unit.Id] = tag;
            }
        }

        private void ApplyTicket(BattleTicket ticket)
        {
            _stageId = ticket.StageId;
            _dungeon = ticket.Dungeon;
            _formation = ticket.Formation;
            _chapter = ticket.Chapter;
            _level = ticket.Level;
            _seed = ticket.Seed;
        }

        private void Leave() => Nav.Go(IsWorldBoss ? Page.WorldBoss : _dungeon != null ? Page.Dungeons : Page.Map);

        private bool IsWorldBoss => _stageId == WorldBoss.StageId;

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
            var setup = (recording ? DemoMeta.BuildSetup(_stageId, _seed, GameSession.View.Raw, _formation) : null)
                ?? Campaign.Setup(_chapter, _level, _seed);
            _battle = new Battle(setup);
            _recorder = recording ? new ReplayRecorder(_battle) : null;
            _finishing = false;
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
            _objectiveHint = false;
            _stage.Bind(_battle, _root, _field);
            BuildTags();
            PumpEvents();
            Refresh();
            if (_dungeon == null && !IsWorldBoss && _chapter == 0) ShowLevelTutorial();
        }

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
            if (_fx.PendingSeconds > 0.05f) return;
            var (card, target, mover) = AutoPlayer.Pick(_battle);
            if (card != null) _recorder!.Play(card, target, mover);
            else _recorder!.EndTurn();
            PumpEvents();
            Refresh();
        }
    }
}
