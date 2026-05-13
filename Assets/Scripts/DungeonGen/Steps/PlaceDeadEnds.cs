using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    internal static class PlaceDeadEnds
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        public static void Apply(GenContext ctx)
        {
            ctx.deadEndTerminals = FindNaturalDeadEnds(ctx);

            int extras = ctx.rng.NextRangeInclusive(
                ctx.config.minDeadEndBranches, ctx.config.maxDeadEndBranches);

            for (int i = 0; i < extras; i++)
            {
                if (TryCarveBranch(ctx, out var terminal))
                    ctx.deadEndTerminals.Add(terminal);
            }

            ctx.stats.deadEndCount = ctx.deadEndTerminals.Count;
        }

        private static List<Vector2Int> FindNaturalDeadEnds(GenContext ctx)
        {
            var result = new List<Vector2Int>();
            int w = ctx.config.gridSize.x, h = ctx.config.gridSize.y;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (ctx.tiles[x, y] != TileType.Floor) continue;
                    if (ctx.RoomAt(p).HasValue) continue;
                    if (CountFloorNeighbours(ctx, p) == 1)
                        result.Add(p);
                }
            }
            return result;
        }

        private static int CountFloorNeighbours(GenContext ctx, Vector2Int p)
        {
            int n = 0;
            foreach (var d in Dirs)
            {
                var q = p + d;
                if (ctx.InBounds(q) && ctx.tiles[q.x, q.y] != TileType.Wall) n++;
            }
            return n;
        }

        private static bool TryCarveBranch(GenContext ctx, out Vector2Int terminal)
        {
            terminal = default;
            int len = ctx.rng.NextRangeInclusive(
                ctx.config.deadEndLengthRange.x, ctx.config.deadEndLengthRange.y);

            var corridorTiles = new List<Vector2Int>();
            int w = ctx.config.gridSize.x, h = ctx.config.gridSize.y;
            for (int x = 1; x < w - 1; x++)
                for (int y = 1; y < h - 1; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (ctx.tiles[x, y] != TileType.Floor) continue;
                    if (ctx.RoomAt(p).HasValue) continue;
                    corridorTiles.Add(p);
                }

            if (corridorTiles.Count == 0) return false;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                var root = ctx.Pick(corridorTiles);
                var order = new List<int> { 0, 1, 2, 3 };
                ctx.rng.Shuffle(order);

                foreach (var i in order)
                {
                    var d = Dirs[i];
                    if (TryCarveStraight(ctx, root, d, len, out terminal))
                        return true;
                }
            }
            return false;
        }

        private static bool TryCarveStraight(
            GenContext ctx, Vector2Int root, Vector2Int dir, int len, out Vector2Int terminal)
        {
            terminal = default;
            for (int step = 1; step <= len; step++)
            {
                var p = root + dir * step;
                if (!ctx.InBounds(p)) return false;
                if (ctx.tiles[p.x, p.y] != TileType.Wall) return false;
                if (ctx.RoomAt(p).HasValue) return false;
            }

            for (int step = 1; step <= len; step++)
            {
                var p = root + dir * step;
                ctx.tiles[p.x, p.y] = TileType.Floor;
                terminal = p;
            }
            return true;
        }
    }

    internal static class GenContextExtensions
    {
        public static T Pick<T>(this GenContext ctx, System.Collections.Generic.IList<T> list)
        {
            return list[ctx.rng.NextInt(list.Count)];
        }
    }
}
