using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    internal static class PlaceExtras
    {
        public static void Apply(GenContext ctx)
        {
            var distFromStart = GridBfs.DistanceField(
                ctx.tiles, ctx.startPos,
                t => t.IsWalkable());

            var scored = new List<(Vector2Int pos, int detour)>();
            foreach (var t in ctx.deadEndTerminals)
            {
                if (!ctx.InBounds(t)) continue;
                int distToTerminal = distFromStart[t.x, t.y];
                if (distToTerminal < 0) continue;

                int distToNearestCritical = NearestCriticalRoomDist(ctx, t, distFromStart);
                int detour = distToTerminal - distToNearestCritical;
                scored.Add((t, detour));
            }

            scored.Sort((a, b) => b.detour.CompareTo(a.detour));

            int placed = 0;
            placed += MaybePlaceSingleTile(ctx, scored, ctx.config.extraChestChance, EntityKind.Chest);
            placed += MaybePlaceWithSpace(ctx, scored, ctx.config.extraShrineChance, EntityKind.Shrine);
            placed += MaybePlaceWithSpace(ctx, scored, ctx.config.extraRestChance, EntityKind.GoldPile,
                                          markRestTile: true);

            ctx.stats.extrasPlaced = placed;
        }

        private static int NearestCriticalRoomDist(
            GenContext ctx, Vector2Int from, int[,] distFromStart)
        {
            int best = int.MaxValue;
            foreach (var room in ctx.rooms)
            {
                if (room.kind == RoomKind.Empty || room.kind == RoomKind.Loot) continue;
                int d = Mathf.Abs(room.Center.x - from.x) + Mathf.Abs(room.Center.y - from.y);
                if (d < best) best = d;
            }
            return best == int.MaxValue ? 0 : best;
        }

        private static int MaybePlaceSingleTile(
            GenContext ctx, List<(Vector2Int pos, int detour)> scored,
            float chance, EntityKind kind)
        {
            if (!ctx.rng.RollChance(chance)) return 0;
            foreach (var (pos, _) in scored)
            {
                if (TileAlreadyOccupied(ctx, pos)) continue;
                ctx.entities.Add(new EntityPlacement(pos, kind));
                return 1;
            }
            return 0;
        }

        private static int MaybePlaceWithSpace(
            GenContext ctx, List<(Vector2Int pos, int detour)> scored,
            float chance, EntityKind kind, bool markRestTile = false)
        {
            if (!ctx.rng.RollChance(chance)) return 0;
            foreach (var (pos, _) in scored)
            {
                if (TileAlreadyOccupied(ctx, pos)) continue;
                if (markRestTile)
                {
                    ctx.tiles[pos.x, pos.y] = TileType.Rest;
                }
                else
                {
                    ctx.entities.Add(new EntityPlacement(pos, kind));
                }
                return 1;
            }
            return 0;
        }

        private static bool TileAlreadyOccupied(GenContext ctx, Vector2Int pos)
        {
            foreach (var e in ctx.entities)
                if (e.position == pos) return true;
            var t = ctx.tiles[pos.x, pos.y];
            if (t == TileType.Start || t == TileType.Stairway || t == TileType.Rest) return true;
            return false;
        }
    }
}
