using UnityEngine;

namespace DarkSpire
{
    public static class LineOfSight
    {
        public static bool HasLoS(TileType[,] grid, Vector2Int from, Vector2Int to)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            int x0 = from.x, y0 = from.y;
            int x1 = to.x,   y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int x = x0, y = y0;
            while (true)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return false;
                if ((x != x0 || y != y0) && (x != x1 || y != y1))
                {
                    if (grid[x, y].IsImpassable()) return false;
                }
                if (x == x1 && y == y1) return true;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 <  dx) { err += dx; y += sy; }
            }
        }
    }
}
