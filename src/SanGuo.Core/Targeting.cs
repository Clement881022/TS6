using System.Collections.Generic;

namespace SanGuo.Core
{
    public static class Targeting
    {
        public static List<Position> ExpandShape(Position center, Shape shape, int lanes, int rows)
        {
            var cells = new List<Position>();
            switch (shape)
            {
                case Shape.Single:
                    cells.Add(center);
                    break;
                case Shape.Row3:
                    cells.Add(center);
                    AddIfInBounds(cells, center.Lane - 1, center.Row, lanes, rows);
                    AddIfInBounds(cells, center.Lane + 1, center.Row, lanes, rows);
                    break;
                case Shape.Column3:
                    cells.Add(center);
                    AddIfInBounds(cells, center.Lane, center.Row - 1, lanes, rows);
                    AddIfInBounds(cells, center.Lane, center.Row + 1, lanes, rows);
                    break;
                case Shape.Cross:
                    cells.Add(center);
                    AddIfInBounds(cells, center.Lane - 1, center.Row, lanes, rows);
                    AddIfInBounds(cells, center.Lane + 1, center.Row, lanes, rows);
                    AddIfInBounds(cells, center.Lane, center.Row - 1, lanes, rows);
                    AddIfInBounds(cells, center.Lane, center.Row + 1, lanes, rows);
                    break;
                case Shape.All:
                    for (int l = 0; l < lanes; l++)
                        for (int r = 0; r < rows; r++)
                            cells.Add(new Position(l, r));
                    break;
            }
            return cells;
        }

        private static void AddIfInBounds(List<Position> cells, int lane, int row, int lanes, int rows)
        {
            if (lane >= 0 && lane < lanes && row >= 0 && row < rows)
                cells.Add(new Position(lane, row));
        }
    }
}
