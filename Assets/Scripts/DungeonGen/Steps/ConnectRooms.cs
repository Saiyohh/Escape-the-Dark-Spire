using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Step 2: connect rooms with corridors. Build a Prim's MST on room
    // centers using Manhattan distance, then add N extra edges for loops,
    // and carve L-shaped 1-tile-wide corridors for each edge.
    internal static class ConnectRooms
    {
        public static void Apply(GenContext ctx)
        {
            int n = ctx.rooms.Count;
            if (n < 2) return;

            var mst = ComputeMST(ctx);
            CarveAll(ctx, mst);

            // Extra edges: pick from non-MST edges at random.
            var allEdges = AllEdges(n);
            var mstSet = new HashSet<(int, int)>();
            foreach (var (a, b) in mst) mstSet.Add(NormalizeEdge(a, b));

            var candidates = new List<(int, int)>();
            foreach (var (a, b) in allEdges)
                if (!mstSet.Contains(NormalizeEdge(a, b))) candidates.Add((a, b));

            ctx.rng.Shuffle(candidates);
            int extras = Mathf.Min(ctx.config.extraConnections, candidates.Count);
            for (int i = 0; i < extras; i++)
                CarveCorridor(ctx, ctx.rooms[candidates[i].Item1].Center, ctx.rooms[candidates[i].Item2].Center);
        }

        private static (int, int) NormalizeEdge(int a, int b) => a < b ? (a, b) : (b, a);

        private static List<(int, int)> AllEdges(int n)
        {
            var edges = new List<(int, int)>();
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    edges.Add((a, b));
            return edges;
        }

        private static List<(int, int)> ComputeMST(GenContext ctx)
        {
            int n = ctx.rooms.Count;
            var inTree = new bool[n];
            var minEdge = new (int from, int dist)[n];
            for (int i = 0; i < n; i++) minEdge[i] = (-1, int.MaxValue);

            inTree[0] = true;
            for (int i = 1; i < n; i++)
                minEdge[i] = (0, Manhattan(ctx.rooms[0].Center, ctx.rooms[i].Center));

            var result = new List<(int, int)>();
            for (int step = 0; step < n - 1; step++)
            {
                int next = -1;
                int bestDist = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (inTree[i]) continue;
                    if (minEdge[i].dist < bestDist)
                    {
                        bestDist = minEdge[i].dist;
                        next = i;
                    }
                }
                if (next < 0) break;
                inTree[next] = true;
                result.Add((minEdge[next].from, next));

                for (int i = 0; i < n; i++)
                {
                    if (inTree[i]) continue;
                    int d = Manhattan(ctx.rooms[next].Center, ctx.rooms[i].Center);
                    if (d < minEdge[i].dist) minEdge[i] = (next, d);
                }
            }
            return result;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static void CarveAll(GenContext ctx, List<(int, int)> edges)
        {
            foreach (var (a, b) in edges)
                CarveCorridor(ctx, ctx.rooms[a].Center, ctx.rooms[b].Center);
        }

        // L-shaped corridor: pick horizontal-first or vertical-first at random.
        private static void CarveCorridor(GenContext ctx, Vector2Int from, Vector2Int to)
        {
            bool horizontalFirst = ctx.rng.NextInt(2) == 0;
            if (horizontalFirst)
            {
                CarveHorizontal(ctx, from.x, to.x, from.y);
                CarveVertical(ctx, from.y, to.y, to.x);
            }
            else
            {
                CarveVertical(ctx, from.y, to.y, from.x);
                CarveHorizontal(ctx, from.x, to.x, to.y);
            }
        }

        private static void CarveHorizontal(GenContext ctx, int x0, int x1, int y)
        {
            int from = Mathf.Min(x0, x1);
            int to = Mathf.Max(x0, x1);
            for (int x = from; x <= to; x++)
            {
                if (!ctx.InBounds(new Vector2Int(x, y))) continue;
                if (ctx.tiles[x, y] == TileType.Wall) ctx.tiles[x, y] = TileType.Floor;
            }
        }

        private static void CarveVertical(GenContext ctx, int y0, int y1, int x)
        {
            int from = Mathf.Min(y0, y1);
            int to = Mathf.Max(y0, y1);
            for (int y = from; y <= to; y++)
            {
                if (!ctx.InBounds(new Vector2Int(x, y))) continue;
                if (ctx.tiles[x, y] == TileType.Wall) ctx.tiles[x, y] = TileType.Floor;
            }
        }
    }
}
