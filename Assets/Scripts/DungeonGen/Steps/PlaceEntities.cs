using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // tile come in Step 5; extras (bonus chest/shrine/rest at dead ends) come
    // in Step 5.5.
    internal static class PlaceEntities
    {
        public static void Apply(GenContext ctx)
        {
            foreach (var room in ctx.rooms)
            {
                switch (room.kind)
                {
                    case RoomKind.Start:
                        ctx.startPos = room.Center;
                        ctx.tiles[ctx.startPos.x, ctx.startPos.y] = TileType.Start;
                        break;

                    case RoomKind.Key:
                        ctx.entities.Add(new EntityPlacement(room.Center, EntityKind.Key));
                        // Key Room guardian — Standard monster patrolling the room.
                        ctx.monsters.Add(new MonsterSpawn(
                            MonsterTier.Standard,
                            room.Center,
                            BuildRoomPatrol(room)));
                        break;

                    case RoomKind.Loot:
                        // 1-2 chests in a Loot Room.
                        int chestCount = ctx.rng.NextRangeInclusive(1, 2);
                        var chestPositions = SpreadInRoom(room, chestCount, ctx.rng);
                        foreach (var p in chestPositions)
                            ctx.entities.Add(new EntityPlacement(p, EntityKind.Chest));
                        break;

                    case RoomKind.Rest:
                        // Mark the center tile as a Rest tile in the grid itself
                        // (per Exploration spec — rest tiles render via TileType, not entity).
                        ctx.tiles[room.Center.x, room.Center.y] = TileType.Rest;
                        break;

                    case RoomKind.Shrine:
                        ctx.entities.Add(new EntityPlacement(room.Center, EntityKind.Shrine));
                        break;

                    case RoomKind.Empty:
                    case RoomKind.Boss:
                    default:
                        // Empty rooms get monster patrols added below; Boss room is
                        // handled in Step 5 (gate + stairway + boss spawn).
                        break;
                }
            }

            PlaceCorridorMonsters(ctx);
            if (ctx.config.spawnElite) PlaceRoamingElite(ctx);
            PlaceBoss(ctx);
            PlaceDeadEndLoot(ctx);
        }

        // Boss spawn lives in the boss room; the gate + stairway tile come in Step 5.
        private static void PlaceBoss(GenContext ctx)
        {
            var bossRoom = ctx.FirstRoomOfKind(RoomKind.Boss);
            if (bossRoom == null) return;

            ctx.monsters.Add(new MonsterSpawn(
                MonsterTier.Boss,
                bossRoom.Value.Center,
                new List<Vector2Int>()));   // Boss does not patrol.
        }

        private static void PlaceCorridorMonsters(GenContext ctx)
        {
            var corridorTiles = CollectCorridorTiles(ctx);
            if (corridorTiles.Count == 0) return;

            // Spawn safety bubble: keep enemies far enough from the player's
            // spawn that they can't immediately see/chase. Measured as path
            // distance through walkable tiles, not Manhattan, so a monster on
            // the other side of a wall right next to the start room still
            // counts as far away.
            int safeSpawnDist = Mathf.Max(
                8, Mathf.CeilToInt(ctx.config.enemyDetectionRadius) + 5);

            // Min walkable-path distance between two corridor monsters. Path
            // distance (not Manhattan) is what matters in 1-tile corridors —
            // two monsters at Manhattan 4 in the same passage still cork it.
            const int MinMonsterPathDist = 6;

            int count = ctx.config.corridorMonsterCount;
            ctx.rng.Shuffle(corridorTiles);

            var placedStarts = new HashSet<Vector2Int>();
            int placed = 0;
            foreach (var tile in corridorTiles)
            {
                if (placed >= count) break;

                var distField = GridBfs.DistanceField(
                    ctx.tiles, tile, t => t.IsWalkable());

                int distFromStart = distField[ctx.startPos.x, ctx.startPos.y];
                if (distFromStart < 0 || distFromStart < safeSpawnDist) continue;

                if (TooCloseToOtherMonsterByPath(distField, placedStarts, MinMonsterPathDist))
                    continue;

                // Don't seal off any room: simulate the dungeon with this
                // monster + every previously placed corridor monster acting
                // as walls, and require every room center to remain reachable
                // from the start. Catches "two enemies completely block you
                // off" without any geometric special-casing.
                if (WouldBlockProgression(ctx, tile, placedStarts)) continue;

                var route = BuildCorridorPatrol(ctx, tile, 5);
                ctx.monsters.Add(new MonsterSpawn(MonsterTier.Standard, tile, route));
                placedStarts.Add(tile);
                placed++;
            }
        }

        private static bool TooCloseToOtherMonsterByPath(
            int[,] distField, HashSet<Vector2Int> others, int minDist)
        {
            foreach (var p in others)
            {
                int d = distField[p.x, p.y];
                if (d >= 0 && d < minDist) return true;
            }
            return false;
        }

        // BFS from start treating `candidate` and every already-placed corridor
        // monster as a wall. Returns true if any room becomes unreachable —
        // i.e. placing the candidate would block player progression.
        private static bool WouldBlockProgression(
            GenContext ctx, Vector2Int candidate, HashSet<Vector2Int> blockerStarts)
        {
            int w = ctx.config.gridSize.x, h = ctx.config.gridSize.y;
            var visited = new HashSet<Vector2Int> { ctx.startPos };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(ctx.startPos);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in Dirs4)
                {
                    var n = cur + d;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                    if (visited.Contains(n)) continue;
                    if (n == candidate) continue;
                    if (blockerStarts.Contains(n)) continue;
                    if (!ctx.tiles[n.x, n.y].IsWalkable()) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }

            foreach (var room in ctx.rooms)
                if (!visited.Contains(room.Center)) return true;
            return false;
        }

        private static void PlaceRoamingElite(GenContext ctx)
        {
            var corridorTiles = CollectCorridorTiles(ctx);
            if (corridorTiles.Count < 10) return;

            // Pick a far-from-start tile so the Elite roams away from the spawn.
            Vector2Int best = corridorTiles[0];
            int bestDist = 0;
            foreach (var t in corridorTiles)
            {
                int d = Mathf.Abs(t.x - ctx.startPos.x) + Mathf.Abs(t.y - ctx.startPos.y);
                if (d > bestDist) { bestDist = d; best = t; }
            }

            var route = BuildCorridorPatrol(ctx, best, 12);
            ctx.monsters.Add(new MonsterSpawn(MonsterTier.Elite, best, route));
        }

        private static void PlaceDeadEndLoot(GenContext ctx)
        {
            foreach (var terminal in ctx.deadEndTerminals)
            {
                float roll = ctx.rng.NextFloat();
                if (roll < 0.4f)
                {
                    ctx.entities.Add(new EntityPlacement(terminal, EntityKind.Chest));
                }
                else if (roll < 0.8f)
                {
                    int gold = ctx.rng.NextRangeInclusive(5, 15);
                    ctx.entities.Add(new EntityPlacement(terminal, EntityKind.GoldPile, gold));
                }
                // remaining 20% → empty terminal.
            }
        }

        // Helpers ------------------------------------------------------------

        private static readonly Vector2Int[] Dirs4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        private static List<Vector2Int> CollectCorridorTiles(GenContext ctx)
        {
            var list = new List<Vector2Int>();
            int w = ctx.config.gridSize.x, h = ctx.config.gridSize.y;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (ctx.tiles[x, y] != TileType.Floor) continue;
                    if (ctx.RoomAt(p).HasValue) continue;
                    list.Add(p);
                }
            return list;
        }

        // Walk along passable tiles from `start`, picking up to `length` waypoints.
        private static List<Vector2Int> BuildCorridorPatrol(GenContext ctx, Vector2Int start, int length)
        {
            var route = new List<Vector2Int> { start };
            var visited = new HashSet<Vector2Int> { start };
            var cur = start;
            for (int i = 0; i < length; i++)
            {
                Vector2Int? next = null;
                var dirs = new List<Vector2Int>(Dirs4);
                ctx.rng.Shuffle(dirs);
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (!ctx.InBounds(n)) continue;
                    if (ctx.tiles[n.x, n.y] == TileType.Wall) continue;
                    if (visited.Contains(n)) continue;
                    next = n; break;
                }
                if (next == null) break;
                route.Add(next.Value);
                visited.Add(next.Value);
                cur = next.Value;
            }
            return route;
        }

        private static List<Vector2Int> BuildRoomPatrol(RoomPlacement room)
        {
            // Loop along the four corners (clockwise) of a Key Room interior so
            // the guardian moves visibly within its room.
            var b = room.bounds;
            return new List<Vector2Int>
            {
                new Vector2Int(b.xMin,         b.yMin),
                new Vector2Int(b.xMax - 1,     b.yMin),
                new Vector2Int(b.xMax - 1,     b.yMax - 1),
                new Vector2Int(b.xMin,         b.yMax - 1),
            };
        }

        private static List<Vector2Int> SpreadInRoom(RoomPlacement room, int count, SeededRandom rng)
        {
            var picks = new List<Vector2Int>();
            var taken = new HashSet<Vector2Int>();
            int safety = 50;
            while (picks.Count < count && safety-- > 0)
            {
                int x = rng.NextRangeInclusive(room.bounds.xMin, room.bounds.xMax - 1);
                int y = rng.NextRangeInclusive(room.bounds.yMin, room.bounds.yMax - 1);
                var p = new Vector2Int(x, y);
                if (taken.Add(p)) picks.Add(p);
            }
            return picks;
        }
    }
}
