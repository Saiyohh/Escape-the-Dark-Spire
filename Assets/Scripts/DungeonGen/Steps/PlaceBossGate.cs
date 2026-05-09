using UnityEngine;

namespace DarkSpire
{
    // Step 5: place the boss-room doorway. The gate AND the stairway are the
    // SAME tile — the boss room's only entrance is also the way out (down to
    // the next floor). Visual layering:
    //
    //   [Stairway tile]   <- written into the grid; visible once the gate is gone
    //   [BossGate entity] <- rendered on top; blocks passage while locked
    //
    // Player flow:
    //   1. Walk to gate.   Locked — need N keys.
    //   2. Get keys, press E adjacent.   Gate unlocks (entity stops blocking).
    //   3. Walk onto the doorway tile.   "Defeat the boss first" (Stairway +
    //      !bossDefeated). Party now stands inside the boss room.
    //   4. Walk to boss, fight, win.   bossDefeated is set in RunStateHolder.
    //   5. Resume from combat.   The gate entity is skipped on respawn so the
    //      stairway tile is naked — walking back onto it descends.
    //
    // The boss enemy itself is placed elsewhere (PlaceEntities, room center);
    // this step only owns the entrance/exit tile.
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

            // Find the perimeter floor tile adjacent to a corridor — that's
            // the carved doorway. If multiple candidates exist, pick the one
            // closest to the start so the gate sits on the natural approach.
            var b = bossRoom.Value.bounds;
            Vector2Int? bestGate = null;
            int bestDist = int.MaxValue;

            for (int x = b.xMin; x < b.xMax; x++)
            {
                Consider(ctx, new Vector2Int(x, b.yMin), b, ref bestGate, ref bestDist);
                Consider(ctx, new Vector2Int(x, b.yMax - 1), b, ref bestGate, ref bestDist);
            }
            for (int y = b.yMin; y < b.yMax; y++)
            {
                Consider(ctx, new Vector2Int(b.xMin, y), b, ref bestGate, ref bestDist);
                Consider(ctx, new Vector2Int(b.xMax - 1, y), b, ref bestGate, ref bestDist);
            }

            if (!bestGate.HasValue) return;

            ctx.bossGatePos = bestGate.Value;
            ctx.stairwayPos = bestGate.Value;     // same tile

            // Stamp the doorway as a Stairway tile underneath the gate entity.
            // While the gate exists it visually covers the stairs; once the
            // entity is gone (post-boss-defeat resume) the stairs are visible
            // and the existing TileType.Stairway walkover handler triggers
            // descent.
            ctx.tiles[ctx.bossGatePos.x, ctx.bossGatePos.y] = TileType.Stairway;

            // intPayload carries keysRequired so BossGateEntity can show the
            // right counter without re-reading the config asset.
            ctx.entities.Add(new EntityPlacement(
                ctx.bossGatePos, EntityKind.BossGate, ctx.config.keysRequired));
        }

        private static void Consider(
            GenContext ctx, Vector2Int p, RectInt bossBounds,
            ref Vector2Int? best, ref int bestDist)
        {
            // Must be on the boss room's perimeter floor.
            if (ctx.tiles[p.x, p.y] != TileType.Floor) return;
            // Must have at least one walkable corridor neighbour OUTSIDE the
            // room (Wall and Empty both fail this check, walking tiles pass).
            bool hasCorridorNeighbour = false;
            foreach (var d in Dirs4)
            {
                var n = p + d;
                if (!ctx.InBounds(n)) continue;
                if (bossBounds.Contains(n)) continue;
                if (ctx.tiles[n.x, n.y].IsWalkable()) { hasCorridorNeighbour = true; break; }
            }
            if (!hasCorridorNeighbour) return;

            int dist = Mathf.Abs(p.x - ctx.startPos.x) + Mathf.Abs(p.y - ctx.startPos.y);
            if (dist < bestDist) { best = p; bestDist = dist; }
        }
    }
}
