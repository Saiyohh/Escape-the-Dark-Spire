using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Serialized snapshot of a single generated floor. Lets designers pin a
    // specific seed for tutorials, fixed test fixtures, or screenshots.
    //
    // Runtime DungeonBootstrap (Phase 3) can take a snapshot SO instead of a
    // FloorGenerationConfigSO + seed and skip the generator entirely.
    [CreateAssetMenu(fileName = "GFS_NewSnapshot", menuName = "DarkSpire/Dungeon/Generated Floor Snapshot")]
    public class GeneratedFloorSO : ScriptableObject
    {
        public int seed;
        public string sourceConfigName;
        public Vector2Int gridSize;

        // Row-major flat: index = y * gridSize.x + x. Unity can't serialize 2D arrays.
        public TileType[] tilesFlat;

        public List<EntityPlacement> entities = new();
        public List<MonsterSpawn> monsterSpawns = new();

        public Vector2Int startPosition;
        public Vector2Int stairwayPosition;
        public Vector2Int bossGatePosition;

        public FloorEncounterDataSO encounterPool;
        public GenerationStats stats;

        public TileType GetTile(int x, int y) => tilesFlat[y * gridSize.x + x];

        public GeneratedFloorData ToRuntimeData()
        {
            var d = new GeneratedFloorData
            {
                gridSize = gridSize,
                tiles = new TileType[gridSize.x, gridSize.y],
                entities = new List<EntityPlacement>(entities),
                monsterSpawns = new List<MonsterSpawn>(monsterSpawns),
                encounterPool = encounterPool,
                stats = stats,
                seed = seed,
                startPosition = startPosition,
                stairwayPosition = stairwayPosition,
                bossGatePosition = bossGatePosition,
            };
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                    d.tiles[x, y] = tilesFlat[y * gridSize.x + x];
            return d;
        }

        public static GeneratedFloorSO FromRuntimeData(GeneratedFloorData data, string sourceConfigName)
        {
            var so = CreateInstance<GeneratedFloorSO>();
            so.seed = data.seed;
            so.sourceConfigName = sourceConfigName;
            so.gridSize = data.gridSize;
            so.tilesFlat = new TileType[data.gridSize.x * data.gridSize.y];
            for (int x = 0; x < data.gridSize.x; x++)
                for (int y = 0; y < data.gridSize.y; y++)
                    so.tilesFlat[y * data.gridSize.x + x] = data.tiles[x, y];
            so.entities = new List<EntityPlacement>(data.entities);
            so.monsterSpawns = new List<MonsterSpawn>(data.monsterSpawns);
            so.startPosition = data.startPosition;
            so.stairwayPosition = data.stairwayPosition;
            so.bossGatePosition = data.bossGatePosition;
            so.encounterPool = data.encounterPool;
            so.stats = data.stats;
            return so;
        }
    }
}
