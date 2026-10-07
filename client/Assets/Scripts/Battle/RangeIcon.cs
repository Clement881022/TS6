#nullable enable
using SanGuo.Core;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 卡牌範圍圖示：5x5 小棋盤，正中央是施放者；亮起來的格子是「射程」（曼哈頓格距）內可選 / 會影響的位置。
    /// 敵方目標為紅、我方為藍、自身為黃、移動為綠；下方文字標出射程與形狀。
    /// </summary>
    public static class RangeIcon
    {
        private const int Size = 5;
        private const int Mid = 2;

        /// <param name="moveRange">移動卡的格數（持有者移動力）；其他牌忽略。</param>
        public static VisualElement Build(CardDef def, int moveRange = 0)
        {
            bool ally = def.Target == TargetRule.Self || def.Target == TargetRule.AllyLowestHp || def.Target == TargetRule.AllAllies;
            bool move = def.Target == TargetRule.MoveDest;
            int range = move ? moveRange : def.Range;
            bool all = def.Target == TargetRule.AllAllies || def.Target == TargetRule.AllEnemies;

            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("range-icon");
            root.AddToClassList(move ? "range-move" : ally ? "range-ally" : "range-enemy");

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("range-grid");
            root.Add(grid);
            for (int y = 0; y < Size; y++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("range-row");
                for (int x = 0; x < Size; x++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("range-cell");
                    int dist = System.Math.Abs(x - Mid) + System.Math.Abs(y - Mid);
                    bool self = x == Mid && y == Mid;
                    if (def.Target == TargetRule.Self) { if (self) cell.AddToClassList("range-cell-self"); }
                    else if (self) cell.AddToClassList("range-cell-self");
                    else if (all || dist <= range) cell.AddToClassList("range-cell-on");
                    line.Add(cell);
                }
                grid.Add(line);
            }

            var label = new Label(Caption(def, range)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("range-text");
            root.Add(label);
            if (def.Target == TargetRule.EnemyLowestHp || def.Target == TargetRule.AllyLowestHp)
                root.Add(UiIcons.Icon("hp", "range-hp"));
            return root;
        }

        public static string Caption(CardDef def, int range)
        {
            switch (def.Target)
            {
                case TargetRule.Self: return "自身";
                case TargetRule.AllAllies: return "全體友軍";
                case TargetRule.AllEnemies: return "全體敵人";
                case TargetRule.MoveDest: return $"移動 {range} 格";
            }
            string shape;
            switch (def.Shape)
            {
                case Shape.Row: shape = "·整排"; break;
                case Shape.Column: shape = "·整欄"; break;
                case Shape.Cross: shape = "·十字"; break;
                default: shape = ""; break;
            }
            return $"射程 {range}{shape}";
        }
    }
}
