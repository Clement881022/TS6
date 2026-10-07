#nullable enable
using SanGuo.Core;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 卡牌範圍圖示：一張 5 路 × 2 排的小棋盤，亮起來的格子就是會打到（或加成）的位置，取代「同路最前排敵人」之類的文字。
    /// 敵方棋盤為紅、我方為藍；中間那一路代表施放者所在的路。前排靠中線：敵方前排在下、我方前排在上。
    /// </summary>
    public static class RangeIcon
    {
        private const int Lanes = 5;
        private const int Mid = 2;

        public static VisualElement Build(CardDef def)
        {
            bool ally = def.Target == TargetRule.Self || def.Target == TargetRule.AllyLowestHp || def.Target == TargetRule.AllAllies;
            var on = Pattern(def, ally);

            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("range-icon");
            root.AddToClassList(ally ? "range-ally" : "range-enemy");
            if (def.Target == TargetRule.EnemyAny) root.AddToClassList("range-any");

            // 由上而下：敵方是 後排、前排；我方是 前排、後排。on 的第一維 0 = 後排、1 = 前排。
            int[] order = ally ? new[] { 1, 0 } : new[] { 0, 1 };
            foreach (int row in order)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("range-row");
                for (int lane = 0; lane < Lanes; lane++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("range-cell");
                    if (on[row, lane]) cell.AddToClassList("range-cell-on");
                    if (def.Target == TargetRule.Self && row == 1 && lane == Mid) cell.AddToClassList("range-cell-self");
                    line.Add(cell);
                }
                root.Add(line);
            }
            if (def.Target == TargetRule.EnemyLowestHp || def.Target == TargetRule.AllyLowestHp)
                root.Add(UiIcons.Icon("hp", "range-hp"));
            return root;
        }

        /// <summary>on[排, 路]：排 0 = 後排、1 = 前排。</summary>
        private static bool[,] Pattern(CardDef def, bool ally)
        {
            var on = new bool[2, Lanes];
            switch (def.Target)
            {
                case TargetRule.Self: on[1, Mid] = true; return on;
                case TargetRule.AllyLowestHp: on[0, 3] = true; return on;
                case TargetRule.AllAllies:
                case TargetRule.AllEnemies: Fill(on); return on;
                case TargetRule.EnemyAny: Fill(on); return on;
            }

            int row, lane = Mid;
            switch (def.Target)
            {
                case TargetRule.EnemyFront: row = 1; break;
                case TargetRule.EnemyBack: row = 0; break;
                default: row = 0; lane = 3; break; // EnemyLowestHp：示意用，實際是血量最低者
            }
            switch (def.Shape)
            {
                case Shape.Row: for (int l = 0; l < Lanes; l++) on[row, l] = true; break;
                case Shape.Column: on[0, lane] = true; on[1, lane] = true; break;
                case Shape.Cross:
                    on[row, lane] = true;
                    on[1 - row, lane] = true;
                    if (lane > 0) on[row, lane - 1] = true;
                    if (lane < Lanes - 1) on[row, lane + 1] = true;
                    break;
                case Shape.All: Fill(on); break;
                default: on[row, lane] = true; break;
            }
            return on;
        }

        private static void Fill(bool[,] on)
        {
            for (int r = 0; r < 2; r++) for (int l = 0; l < Lanes; l++) on[r, l] = true;
        }
    }
}
