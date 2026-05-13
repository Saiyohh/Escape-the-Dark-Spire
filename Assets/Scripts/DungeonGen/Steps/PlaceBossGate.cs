using UnityEngine;

namespace DarkSpire
{
    // Step 5: place the boss-room doorway.
    //
    // Layout:
    //   [Corridor tile (one out from the room)]   <- BossGate entity (blocker)
    //   [Boss-room perimeter floor tile]          <- Stairway tile in the grid
    //
    // The gate sits one tile OUTSIDE the boss room, in the corridor, so the
    // door visually reads as a door across the hallway rather than a tile
    // tucked inside the room. The stairway tile stays on the perimeter and
    // is the descent trigger once the boss is defeated.
    //
    // Player flow:
    //   1. Walk down corridor, hit the gate.   Locked — need N keys.
    //   2. Get keys, press E adjacent.         Gate unlocks (entity stops blocking).
    //   3. Walk through the corridor tile onto the stairway perimeter tile.
    //      Pre-boss-defeat: "Defeat the boss first" message; party still
    //      stands on the doorway.
    //   4. Walk to boss, fight, win.           bossDefeated set in RunStateHolder.
    //   5. Resume from combat.                 Gate entity is skipped on respawn
    //      (EntitySpawner filters BossGate when bossDefeated). Stairway tile is
    //      naked — walking back onto it descends.
    //
    // The gate's strPayload carries the cardinal direction from gate → room
    // ("N"/"S"/"E"/"W") so the renderer can pick a direction-facing sprite.
    // intPayload carries keysRequired.
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

            // bossGatePos = where the gate ENTITY lives (and what gates passage).
            // stairwayPos = where the descend-trigger tile is (the room doorway).
            ctx.bossGatePos = bestCorridor;
            ctx.stairwayPos = bestPerimeter.Value;

            ctx.tiles[ctx.stairwayPos.x, ctx.stairwayPos.y] = TileType.Stairway;

            // Direction from gate → room (so a north-facing sprite is drawn
            // when the room is north of the gate). Perimeter–to–corridor is
            // the inverse; negate to get gate → room.
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

            // Pick the perimeter–corridor pair closest to start by Manhattan
            // distance from the corridor tile, so the gate sits squarely on
            // the natural approach.
            foreach (var d in Dirs4)
            {
                var n = p + d;
                if (!ctx.InBounds(n)) continue;
                if (bossBounds.Contains(n)) continue;
                if (!ctx.tiles[n.x, n.y].IsWalkable()) continue;
                // Don't put the gate inside another room — would look like a
                // door floating in a treasure room. Prefer true corridor tiles.
                if (ctx.RoomAt(n).HasValue) continue;
                // Don't drop the gate on top of a corridor monster's spawn
                // (they're placed in PlaceEntities, which runs first).
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
