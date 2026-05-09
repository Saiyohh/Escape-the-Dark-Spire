using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // 4-neighbour BFS on a TileType grid. Shared by:
    //  - Generator validation (reachability from Start)
    //  - Map monster Chase pathfinding
    //  - Floor Generator Window critical-path overlay
    //
    // Caller supplies a passability predicate so different consumers can treat
    // tiles differently (e.g. monsters can't path through Stairway, but the
    // generator's reachability check can).
    public static class GridBfs
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        public static HashSet<Vector2Int> Reachable(
            TileType[,] grid, Vector2Int start, Predicate<TileType> isPassable)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            var visited = new HashSet<Vector2Int>();
            if (!InBounds(start, w, h) || !isPassable(grid[start.x, start.y]))
                return visited;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (!InBounds(n, w, h)) continue;
                    if (visited.Contains(n)) continue;
                    if (!isPassable(grid[n.x, n.y])) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return visited;
        }

        // Returns the shortest path from start to goal inclusive, or null if
        // unreachable. Caller's predicate decides passability.
        public static List<Vector2Int> FindPath(
            TileType[,] grid, Vector2Int start, Vector2Int goal, Predicate<TileType> isPassable)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            if (!InBounds(start, w, h) || !InBounds(goal, w, h)) return null;
            if (!isPassable(grid[start.x, start.y]) || !isPassable(grid[goal.x, goal.y])) return null;
            if (start == goal) return new List<Vector2Int> { start };

            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == goal) return Reconstruct(cameFrom, start, goal);
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (!InBounds(n, w, h)) continue;
                    if (cameFrom.ContainsKey(n)) continue;
                    if (!isPassable(grid[n.x, n.y])) continue;
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        // Distance map: -1 for unreachable, else step count from start.
        public static int[,] DistanceField(
            TileType[,] grid, Vector2Int start, Predicate<TileType> isPassable)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            var dist = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    dist[x, y] = -1;

            if (!InBounds(start, w, h) || !isPassable(grid[start.x, start.y]))
                return dist;

            dist[start.x, start.y] = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int d0 = dist[cur.x, cur.y];
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (!InBounds(n, w, h)) continue;
                    if (dist[n.x, n.y] != -1) continue;
                    if (!isPassable(grid[n.x, n.y])) continue;
                    dist[n.x, n.y] = d0 + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        private static bool InBounds(Vector2Int p, int w, int h) =>
            p.x >= 0 && p.y >= 0 && p.x < w && p.y < h;

        private static List<Vector2Int> Reconstruct(
            Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var cur = goal;
            while (cur != start)
            {
                path.Add(cur);
                cur = cameFrom[cur];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
