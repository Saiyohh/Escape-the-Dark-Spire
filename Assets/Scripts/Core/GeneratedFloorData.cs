using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class GeneratedFloorData
    {
        public Vector2Int gridSize;
        public TileType[,] tiles;
        public List<EntityPlacement> entities = new();
        public List<MonsterSpawn> monsterSpawns = new();
        public FloorEncounterDataSO encounterPool;
        public GenerationStats stats;
        public int seed;

        public Vector2Int startPosition;     // resolved during Step 4
        public Vector2Int stairwayPosition;  // inside boss room
        public Vector2Int bossGatePosition;

        public bool InBounds(Vector2Int p) =>
            p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;

        public TileType TileAt(Vector2Int p) => tiles[p.x, p.y];

        public bool IsPassable(Vector2Int p) =>
            InBounds(p) && tiles[p.x, p.y] != TileType.Wall;
    }
}
