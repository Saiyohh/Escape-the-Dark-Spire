using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Mutable scratch space the generator steps share. Lives only for one
    // generation attempt; on validation failure it's discarded and a new
    // context starts with a different seed offset.
    internal class GenContext
    {
        public FloorGenerationConfigSO config;
        public SeededRandom rng;
        public TileType[,] tiles;

        public List<RoomPlacement> rooms = new();
        public List<EntityPlacement> entities = new();
        public List<MonsterSpawn> monsters = new();

        // Tiles where a corridor terminates with no further opening — candidates
        // for loot/extras placement.
        public List<Vector2Int> deadEndTerminals = new();

        // Resolved by Step 4/5.
        public Vector2Int startPos;
        public Vector2Int stairwayPos;
        public Vector2Int bossGatePos;

        public GenerationStats stats;

        public GenContext(FloorGenerationConfigSO config, SeededRandom rng)
        {
            this.config = config;
            this.rng = rng;
            tiles = new TileType[config.gridSize.x, config.gridSize.y];
            // Default-initialized TileType[] is all Wall (enum value 0) — what we want.
        }

        public bool InBounds(Vector2Int p) =>
            p.x >= 0 && p.y >= 0 && p.x < config.gridSize.x && p.y < config.gridSize.y;

        public bool IsFloor(Vector2Int p) =>
            InBounds(p) && tiles[p.x, p.y].IsWalkable();

        public RoomPlacement? RoomAt(Vector2Int p)
        {
            foreach (var r in rooms)
                if (r.bounds.Contains(p)) return r;
            return null;
        }

        public RoomPlacement? FirstRoomOfKind(RoomKind kind)
        {
            foreach (var r in rooms)
                if (r.kind == kind) return r;
            return null;
        }
    }
}
