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
            public VisualElement HpFill = null!;
            public Label HpText = null!;
            public VisualElement Extra = null!;
            public VisualElement Intent = null!;
        }

        private const float TagWidth = 132f;
        private const float TagHeight = 62f;

        private readonly VisualElement _root;
        private readonly BattleStage _stage;
        private readonly Dictionary<int, UnitTag> _tags = new Dictionary<int, UnitTag>();
        private readonly List<string> _log = new List<string>();

        private readonly IGameBackend _backend;
        private ProfileView _view = new ProfileView();
        private Battle _battle = null!;
        /// <summary>null = 只是地圖後面的預覽戰場（沒有開始關卡），不能操作。</summary>
        private ReplayRecorder? _recorder;
        private MetaScreens _meta = null!;
        private bool _busy;
        private ulong _seed;
        private int _level = 1;
        private string _stageId = "1-1";
        /// <summary>目前進行的是資源副本時不為 null（主線關卡為 null）。</summary>
        private ResourceDungeonDef? _dungeon;
        private int _eventCursor;
        private bool _auto;
        private readonly Dictionary<string, Position> _formation = new Dictionary<string, Position>();
        private HashSet<(Side, int, int)> _previewTargets = new HashSet<(Side, int, int)>();

        private VisualElement _content = null!;
        private VisualElement _field = null!;
        private VisualElement _tagLayer = null!;
        private BattleFx _fx = null!;
        private VisualElement _hand = null!;
        private Label _title = null!;
        private VisualElement _cost = null!;
        private VisualElement _piles = null!;
        private Label _logLabel = null!;
        private Button _autoButton = null!;
        private Button _formationButton = null!;
        private VisualElement? _overlay;

        public BattleScreen(VisualElement root, BattleStage stage, IGameBackend backend)
        {
            _root = root;
            _stage = stage;
            _backend = backend;
            _seed = 1;
            LoadRoster();
            BuildStatic();
            _meta = new MetaScreens(_root, backend, () => _view, RefreshProfile, Toast, OpenMap, EnterDungeon);
            StartBattle(recording: false);
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
            buttons.Add(MakeButton("地圖", OpenMap));
            _formationButton = MakeButton("編隊", OpenFormation);
            buttons.Add(_formationButton);
            buttons.Add(MakeButton("重來", () => { _ = BeginStageId(_stageId); }));
            header.Add(buttons);
            _content.Add(header);

            // 戰場區：3D 畫在這塊後面（透明）。
            _field = new VisualElement();
            _field.AddToClassList("field");
            _content.Add(_field);

            // 右下角：手牌 + 費用 / 結束回合；左下角：戰鬥紀錄。
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("bottom");

            var info = new VisualElement();
            info.AddToClassList("info");
            _cost = new VisualElement();
            _cost.AddToClassList("cost-label");
            _piles = new VisualElement();
            _piles.AddToClassList("pile-label");
            info.Add(_cost);
            info.Add(_piles);
            info.Add(MakeButton("結束回合", EndTurn, primary: true));
            bottom.Add(info);

            _hand = new VisualElement();
            _hand.AddToClassList("hand");
            bottom.Add(_hand);
            _content.Add(bottom);

            var log = new VisualElement { pickingMode = PickingMode.Ignore };
            log.AddToClassList("log");
            _logLabel = new Label { pickingMode = PickingMode.Ignore };
            _logLabel.AddToClassList("log-line");
            log.Add(_logLabel);
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
                var nameRow = new VisualElement { pickingMode = PickingMode.Ignore }; nameRow.AddToClassList("tag-name-row");
                nameRow.Add(tag.Role); nameRow.Add(tag.Name);
                tag.Root.Add(nameRow);
                tag.Root.Add(hpBg);
                tag.Root.Add(tag.Extra);
                tag.Root.Add(tag.Intent);
                _tagLayer.Add(tag.Root);
                _tags[unit.Id] = tag;
            }
        }

        // ------------------------------------------------------------ 流程

        private static string StageIdOf(int level) => DemoMeta.StageId(1, level);

        /// <summary>進入關卡：可編隊的關卡先開編隊畫面（開戰才扣體力）；鎖定編隊的直接開戰。</summary>
        private void EnterLevel(int level)
        {
            if (_busy || level < 1 || level > DemoContent.ChapterLevelCount) return;
            if (DemoContent.Level(level, 1).FormationLocked) { _ = BeginStage(level); return; }
            _level = level;
            OpenFormation();
        }

        private Task BeginStage(int level) => BeginStageId(StageIdOf(level));

        /// <summary>開始資源副本戰鬥（由招募 / 武將以外的「資源副本」畫面呼叫）。</summary>
        public void EnterDungeon(string dungeonId)
        {
            if (_busy) return;
            _meta.Close();
            _ = BeginStageId(dungeonId);
        }

        /// <summary>向後端開始關卡 / 副本（檢查條件、扣體力、取得種子），成功才開打並開始錄操作。</summary>
        private async Task BeginStageId(string stageId)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var r = await _backend.StartStage(stageId);
                if (!r.Ok) { Toast(ExplainBackend(r.Code)); return; }
                _stageId = stageId;
                _dungeon = DemoMeta.FindDungeon(stageId);
                if (_dungeon == null && int.TryParse(stageId.Substring(stageId.IndexOf('-') + 1), out int level)) _level = level;
                _seed = r.Seed;
                _mapPanel?.RemoveFromHierarchy();
                _mapPanel = null;
                _formationPanel?.RemoveFromHierarchy();
                _formationPanel = null;
                StartBattle(recording: true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast(ExplainBackend("network"));
            }
            finally
            {
                _busy = false;
            }
        }

        internal static string MaterialName(string key)
        {
            switch (key)
            {
                case HeroGrowth.ExpBook: return "經驗書";
                case HeroGrowth.CardMaterial: return "卡牌強化素材";
                default: return key.StartsWith("shard:") ? "突破碎片" : key;
            }
        }

        internal static string ExplainBackend(string code)
        {
            switch (code)
            {
                case "NotEnoughStamina": return "體力不足";
                case "LevelTooLow": return "帳號等級不足";
                case "NotThreeStars": return "三星通關才能掃蕩";
                case "InvalidCount": return "掃蕩次數不合法";
                case "NotEnoughYuanbao": return "元寶不足";
                case "NotEnoughGold": return "金幣不足";
                case "NotEnoughMaterial": return "素材不足";
                case "NeedsPlayerLevel": return "武將等級不能超過帳號等級";
                case "AtCap": return "已達上限";
                case "NotOpenToday": return "今天不開放";
                case "LimitReached": return "今日次數已用完";
                case "NotCleared": return "尚未通關，無法掃蕩";
                case "NotComplete": return "尚未達成";
                case "AlreadyClaimed": return "已經領取過了";
                case "NotUnlocked": return "尚未開放";
                case "unknown_dungeon": return "沒有這個副本";
                case "AlreadyOwned": return "已經購買過了";
                case "NotPaid": return "尚未購買";
                case "NotActive": return "月卡尚未生效或已到期";
                case "AlreadyClaimedToday": return "今天已經領過了";
                case "UnknownProduct": return "沒有這個商品";
                case "UnknownOrder": return "找不到訂單";
                case "disabled": return "測試付款未開啟";
                case "unknown_stage": return "沒有這個關卡";
                case "no_pending_stage": return "沒有進行中的關卡";
                case "invalid_replay": return "操作紀錄驗證失敗，本局無效";
                case "network": return "連線失敗，請稍後再試";
                default: return "失敗：" + code;
            }
        }

        private void StartBattle(bool recording)
        {
            var setup = _dungeon != null ? DemoMeta.DungeonSetup(_seed) : DemoContent.Level(_level, _seed);
            if (!setup.FormationLocked) ApplyFormation(setup);
            _battle = new Battle(setup);
            _recorder = recording ? new ReplayRecorder(_battle) : null;
            _finishing = false;
            // 教學關：隊伍固定、不開放自動戰鬥（之後再開放）。
            _formationButton.style.display = setup.FormationLocked ? DisplayStyle.None : DisplayStyle.Flex;
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
        }

        // ------------------------------------------------------------ 戰前編隊與大地圖

        private const int MaxTeamSize = 4;

        private readonly List<HeroDef> _rosterList = DemoContent.Roster();
        private readonly Dictionary<string, HeroDef> _roster = new Dictionary<string, HeroDef>();
        private bool _finishing;
        private VisualElement? _formationPanel;
        private VisualElement? _mapPanel;
        private string? _formationPick;
        private string _formationMessage = "";

        private VisualElement? _stagePanel;

        private bool Blocked => _recorder == null || _busy || _formationPanel != null || _mapPanel != null || _stagePanel != null || _meta.IsOpen;

        private void LoadRoster()
        {
            foreach (var h in _rosterList) _roster[h.Id] = h;
        }

        private bool IsUnlocked(int level) => level == 1 || _view.ClearedStages.Contains(StageIdOf(level - 1));

        private async Task RefreshProfile()
        {
            try
            {
                var v = await _backend.GetProfile();
                if (v != null) _view = v;
                else Toast(ExplainBackend("network"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast(ExplainBackend("network"));
            }
        }

        /// <summary>把玩家排好的隊伍與站位套用到關卡設定；第一次使用關卡的預設隊伍。</summary>
        private void ApplyFormation(BattleSetup setup)
        {
            if (_formation.Count == 0)
                foreach (var h in setup.Heroes) _formation[h.Def.Id] = h.Pos;
            setup.Heroes.Clear();
            foreach (var def in _rosterList.Where(d => _formation.ContainsKey(d.Id)))
                setup.Heroes.Add(new HeroSlot(def, _formation[def.Id]));
        }

        private string? HeroAtCell(int lane, int row)
        {
            foreach (var kv in _formation)
                if (kv.Value.Lane == lane && kv.Value.Row == row) return kv.Key;
            return null;
        }

        private static string HeroLabel(HeroDef h) => $"{h.Name}　{h.Rarity} {CardText.RoleName(h.Role)}";

        // ---- 編隊 ----

        public void OpenFormation()
        {
            // 第一次進來先把預設隊伍填好。
            if (_formation.Count == 0) ApplyFormation(DemoContent.Level(_level, _seed));
            _formationPick = null;
            _formationMessage = "";
            _mapPanel?.RemoveFromHierarchy();
            _mapPanel = null;
            _formationPanel?.RemoveFromHierarchy();
            _formationPanel = new VisualElement();
            _formationPanel.AddToClassList("overlay");
            _formationPanel.AddToClassList("formation-overlay");
            BuildFormationContent();
            _root.Add(_formationPanel);
        }

        private void BuildFormationContent()
        {
            var panel = _formationPanel!;
            panel.Clear();
            var level = DemoContent.Level(_level, _seed);

            var title = new Label($"排兵布陣　第 {_level} 關　{DemoContent.LevelNames[_level - 1]}");
            title.AddToClassList("formation-title");
            panel.Add(title);

            var foes = level.Enemies.GroupBy(e => e.Def.Name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key);
            var foeLabel = new Label("敵方：" + string.Join("、", foes));
            foeLabel.AddToClassList("formation-hint");
            panel.Add(foeLabel);
            var hint = new Label(_formationMessage.Length > 0 ? _formationMessage
                : "同路沒有對手時，攻擊會落在最上方（第 1 路）；點武將再點格子可移動 / 換位，最多上場 4 人");
            hint.AddToClassList("formation-hint");
            if (_formationMessage.Length > 0) hint.AddToClassList("formation-warn");
            panel.Add(hint);

            var head = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            head.Add(FormationLabel("", 120));
            head.Add(FormationLabel("後排", 260));
            head.Add(FormationLabel("前排", 260));
            panel.Add(head);

            for (int lane = 0; lane < level.Lanes; lane++)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(FormationLabel($"第 {lane + 1} 路", 120));
                for (int r = level.Rows - 1; r >= 0; r--) // 後排在左、前排在右（前排朝向敵人）
                {
                    int l = lane, rr = r;
                    string? id = HeroAtCell(lane, r);
                    var cell = new Button(() => OnFormationCell(l, rr)) { text = id == null ? "—" : HeroLabel(_roster[id]) };
                    cell.AddToClassList("formation-cell");
                    if (id == null) cell.AddToClassList("formation-cell-empty");
                    if (id != null && id == _formationPick) cell.AddToClassList("btn-on");
                    row.Add(cell);
                }
                panel.Add(row);
            }

            var benchTitle = new Label("待命武將（點選後再點上方格子上場；先點場上武將再點這裡可下場）");
            benchTitle.AddToClassList("formation-hint");
            benchTitle.style.marginTop = 14;
            panel.Add(benchTitle);
            var bench = new VisualElement();
            bench.AddToClassList("formation-bench");
            foreach (var def in _rosterList.Where(d => !_formation.ContainsKey(d.Id)))
            {
                string id = def.Id;
                var chip = new Button(() => OnBenchChip(id)) { text = HeroLabel(def) };
                chip.AddToClassList("formation-chip");
                if (id == _formationPick) chip.AddToClassList("btn-on");
                bench.Add(chip);
            }
            panel.Add(bench);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            buttons.Add(MakeButton("回地圖", OpenMap));
            buttons.Add(MakeButton("開戰", () =>
            {
                if (_formation.Count == 0) { _formationMessage = "至少要有 1 名武將上場"; BuildFormationContent(); return; }
                _ = BeginStageId(StageIdOf(_level));
            }, primary: true));
            panel.Add(buttons);
        }

        private static Label FormationLabel(string text, float width)
        {
            var l = new Label(text);
            l.AddToClassList("formation-label");
            l.style.width = width;
            return l;
        }

        private void OnFormationCell(int lane, int row)
        {
            _formationMessage = "";
            string? occupant = HeroAtCell(lane, row);
            if (_formationPick == null)
            {
                if (occupant != null) _formationPick = occupant;
            }
            else
            {
                bool pickOnBoard = _formation.ContainsKey(_formationPick);
                if (occupant == _formationPick)
                {
                    // 再點一次 = 取消選取
                }
                else if (pickOnBoard)
                {
                    var from = _formation[_formationPick];
                    if (occupant != null) _formation[occupant] = from; // 落在隊友格 → 換位
                    _formation[_formationPick] = new Position(lane, row);
                }
                else
                {
                    if (occupant != null) _formation.Remove(occupant); // 替換：原本的人下場
                    else if (_formation.Count >= MaxTeamSize)
                    {
                        _formationMessage = $"場上最多 {MaxTeamSize} 人，請先讓一名武將下場";
                        BuildFormationContent();
                        return;
                    }
                    _formation[_formationPick] = new Position(lane, row);
                }
                _formationPick = null;
            }
            BuildFormationContent();
        }

        private void OnBenchChip(string id)
        {
            _formationMessage = "";
            if (_formationPick == null) _formationPick = id;
            else if (_formation.ContainsKey(_formationPick))
            {
                if (_formation.Count > 1) _formation.Remove(_formationPick);
                else _formationMessage = "至少要有 1 名武將上場";
                _formationPick = null;
            }
            else _formationPick = id == _formationPick ? null : id;
            BuildFormationContent();
        }

        // ---- 大地圖 ----

        public void OpenMap() => _ = ShowMap();

        private static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);

        private string ProfileLine() =>
            $"Lv.{_view.Level}　經驗 {_view.Exp}/{_view.ExpToNext}　體力 {_view.Stamina}/{_view.StaminaCap}　金幣 {_view.Gold}　元寶 {_view.Yuanbao}　[{_backend.Name}]";

        private async Task ShowMap()
        {
            if (_busy) return;
            _busy = true;
            try { await RefreshProfile(); }
            finally { _busy = false; }

            _formationPanel?.RemoveFromHierarchy();
            _formationPanel = null;
            _stagePanel?.RemoveFromHierarchy();
            _stagePanel = null;
            _mapPanel?.RemoveFromHierarchy();
            _mapPanel = new VisualElement();
            _mapPanel.AddToClassList("overlay");
            _mapPanel.AddToClassList("formation-overlay");

            var title = new Label("第一章　黃巾之亂");
            title.AddToClassList("formation-title");
            _mapPanel.Add(title);
            var profile = new Label(ProfileLine());
            profile.AddToClassList("formation-hint");
            _mapPanel.Add(profile);
            var hint = new Label("打贏一關才會開啟下一關；三星通關後可掃蕩");
            hint.AddToClassList("formation-hint");
            _mapPanel.Add(hint);
            var menu = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            menu.Add(MakeButton("招募", () => { _mapPanel?.RemoveFromHierarchy(); _mapPanel = null; _meta.OpenGacha(); }));
            menu.Add(MakeButton("武將", () => { _mapPanel?.RemoveFromHierarchy(); _mapPanel = null; _meta.OpenHeroes(); }));
            menu.Add(MakeButton("副本", () => { _mapPanel?.RemoveFromHierarchy(); _mapPanel = null; _meta.OpenDungeons(); }));
            menu.Add(MakeButton("任務", () => { _mapPanel?.RemoveFromHierarchy(); _mapPanel = null; _meta.OpenQuests(); }));
            menu.Add(MakeButton("商店", () => { _mapPanel?.RemoveFromHierarchy(); _mapPanel = null; _meta.OpenShop(); }));
            _mapPanel.Add(menu);

            var field = new VisualElement();
            field.AddToClassList("map-field");
            int total = DemoContent.LevelNames.Length;
            var centers = new List<Vector2>();
            for (int i = 0; i < total; i++)
                centers.Add(new Vector2(90 + i * 169f, 250 + Mathf.Sin(i * 0.9f) * 140f));

            for (int i = 0; i < total - 1; i++)
            {
                for (int k = 1; k <= 3; k++)
                {
                    var p = Vector2.Lerp(centers[i], centers[i + 1], k / 4f);
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("map-dot");
                    dot.style.left = p.x - 5; dot.style.top = p.y - 5;
                    field.Add(dot);
                }
            }

            for (int i = 0; i < total; i++)
            {
                int level = i + 1;
                bool implemented = level <= DemoContent.ChapterLevelCount;
                int stars = _view.StarsOf(StageIdOf(level));
                bool cleared = _view.ClearedStages.Contains(StageIdOf(level));
                bool open = implemented && IsUnlocked(level);
                bool boss = level == total;
                float size = boss ? 118 : 92;

                var node = new Button(() => { if (open) OpenStageDetail(level); }) { text = level.ToString() };
                node.AddToClassList("map-node");
                node.AddToClassList(cleared ? "map-node-clear" : open ? "map-node-open" : "map-node-lock");
                if (boss) node.AddToClassList("map-node-boss");
                node.style.width = size; node.style.height = size;
                node.style.borderTopLeftRadius = size / 2; node.style.borderTopRightRadius = size / 2;
                node.style.borderBottomLeftRadius = size / 2; node.style.borderBottomRightRadius = size / 2;
                node.style.left = centers[i].x - size / 2;
                node.style.top = centers[i].y - size / 2;
                field.Add(node);

                string status = cleared ? Stars(stars) : !implemented ? "未開放" : open ? "可挑戰" : "未解鎖";
                var caption = new Label($"{DemoContent.LevelNames[i]}\n{status}") { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("map-caption");
                caption.style.left = centers[i].x - 80;
                caption.style.top = centers[i].y + size / 2 + 4;
                field.Add(caption);
            }
            _mapPanel.Add(field);
            _root.Add(_mapPanel);
        }

        // ---- 關卡資訊（挑戰 / 掃蕩）----

        private void OpenStageDetail(int level)
        {
            _stagePanel?.RemoveFromHierarchy();
            var stage = DemoMeta.Chapter1Stage(level);
            int stars = _view.StarsOf(stage.StageId);
            _stagePanel = new VisualElement();
            _stagePanel.AddToClassList("overlay");

            var title = new Label($"第 {level} 關　{DemoContent.LevelNames[level - 1]}");
            title.AddToClassList("overlay-text");
            _stagePanel.Add(title);
            AddInfo(_stagePanel, $"最高星數 {Stars(stars)}　　消耗體力 {stage.StaminaCost}（現有 {_view.Stamina}）");
            AddInfo(_stagePanel, $"獎勵：經驗 {stage.Exp}　金幣 {stage.Gold}" + (_view.ClearedStages.Contains(stage.StageId) ? "" : $"　首通元寶 {stage.FirstClearYuanbao}"));
            string par = stage.StarTurnPar > 0 ? $"　三星：{stage.StarTurnPar} 回合內" : "";
            AddInfo(_stagePanel, "★ 通關　★★ 無武將陣亡" + par);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(MakeButton("挑戰", () => { CloseStageDetail(); EnterLevel(level); }, primary: true));
            if (stars >= 3)
            {
                row.Add(MakeButton("掃蕩 ×1", () => _ = SweepStage(level, 1)));
                row.Add(MakeButton("掃蕩 ×10", () => _ = SweepStage(level, PlayerProfile.MaxSweepCount)));
            }
            row.Add(MakeButton("返回", CloseStageDetail));
            _stagePanel.Add(row);
            _root.Add(_stagePanel);
        }

        private void CloseStageDetail()
        {
            _stagePanel?.RemoveFromHierarchy();
            _stagePanel = null;
        }

        private static void AddInfo(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("formation-hint");
            parent.Add(l);
        }

        private async Task SweepStage(int level, int count)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var r = await _backend.Sweep(StageIdOf(level), count);
                if (!r.Ok) { Toast(ExplainBackend(r.Code)); return; }
                await RefreshProfile();
                Toast($"掃蕩 ×{count}：經驗 +{r.Exp}　金幣 +{r.Gold}" + (r.LevelsGained > 0 ? $"　升 {r.LevelsGained} 級！" : ""));
                CloseStageDetail();
                _busy = false;
                await ShowMap();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast(ExplainBackend("network"));
            }
            finally
            {
                _busy = false;
            }
        }

        private void EndTurn()
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
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
            var result = _recorder!.Play(card);
            if (result != PlayResult.Ok) { Toast(Explain(result)); Refresh(); return; }
            _previewTargets.Clear();
            PumpEvents();
            Refresh();
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
                if (!card.Def.Basic) el.AddToClassList("card-skill");
                if (ok != PlayResult.Ok) el.AddToClassList("card-disabled");

                var cost = new Label(card.Def.Cost.ToString());
                cost.AddToClassList("card-cost");
                var costIcon = UiIcons.Get("cost");
                if (costIcon != null) cost.style.backgroundImage = new StyleBackground(costIcon);
                var tag = UiIcons.Icon(card.Def.Basic ? "damage" : "charge", "card-tag");
                var top = new VisualElement { pickingMode = PickingMode.Ignore };
                top.AddToClassList("card-top");
                top.Add(cost);
                top.Add(tag);

                // 卡面插圖：Resources/HeroArt/<heroId>.png（占位 / 正式美術都放這裡），沒有圖就維持純色。
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("card-art");
                var portrait = Resources.Load<Texture2D>("HeroArt/" + card.Owner.DefId);
                if (portrait != null) art.style.backgroundImage = new StyleBackground(portrait);
                var name = new Label(card.Def.Name);
                name.AddToClassList("card-name");
                var owner = new Label(card.Owner.Name + (card.Owner.Alive ? "" : "（陣亡）"));
                owner.AddToClassList("card-owner");
                var target = new Label(CardText.Target(card.Def));
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
                el.Add(top);
                el.Add(name);
                el.Add(owner);
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
            _cost.Add(UiIcons.Chip("cost", $"{_battle.Cost}/{_battle.Setup.CostCap}", "chip-big"));
            _piles.Clear();
            _piles.Add(UiIcons.Chip("draw", _battle.DrawPile.Count.ToString()));
            _piles.Add(UiIcons.Chip("kw_retain", _battle.DiscardPile.Count.ToString()));
            _piles.Add(UiIcons.Chip("kw_exhaust", _battle.ExhaustPile.Count.ToString()));
            _logLabel.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 4)));
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
                result = await _backend.FinishStage(stageId, actions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = new FinishStageResult { Code = "network" };
            }
            if (_overlay != overlay) return; // 期間已離開這一局
            try { await RefreshProfile(); } catch { /* 已在內部處理 */ }
            if (_overlay != overlay) return;

            overlay.Clear();
            if (!result.Ok)
            {
                var err = new Label(ExplainBackend(result.Code));
                err.AddToClassList("overlay-text");
                overlay.Add(err);
                overlay.Add(MakeButton("回地圖", OpenMap, primary: true));
                return;
            }

            var text = new Label(result.Won ? "勝利" : "敗北");
            text.AddToClassList("overlay-text");
            overlay.Add(text);
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (result.Won && dungeon != null)
            {
                var gains = new List<string>();
                if (result.Gold > 0) gains.Add($"金幣 +{result.Gold}");
                if (result.Yuanbao > 0) gains.Add($"元寶 +{result.Yuanbao}");
                foreach (var m in result.Materials) gains.Add($"{MaterialName(m.Key)} +{m.Value}");
                AddInfo(overlay, "獎勵：" + string.Join("　", gains));
            }
            else if (result.Won)
            {
                AddInfo(overlay, Stars(result.Stars) + (result.FirstClear ? "　首次通關" : ""));
                AddInfo(overlay, $"經驗 +{result.Exp}　金幣 +{result.Gold}" + (result.Yuanbao > 0 ? $"　元寶 +{result.Yuanbao}" : ""));
                if (result.LevelsGained > 0) AddInfo(overlay, $"帳號升級！Lv.{_view.Level}（體力已回滿）");
            }

            if (dungeon != null)
            {
                overlay.Add(MakeButton("再打一次", () => _ = BeginStageId(stageId), primary: true));
                overlay.Add(MakeButton("回地圖", OpenMap));
                return;
            }
            int level = _level;
            bool hasNext = result.Won && level < DemoContent.ChapterLevelCount;
            if (hasNext) overlay.Add(MakeButton("下一關", () => EnterLevel(level + 1), primary: true));
            overlay.Add(MakeButton("再打一次", () => EnterLevel(level), primary: !hasNext));
            overlay.Add(MakeButton("回地圖", OpenMap));
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

        public void DebugSetLevel(int level) => EnterLevel(level);

        public void DebugPlayFirstPlayable()
        {
            var card = _battle.Hand.FirstOrDefault(c => _battle.CanPlay(c) == PlayResult.Ok);
            if (card != null) OnCardClicked(card);
        }

        public void DebugOpenFormation() => OpenFormation();
        public void DebugOpenMap() => OpenMap();

        /// <summary>截圖用：開啟養成 / 商店畫面（gacha、heroes、dungeons、quests、shop）；gacha 會先抽一次十連。</summary>
        public async void DebugOpenMeta(string name)
        {
            _mapPanel?.RemoveFromHierarchy();
            _mapPanel = null;
            await RefreshProfile();
            switch (name)
            {
                case "gacha": _meta.OpenGacha(); await _meta.DebugTenPull(); break;
                case "heroes": _meta.OpenHeroes(); break;
                case "dungeons": _meta.OpenDungeons(); break;
                case "quests": _meta.OpenQuests(); break;
                case "shop": _meta.OpenShop(); break;
            }
        }

        public void DebugPreviewFirstCard()
        {
            var card = _battle.Hand.FirstOrDefault();
            if (card != null) Preview(card);
        }
    }
}
