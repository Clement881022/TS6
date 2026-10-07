#nullable enable
using System.Linq;
using SanGuo.Core;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    /// <summary>戰前編隊：5x2 站位與上場武將（最多 4 人），按「開戰」才向後端開始關卡並扣體力。</summary>
    public sealed class FormationPage : PageBase
    {
        private string? _pick;
        private string _message = "";

        protected override Page Id => Page.Formation;
        protected override string Title => $"排兵布陣　第 {GameSession.SelectedLevel} 關　{DemoContent.LevelNames[GameSession.SelectedLevel - 1]}";
        protected override bool ShowNav => false;

        private static string HeroLabel(HeroDef h) => $"{h.Name}　{h.Rarity} {CardText.RoleName(h.Role)}";

        private static string? HeroAtCell(int lane, int row)
        {
            foreach (var kv in GameSession.Formation)
                if (kv.Value.Lane == lane && kv.Value.Row == row) return kv.Key;
            return null;
        }

        protected override void BuildBody(VisualElement body)
        {
            int levelNo = GameSession.SelectedLevel;
            var level = DemoContent.Level(levelNo, 1);
            // 第一次進來先把預設隊伍填好。
            if (GameSession.Formation.Count == 0) GameSession.ApplyFormation(level);
            var formation = GameSession.Formation;

            var foes = level.Enemies.GroupBy(e => e.Def.Name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key);
            var enemyPanel = UiKit.Panel();
            enemyPanel.Add(UiKit.Text("敵方：" + string.Join("、", foes), "txt-gold"));
            body.Add(enemyPanel);
            body.Add(_message.Length > 0
                ? UiKit.Hint(_message, warn: true)
                : UiKit.Hint("同路沒有對手時，攻擊會落在最上方（第 1 路）；點武將再點格子可移動 / 換位，最多上場 4 人"));

            var board = UiKit.Panel("formation-board");
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            head.Add(FormLabel("", 120));
            head.Add(FormLabel("後排", 260));
            head.Add(FormLabel("前排", 260));
            board.Add(head);
            for (int lane = 0; lane < level.Lanes; lane++)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(FormLabel($"第 {lane + 1} 路", 120));
                for (int r = level.Rows - 1; r >= 0; r--) // 後排在左、前排在右（前排朝向敵人）
                {
                    int l = lane, rr = r;
                    string? id = HeroAtCell(lane, r);
                    var cell = new Button(() => OnCell(l, rr)) { text = id == null ? "—" : HeroLabel(GameSession.DefOf(id)!) };
                    cell.AddToClassList("formation-cell");
                    if (id == null) cell.AddToClassList("formation-cell-empty");
                    if (id != null && id == _pick) cell.AddToClassList("btn-on");
                    row.Add(cell);
                }
                board.Add(row);
            }
            body.Add(board);

            body.Add(UiKit.Section("待命武將"));
            body.Add(UiKit.Text("點選後再點上方格子上場；先點場上武將再點這裡可下場", "txt-dim"));
            var bench = UiKit.Row("row-center");
            foreach (var def in GameSession.Roster.Where(d => !formation.ContainsKey(d.Id)))
            {
                string id = def.Id;
                var chip = new Button(() => OnBench(id)) { text = HeroLabel(def) };
                chip.AddToClassList("formation-chip");
                if (id == _pick) chip.AddToClassList("btn-on");
                bench.Add(chip);
            }
            body.Add(bench);

            var buttons = UiKit.Row("row-center");
            buttons.Add(UiKit.Btn("回地圖", () => Nav.Go(Page.Map)).WithClass("btn-wide"));
            buttons.Add(UiKit.Btn("開戰", () =>
            {
                if (formation.Count == 0) { _message = "至少要有 1 名武將上場"; Rebuild(); return; }
                _ = StartBattle(GameSession.StageIdOf(levelNo));
            }, primary: true).WithClass("btn-wide"));
            body.Add(buttons);
        }

        private static Label FormLabel(string text, float width)
        {
            var l = new Label(text);
            l.AddToClassList("formation-label");
            l.style.width = width;
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
