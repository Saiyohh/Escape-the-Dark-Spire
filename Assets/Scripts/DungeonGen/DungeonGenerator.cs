using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkSpire
{
    public static class DungeonGenerator
    {
        public static GeneratedFloorData Generate(FloorGenerationConfigSO config, int seed)
        {
            if (config == null)
            {
                Debug.LogError("[DungeonGenerator] config is null");
                return null;
            }

            var sw = Stopwatch.StartNew();
            int attempts = 0;
            int maxAttempts = Mathf.Max(1, config.maxRegenerationAttempts);

            while (attempts < maxAttempts)
            {
                var ctx = new GenContext(config, new SeededRandom(seed + attempts));

                if (!PlaceRooms.Apply(ctx))
                {
                    attempts++;
                    continue;
                }
                ConnectRooms.Apply(ctx);
                PlaceDeadEnds.Apply(ctx);
                PlaceEntities.Apply(ctx);
                PlaceBossGate.Apply(ctx);
                PlaceExtras.Apply(ctx);

                if (ValidateFloor.Apply(ctx))
                {
                    DemoteIsolatedWallsToEmpty(ctx);

                    sw.Stop();
                    return BuildResult(ctx, seed, attempts, sw.Elapsed.TotalMilliseconds);
                }

                attempts++;
            }

            Debug.LogError($"[DungeonGenerator] Failed to generate a valid floor after {maxAttempts} attempts (base seed {seed}).");
            return null;
        }

        private static void DemoteIsolatedWallsToEmpty(GenContext ctx)
        {
            int w = ctx.config.gridSize.x;
            int h = ctx.config.gridSize.y;

            var toDemote = new System.Collections.Generic.List<Vector2Int>(64);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (ctx.tiles[x, y] != TileType.Wall) continue;
                    if (HasWalkableNeighbor(ctx.tiles, x, y, w, h)) continue;
                    toDemote.Add(new Vector2Int(x, y));
                }
            }

            for (int i = 0; i < toDemote.Count; i++)
            {
                var p = toDemote[i];
                ctx.tiles[p.x, p.y] = TileType.Empty;
            }
        }

        private static bool HasWalkableNeighbor(TileType[,] tiles, int cx, int cy, int w, int h)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (tiles[nx, ny].IsWalkable()) return true;
                }
            }
            return false;
        }

        private static GeneratedFloorData BuildResult(
            GenContext ctx, int seed, int attempts, double elapsedMs)
        {
            int total = ctx.config.gridSize.x * ctx.config.gridSize.y;
            int floor = 0;
            for (int x = 0; x < ctx.config.gridSize.x; x++)
                for (int y = 0; y < ctx.config.gridSize.y; y++)
                    if (ctx.tiles[x, y].IsWalkable()) floor++;

            ctx.stats.totalTiles = total;
            ctx.stats.floorTiles = floor;
            ctx.stats.wallTiles = total - floor;
            ctx.stats.regenerationAttempts = attempts;
            ctx.stats.generationTimeMs = (float)elapsedMs;

            var path = GridBfs.FindPath(
                ctx.tiles, ctx.startPos, ctx.bossGatePos,
                t => t.IsWalkable());
            ctx.stats.criticalPathLength = path != null ? path.Count : -1;

            return new GeneratedFloorData
            {
                gridSize = ctx.config.gridSize,
                tiles = ctx.tiles,
                entities = ctx.entities,
                monsterSpawns = ctx.monsters,
                encounterPool = ctx.config.encounterPool,
                stats = ctx.stats,
                seed = seed,
                startPosition = ctx.startPos,
                stairwayPosition = ctx.stairwayPos,
                bossGatePosition = ctx.bossGatePos,
            };
        }
    }
}
