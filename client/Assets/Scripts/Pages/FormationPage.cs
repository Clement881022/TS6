#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    /// <summary>
    /// 戰前編隊：3x2 站位（共用 5x5 戰場的我方下兩排中央）與上場武將（最多 4 人，只能帶已擁有的武將，戰鬥會套用他們的等級、突破與卡牌強化）。
    /// 教學關之後的主線關卡與資源副本才會來這裡；按「開戰」才向後端開始並扣體力。
    /// </summary>
    public sealed class FormationPage : PageBase
    {
        private string? _pick;
        private string _message = "";

        protected override Page Id => Page.Formation;
        private static string StageId =>
            GameSession.FormationStageId != "" ? GameSession.FormationStageId : GameSession.StageIdOf(GameSession.SelectedLevel);

        protected override string Title
        {
            get
            {
                var dungeon = DemoMeta.FindDungeon(StageId);
                if (dungeon != null) return $"排兵布陣　{dungeon.Name}";
                int level = DemoMeta.LevelOf(StageId);
                return level == 0 ? "排兵布陣" : $"排兵布陣　第 {level} 關　{DemoContent.LevelNames[level - 1]}";
            }
        }
        protected override Page BackPage => DemoMeta.FindDungeon(StageId) != null ? Page.Dungeons : Page.Map;

        /// <summary>武將卡下方的小字：等級與職業。</summary>
        private static string HeroSub(HeroDef h) =>
            $"Lv.{(GameSession.View.Heroes.TryGetValue(h.Id, out var st) ? st.Level : 1)} {CardText.RoleName(h.Role)}";

        private void Back() => Nav.Go(DemoMeta.FindDungeon(StageId) != null ? Page.Dungeons : Page.Map);

        private static string? HeroAtCell(int lane, int row)
        {
            foreach (var kv in GameSession.Formation)
                if (kv.Value.Lane == lane && kv.Value.Row == row) return kv.Key;
            return null;
        }

        protected override void BuildBody(VisualElement body)
        {
            string stageId = StageId;
            var level = GameSession.EnemyPreview(stageId);
            if (level == null)
            {
                body.Add(UiKit.Hint("找不到這個關卡", warn: true));
                body.Add(UiKit.Btn("返回", Back, primary: true).WithClass("btn-wide"));
                return;
            }
            if (GameSession.OwnedHeroes().Count == 0)
            {
                body.Add(UiKit.Hint("尚未擁有武將，先去招募吧", warn: true));
                var go = UiKit.Row("row-center");
                go.Add(UiKit.Btn("返回", Back).WithClass("btn-wide"));
                go.Add(UiKit.Btn("前往招募", () => Nav.Go(Page.Gacha), primary: true).WithClass("btn-wide"));
                body.Add(go);
                return;
            }
            // 第一次進來（或編隊裡有沒擁有的武將）先自動排好一隊。
            GameSession.EnsureFormation();
            var formation = GameSession.Formation;

            body.style.flexDirection = FlexDirection.Column;

            // ---- 上：敵情與提示 ----
            var foes = level.Enemies.GroupBy(e => e.Def.Name).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);
            var strip = new VisualElement();
            strip.AddToClassList("form-strip");
            strip.Add(UiKit.Text("敵方", "form-strip-label"));
            strip.Add(UiKit.Text(string.Join("　", foes), "txt-gold"));
            body.Add(strip);
            var hint = UiKit.Hint(_message.Length > 0 ? _message : "點武將再點格子可移動 / 換位，最多上場 4 人；戰場是敵我共用的 5x5，開戰後靠「移動」卡走位", _message.Length > 0);
            hint.AddToClassList("form-hint");
            body.Add(hint);

            var main = new VisualElement();
            main.AddToClassList("form-main");
            body.Add(main);

            // ---- 左：站位（前排在上，朝向敵人）----
            var board = new VisualElement();
            board.AddToClassList("bpanel");
            board.AddToClassList("form-board");
            board.Add(UiKit.Text("站位", "bpanel-title"));
            var laneHead = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            laneHead.Add(FormLabel("", 80));
            string[] laneNames = { "左", "中", "右" };
            for (int lane = BattleSetup.FormationMinLane; lane <= BattleSetup.FormationMaxLane; lane++)
                laneHead.Add(FormLabel(laneNames[lane - BattleSetup.FormationMinLane], 150, 5));
            board.Add(laneHead);
            for (int r = BattleSetup.FormationMinRow; r <= BattleSetup.FormationMaxRow; r++)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                row.Add(FormLabel(r == BattleSetup.FormationMinRow ? "前排" : "後排", 80));
                for (int lane = BattleSetup.FormationMinLane; lane <= BattleSetup.FormationMaxLane; lane++)
                {
                    int l = lane, rr = r;
                    string? id = HeroAtCell(lane, r);
                    var def = id == null ? null : GameSession.DefOf(id);
                    if (def == null)
                    {
                        var slot = new Button(() => OnCell(l, rr));
                        slot.AddToClassList("slot-empty");
                        slot.Add(new Label("＋") { pickingMode = PickingMode.Ignore }.WithClass("slot-plus"));
                        row.Add(slot);
                    }
                    else
                    {
                        int lv = GameSession.View.Heroes.TryGetValue(def.Id, out var st) ? st.Level : 1;
                        row.Add(UiKit.HeroTile(def, lv, -1, () => OnCell(l, rr), selected: id == _pick, extraClass: "htile-sm"));
                    }
                }
                board.Add(row);
            }
            main.Add(board);

            // ---- 右：待命武將 ----
            var bench = new VisualElement();
            bench.AddToClassList("bpanel");
            bench.AddToClassList("form-bench");
            bench.Add(UiKit.Text($"待命武將（上場 {formation.Count}/{GameSession.MaxTeamSize}）", "bpanel-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.contentContainer.AddToClassList("hero-grid");
            foreach (var def in GameSession.OwnedHeroes().Where(d => !formation.ContainsKey(d.Id)))
            {
                string id = def.Id;
                int lv = GameSession.View.Heroes.TryGetValue(id, out var st) ? st.Level : 1;
                scroll.Add(UiKit.HeroTile(def, lv, -1, () => OnBench(id), selected: id == _pick, extraClass: "htile-sm"));
            }
            bench.Add(scroll);
            main.Add(bench);

            var buttons = new VisualElement();
            buttons.AddToClassList("form-buttons");
            buttons.Add(UiKit.Btn("返回", Back).WithClass("btn-wide"));
            buttons.Add(UiKit.Btn("戰鬥", () =>
            {
                if (formation.Count == 0) { _message = "至少要有 1 名武將上場"; Rebuild(); return; }
                _ = StartBattle(stageId);
            }, primary: true).WithClass("btn-wide"));
            body.Add(buttons);
        }

        private static Label FormLabel(string text, float width, float margin = 0)
        {
            var l = new Label(text);
            l.AddToClassList("formation-label");
            l.style.width = width;
            l.style.marginLeft = margin; l.style.marginRight = margin;
            return l;
        }

        private void OnCell(int lane, int row)
        {
            var formation = GameSession.Formation;
            _message = "";
            string? occupant = HeroAtCell(lane, row);
            if (_pick == null)
            {
                if (occupant != null) _pick = occupant;
            }
            else
            {
                bool pickOnBoard = formation.ContainsKey(_pick);
                if (occupant == _pick)
                {
                    // 再點一次 = 取消選取
                }
                else if (pickOnBoard)
                {
                    var from = formation[_pick];
                    if (occupant != null) formation[occupant] = from; // 落在隊友格 → 換位
                    formation[_pick] = new Position(lane, row);
                }
                else
                {
                    if (occupant != null) formation.Remove(occupant); // 替換：原本的人下場
                    else if (formation.Count >= GameSession.MaxTeamSize)
                    {
                        _message = $"場上最多 {GameSession.MaxTeamSize} 人，請先讓一名武將下場";
                        Rebuild();
                        return;
                    }
                    formation[_pick] = new Position(lane, row);
                }
                _pick = null;
            }
            Rebuild();
        }

        private void OnBench(string id)
        {
            var formation = GameSession.Formation;
            _message = "";
            if (_pick == null) _pick = id;
            else if (formation.ContainsKey(_pick))
            {
                if (formation.Count > 1) formation.Remove(_pick);
                else _message = "至少要有 1 名武將上場";
                _pick = null;
            }
            else _pick = id == _pick ? null : id;
            Rebuild();
        }
    }
}
