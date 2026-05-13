using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // playable; false signals the top-level driver to discard this attempt
    // and regenerate with a different seed offset.
    internal static class ValidateFloor
    {
        private static readonly Vector2Int[] Dirs4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        public static bool Apply(GenContext ctx)
        {
            // Reachability from Start over all non-wall tiles.
            var reachable = GridBfs.Reachable(
                ctx.tiles, ctx.startPos, t => t.IsWalkable());

            // Every room center (Key/Rest/Shrine/Boss/Loot) must be reachable.
            foreach (var room in ctx.rooms)
            {
                if (!reachable.Contains(room.Center))
                {
                    Debug.LogWarning($"[Validate] {room.kind} at {room.Center} unreachable from Start");
                    return false;
                }
            }

            // Boss Gate must exist and be reachable.
            if (ctx.bossGatePos == default)
            {
                Debug.LogWarning("[Validate] Boss Gate was not placed");
                return false;
            }
            if (!reachable.Contains(ctx.bossGatePos))
            {
                Debug.LogWarning($"[Validate] Boss Gate at {ctx.bossGatePos} unreachable from Start");
                return false;
            }

            // Key count satisfied.
            int keys = 0;
            foreach (var e in ctx.entities) if (e.kind == EntityKind.Key) keys++;
            if (keys < ctx.config.keysRequired)
            {
                Debug.LogWarning($"[Validate] Only {keys} keys placed; required {ctx.config.keysRequired}");
                return false;
            }

            // Boss room must have exactly one walkable corridor entrance —
            // otherwise the gate isn't actually a chokepoint and players can
            // walk into the boss room without keys.
            var bossRoom = ctx.FirstRoomOfKind(RoomKind.Boss);
            if (bossRoom.HasValue)
            {
                if (CountBossRoomEntrances(ctx, bossRoom.Value) != 1)
                {
                    Debug.LogWarning("[Validate] Boss room has more than one corridor entrance — gate is bypassable");
                    return false;
                }

                // Every key must be reachable from start WITHOUT walking through
                // the boss room interior. Otherwise the player would need to
                // unlock the boss gate (which itself requires those keys) just
                // to reach the keys — deadlock.
                var preGateReach = ReachableExcludingRoom(ctx, ctx.startPos, bossRoom.Value.bounds);
                foreach (var e in ctx.entities)
                {
                    if (e.kind != EntityKind.Key) continue;
                    if (!preGateReach.Contains(e.position))
                    {
                        Debug.LogWarning($"[Validate] Key at {e.position} only reachable through Boss Room — would deadlock");
                        return false;
                    }
                }
            }

            return true;
        }

        private static int CountBossRoomEntrances(GenContext ctx, RoomPlacement bossRoom)
        {
            // Count perimeter floor tiles that have a walkable neighbour
            // outside the room. Each one is a corridor mouth.
            var b = bossRoom.bounds;
            int count = 0;
            for (int x = b.xMin; x < b.xMax; x++)
            {
                if (IsEntrance(ctx, new Vector2Int(x, b.yMin), b)) count++;
                if (IsEntrance(ctx, new Vector2Int(x, b.yMax - 1), b)) count++;
            }
            for (int y = b.yMin + 1; y < b.yMax - 1; y++)
            {
                if (IsEntrance(ctx, new Vector2Int(b.xMin, y), b)) count++;
                if (IsEntrance(ctx, new Vector2Int(b.xMax - 1, y), b)) count++;
            }
            return count;
        }

        private static bool IsEntrance(GenContext ctx, Vector2Int p, RectInt bossBounds)
        {
            if (!ctx.tiles[p.x, p.y].IsWalkable()) return false;
            foreach (var d in Dirs4)
            {
                var n = p + d;
                if (!ctx.InBounds(n)) continue;
                if (bossBounds.Contains(n)) continue;
                if (ctx.tiles[n.x, n.y].IsWalkable()) return true;
            }
            return false;
        }

        private static HashSet<Vector2Int> ReachableExcludingRoom(
            GenContext ctx, Vector2Int start, RectInt excluded)
        {
            int w = ctx.config.gridSize.x, h = ctx.config.gridSize.y;
            var visited = new HashSet<Vector2Int>();
            if (excluded.Contains(start)) return visited;
            if (!ctx.tiles[start.x, start.y].IsWalkable()) return visited;

            visited.Add(start);
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in Dirs4)
                {
                    var n = cur + d;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                    if (visited.Contains(n)) continue;
                    if (excluded.Contains(n)) continue;
                    if (!ctx.tiles[n.x, n.y].IsWalkable()) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return visited;
        }
    }
}
