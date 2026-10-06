#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
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
            public VisualElement HpFill = null!;
            public Label HpText = null!;
            public Label Extra = null!;
            public Label Intent = null!;
        }

        private const float TagWidth = 132f;
        private const float TagHeight = 62f;

        private readonly VisualElement _root;
        private readonly BattleStage _stage;
        private readonly Dictionary<int, UnitTag> _tags = new Dictionary<int, UnitTag>();
        private readonly List<string> _log = new List<string>();

        private Battle _battle = null!;
        private ulong _seed;
        private int _level = 1;
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
        private Label _cost = null!;
        private Label _piles = null!;
        private Label _logLabel = null!;
        private Button _autoButton = null!;
        private VisualElement? _overlay;

        public BattleScreen(VisualElement root, BattleStage stage, ulong seed)
        {
            _root = root;
            _stage = stage;
            _seed = seed;
            LoadProgress();
            BuildStatic();
            StartBattle();
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
            buttons.Add(MakeButton("編隊", OpenFormation));
            buttons.Add(MakeButton("重來", () => { _seed++; StartBattle(); }));
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
            _cost = new Label();
            _cost.AddToClassList("cost-label");
            _piles = new Label();
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
            foreach (var unit in _battle.Units)
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
                tag.Extra = new Label(); tag.Extra.AddToClassList("tag-extra");
                tag.Intent = new Label(); tag.Intent.AddToClassList("tag-intent");
                tag.Root.Add(tag.Name);
                tag.Root.Add(hpBg);
                tag.Root.Add(tag.Extra);
                tag.Root.Add(tag.Intent);
                _tagLayer.Add(tag.Root);
                _tags[unit.Id] = tag;
            }
        }

        // ------------------------------------------------------------ 流程

        private void ChangeLevel(int level, bool openFormation = true)
        {
            if (level < 1 || level > DemoContent.ChapterLevelCount) return;
            _level = level;
            _seed++;
            _mapPanel?.RemoveFromHierarchy();
            _mapPanel = null;
            StartBattle();
            if (openFormation) OpenFormation();
        }

        private void StartBattle()
        {
            var setup = DemoContent.Level(_level, _seed);
            ApplyFormation(setup);
            _battle = new Battle(setup);
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
        private const string ClearedKey = "sanguo_cleared_levels";

        private readonly List<HeroDef> _rosterList = DemoContent.Roster();
        private readonly Dictionary<string, HeroDef> _roster = new Dictionary<string, HeroDef>();
        private readonly HashSet<int> _cleared = new HashSet<int>();
        private VisualElement? _formationPanel;
        private VisualElement? _mapPanel;
        private string? _formationPick;
        private string _formationMessage = "";

        private bool Blocked => _formationPanel != null || _mapPanel != null;

        private void LoadProgress()
        {
            foreach (var h in _rosterList) _roster[h.Id] = h;
            foreach (var part in PlayerPrefs.GetString(ClearedKey, "").Split(','))
                if (int.TryParse(part, out int n)) _cleared.Add(n);
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetString(ClearedKey, string.Join(",", _cleared));
            PlayerPrefs.Save();
        }

        private bool IsUnlocked(int level) => level == 1 || _cleared.Contains(level - 1);

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
                _formationPanel?.RemoveFromHierarchy();
                _formationPanel = null;
                StartBattle();
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

        public void OpenMap()
        {
            _formationPanel?.RemoveFromHierarchy();
            _formationPanel = null;
            _mapPanel?.RemoveFromHierarchy();
            _mapPanel = new VisualElement();
            _mapPanel.AddToClassList("overlay");
            _mapPanel.AddToClassList("formation-overlay");

            var title = new Label("第一章　黃巾之亂");
            title.AddToClassList("formation-title");
            _mapPanel.Add(title);
            var hint = new Label("打贏一關才會開啟下一關；點選關卡進入編隊");
            hint.AddToClassList("formation-hint");
            _mapPanel.Add(hint);

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
                bool cleared = _cleared.Contains(level);
                bool open = implemented && IsUnlocked(level);
                bool boss = level == total;
                float size = boss ? 118 : 92;

                var node = new Button(() => { if (open) ChangeLevel(level); }) { text = level.ToString() };
                node.AddToClassList("map-node");
                node.AddToClassList(cleared ? "map-node-clear" : open ? "map-node-open" : "map-node-lock");
                if (boss) node.AddToClassList("map-node-boss");
                node.style.width = size; node.style.height = size;
                node.style.borderTopLeftRadius = size / 2; node.style.borderTopRightRadius = size / 2;
                node.style.borderBottomLeftRadius = size / 2; node.style.borderBottomRightRadius = size / 2;
                node.style.left = centers[i].x - size / 2;
                node.style.top = centers[i].y - size / 2;
                field.Add(node);

                string status = cleared ? "已通關" : !implemented ? "未開放" : open ? "可挑戰" : "未解鎖";
                var caption = new Label($"{DemoContent.LevelNames[i]}\n{status}") { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("map-caption");
                caption.style.left = centers[i].x - 80;
                caption.style.top = centers[i].y + size / 2 + 4;
                field.Add(caption);
            }
            _mapPanel.Add(field);
            _root.Add(_mapPanel);
        }

        private void EndTurn()
        {
            if (_battle.Result != BattleResult.Ongoing) return;
            _battle.EndTurn();
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
            if (card != null) _battle.PlayCard(card);
            else _battle.EndTurn();
            PumpEvents();
            Refresh();
        }

        // ------------------------------------------------------------ 操作

        private void OnCardClicked(CardInstance card)
        {
            if (_battle.Result != BattleResult.Ongoing || Blocked) return;
            var result = _battle.PlayCard(card);
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
            _content.Add(toast);
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
            foreach (var unit in _battle.Units)
            {
                if (!_tags.TryGetValue(unit.Id, out var tag)) continue;
                tag.Root.style.display = unit.Alive ? DisplayStyle.Flex : DisplayStyle.None;
                if (!unit.Alive) continue;

                string sub = unit.Hero != null ? CardText.RoleName(unit.Hero.Role) : (unit.AttackType == AttackType.Ranged ? "遠程" : "近戰");
                tag.Name.text = unit.Hero != null ? $"{unit.Name} {sub}" : unit.Name;
                float ratio = unit.MaxHp <= 0 ? 0 : Mathf.Clamp01(unit.Hp / (float)unit.MaxHp);
                tag.HpFill.style.width = Length.Percent(ratio * 100f);
                tag.HpText.text = $"{unit.Hp}/{unit.MaxHp}";
                var extras = unit.Statuses.Select(s => $"{CardText.StatusName(s.Key)}{s.Value.Turns}").ToList();
                foreach (var b in unit.DefBreaks) extras.Add($"破甲{b.Percent * 100:0}%·{b.Turns}");
                if (unit.Armor > 0) extras.Insert(0, $"護甲{unit.Armor}");
                tag.Extra.text = string.Join(" ", extras);
                tag.Extra.style.display = tag.Extra.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                tag.Intent.text = unit.Side == Side.Enemy ? IntentText(unit) : "";
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

        private string IntentText(Unit enemy)
        {
            var intent = _battle.GetIntent(enemy);
            switch (intent.Type)
            {
                case Intent.Kind.Attack: return $"攻擊→{intent.Target!.Name}";
                case Intent.Kind.Move: return $"移動→第{intent.MoveTo!.Value.Lane + 1}路";
                case Intent.Kind.Stunned: return "昏亂";
                default: return "待機";
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
                var tag = new Label(card.Def.Basic ? "基礎" : "技能");
                tag.AddToClassList("card-tag");
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
                var desc = new Label(CardText.Description(card.Def));
                desc.AddToClassList("card-desc");
                var kw = new Label(CardText.Keywords(card.Def.Keywords));
                kw.AddToClassList("card-kw");

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
            _title.text = $"第 {_level} 關　第 {_battle.Turn} 回合";
            _cost.text = $"費用 {_battle.Cost}/{_battle.Setup.CostCap}";
            _piles.text = $"抽牌 {_battle.DrawPile.Count}　棄牌 {_battle.DiscardPile.Count}　破釜 {_battle.ExhaustPile.Count}";
            _logLabel.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 4)));
        }

        private void RefreshOverlay()
        {
            if (_battle.Result == BattleResult.Ongoing || _overlay != null) return;
            _overlay = new VisualElement();
            _overlay.AddToClassList("overlay");
            var text = new Label(_battle.Result == BattleResult.Won ? "勝利" : "敗北");
            text.AddToClassList("overlay-text");
            _overlay.Add(text);
            bool won = _battle.Result == BattleResult.Won;
            if (won) { _cleared.Add(_level); SaveProgress(); }
            bool hasNext = won && _level < DemoContent.ChapterLevelCount;
            if (hasNext) _overlay.Add(MakeButton("下一關", () => ChangeLevel(_level + 1), primary: true));
            _overlay.Add(MakeButton("再打一次", () => { _seed++; StartBattle(); }, primary: !hasNext));
            if (!won) _overlay.Add(MakeButton("調整編隊", () => { _seed++; StartBattle(); OpenFormation(); }));
            _overlay.Add(MakeButton("回地圖", OpenMap));
            _root.Add(_overlay);
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

        public void DebugSetLevel(int level) => ChangeLevel(level, openFormation: false);

        public void DebugPlayFirstPlayable()
        {
            var card = _battle.Hand.FirstOrDefault(c => _battle.CanPlay(c) == PlayResult.Ok);
            if (card != null) OnCardClicked(card);
        }

        public void DebugOpenFormation() => OpenFormation();
        public void DebugOpenMap() => OpenMap();

        public void DebugPreviewFirstCard()
        {
            var card = _battle.Hand.FirstOrDefault();
            if (card != null) Preview(card);
        }
    }
}
