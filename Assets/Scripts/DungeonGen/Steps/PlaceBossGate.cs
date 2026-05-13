using UnityEngine;

namespace DarkSpire
{
    internal static class PlaceBossGate
    {
        private static readonly Vector2Int[] Dirs4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        public static void Apply(GenContext ctx)
        {
            var bossRoom = ctx.FirstRoomOfKind(RoomKind.Boss);
            if (bossRoom == null) return;

            var b = bossRoom.Value.bounds;
            Vector2Int? bestPerimeter = null;
            Vector2Int bestCorridor = default;
            Vector2Int bestPerimToCorridor = default;
            int bestDist = int.MaxValue;

            for (int x = b.xMin; x < b.xMax; x++)
            {
                Consider(ctx, new Vector2Int(x, b.yMin), b,
                    ref bestPerimeter, ref bestCorridor, ref bestPerimToCorridor, ref bestDist);
                Consider(ctx, new Vector2Int(x, b.yMax - 1), b,
                    ref bestPerimeter, ref bestCorridor, ref bestPerimToCorridor, ref bestDist);
            }
            for (int y = b.yMin; y < b.yMax; y++)
            {
                Consider(ctx, new Vector2Int(b.xMin, y), b,
                    ref bestPerimeter, ref bestCorridor, ref bestPerimToCorridor, ref bestDist);
                Consider(ctx, new Vector2Int(b.xMax - 1, y), b,
                    ref bestPerimeter, ref bestCorridor, ref bestPerimToCorridor, ref bestDist);
            }

            if (!bestPerimeter.HasValue) return;

            ctx.bossGatePos = bestCorridor;
            ctx.stairwayPos = bestPerimeter.Value;

            ctx.tiles[ctx.stairwayPos.x, ctx.stairwayPos.y] = TileType.Stairway;

            string facing = FacingCode(-bestPerimToCorridor);

            ctx.entities.Add(new EntityPlacement(
                ctx.bossGatePos, EntityKind.BossGate,
                ctx.config.keysRequired, facing));
        }

        private static void Consider(
            GenContext ctx, Vector2Int p, RectInt bossBounds,
            ref Vector2Int? bestPerim, ref Vector2Int bestCorridor,
            ref Vector2Int bestPerimToCorridor, ref int bestDist)
        {
            if (ctx.tiles[p.x, p.y] != TileType.Floor) return;

            foreach (var d in Dirs4)
            {
                var n = p + d;
                if (!ctx.InBounds(n)) continue;
                if (bossBounds.Contains(n)) continue;
                if (!ctx.tiles[n.x, n.y].IsWalkable()) continue;
                if (ctx.RoomAt(n).HasValue) continue;
                if (HasMonsterAt(ctx, n)) continue;

                int dist = Mathf.Abs(n.x - ctx.startPos.x) + Mathf.Abs(n.y - ctx.startPos.y);
                if (dist < bestDist)
                {
                    bestPerim = p;
                    bestCorridor = n;
                    bestPerimToCorridor = d;
                    bestDist = dist;
                }
            }
        }

        private static bool HasMonsterAt(GenContext ctx, Vector2Int p)
        {
            foreach (var m in ctx.monsters)
                if (m.start == p) return true;
            return false;
        }

        private static string FacingCode(Vector2Int gateToRoom)
        {
            if (gateToRoom.y > 0) return "N";
            if (gateToRoom.y < 0) return "S";
            if (gateToRoom.x > 0) return "E";
            if (gateToRoom.x < 0) return "W";
            return "N";
        }
    }
}
