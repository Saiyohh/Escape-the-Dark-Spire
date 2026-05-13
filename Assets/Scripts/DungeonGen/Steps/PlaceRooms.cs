using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    internal static class PlaceRooms
    {
        public static bool Apply(GenContext ctx)
        {
            var kinds = AllocateRoomKinds(ctx);

            foreach (var kind in kinds)
            {
                var size = PickRoomSize(kind, ctx.rng);
                if (!TryPlace(ctx, kind, size))
                {
                    return false;
                }
            }

            foreach (var room in ctx.rooms)
                StampRoom(ctx, room);

            ctx.stats.roomCount = ctx.rooms.Count;
            return true;
        }

        private static List<RoomKind> AllocateRoomKinds(GenContext ctx)
        {
            var cfg = ctx.config;
            var roomCount = ctx.rng.NextRangeInclusive(cfg.minRoomCount, cfg.maxRoomCount);

            var kinds = new List<RoomKind>(roomCount);

            kinds.Add(RoomKind.Start);
            kinds.Add(RoomKind.Boss);
            for (int i = 0; i < cfg.keyRoomCount; i++) kinds.Add(RoomKind.Key);
            for (int i = 0; i < cfg.restSiteCount; i++) kinds.Add(RoomKind.Rest);
            for (int i = 0; i < cfg.shrineRoomCount; i++) kinds.Add(RoomKind.Shrine);
            kinds.Add(RoomKind.Loot);     // 1 guaranteed loot room

            while (kinds.Count < roomCount) kinds.Add(RoomKind.Empty);

            return kinds;
        }

        private static Vector2Int PickRoomSize(RoomKind kind, SeededRandom rng) => kind switch
        {
            RoomKind.Start  => new Vector2Int(3, 3),
            RoomKind.Loot   => new Vector2Int(3, 3),
            RoomKind.Key    => new Vector2Int(4, 4),
            RoomKind.Rest   => new Vector2Int(3, 3),
            RoomKind.Shrine => new Vector2Int(3, 3),
            RoomKind.Boss   => new Vector2Int(6, 6),
            RoomKind.Empty  => new Vector2Int(
                                    rng.NextRangeInclusive(3, 5),
                                    rng.NextRangeInclusive(3, 5)),
            _ => new Vector2Int(3, 3),
        };

        private static bool TryPlace(GenContext ctx, RoomKind kind, Vector2Int size)
        {
            var cfg = ctx.config;
            int spacing = cfg.minRoomSpacing;
            int xMax = cfg.gridSize.x - size.x - 1;
            int yMax = cfg.gridSize.y - size.y - 1;
            if (xMax < 1 || yMax < 1) return false;

            for (int attempt = 0; attempt < cfg.maxPlacementAttempts; attempt++)
            {
                int x = ctx.rng.NextRangeInclusive(1, xMax);
                int y = ctx.rng.NextRangeInclusive(1, yMax);
                var bounds = new RectInt(x, y, size.x, size.y);

                if (Overlaps(ctx, bounds, spacing)) continue;

                ctx.rooms.Add(new RoomPlacement(kind, bounds));
                return true;
            }
            return false;
        }

        private static bool Overlaps(GenContext ctx, RectInt bounds, int spacing)
        {
            var inflated = new RectInt(
                bounds.xMin - spacing,
                bounds.yMin - spacing,
                bounds.width + spacing * 2,
                bounds.height + spacing * 2);

            foreach (var existing in ctx.rooms)
            {
                if (inflated.Overlaps(existing.bounds)) return true;
            }
            return false;
        }

        private static void StampRoom(GenContext ctx, RoomPlacement room)
        {
            for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
                    ctx.tiles[x, y] = TileType.Floor;
        }
    }
}
