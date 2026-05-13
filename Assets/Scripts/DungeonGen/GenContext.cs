using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    internal class GenContext
    {
        public FloorGenerationConfigSO config;
        public SeededRandom rng;
        public TileType[,] tiles;

        public List<RoomPlacement> rooms = new();
        public List<EntityPlacement> entities = new();
        public List<MonsterSpawn> monsters = new();

        public List<Vector2Int> deadEndTerminals = new();

        public Vector2Int startPos;
        public Vector2Int stairwayPos;
        public Vector2Int bossGatePos;

        public GenerationStats stats;

        public GenContext(FloorGenerationConfigSO config, SeededRandom rng)
        {
            this.config = config;
            this.rng = rng;
            tiles = new TileType[config.gridSize.x, config.gridSize.y];
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
