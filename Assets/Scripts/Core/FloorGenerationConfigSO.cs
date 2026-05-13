using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "FLOOR_New", menuName = "DarkSpire/Dungeon/Floor Generation Config")]
    public class FloorGenerationConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public int floorNumber = 1;
        public string displayName = "Floor 1";

        [Header("Grid")]
        public Vector2Int gridSize = new Vector2Int(25, 25);

        [Tooltip("Minimum empty-tile gap required between two room edges during placement.")]
        public int minRoomSpacing = 3;

        [Header("Rooms")]
        public int minRoomCount = 6;
        public int maxRoomCount = 8;

        [Tooltip("Extra (non-MST) corridor connections added after the spanning tree, " +
                 "to create loops and alternate routes.")]
        public int extraConnections = 1;

        [Header("Dead Ends")]
        public int minDeadEndBranches = 2;
        public int maxDeadEndBranches = 3;

        [Tooltip("Length range (in tiles) for an extra dead-end branch carved off a corridor.")]
        public Vector2Int deadEndLengthRange = new Vector2Int(3, 6);

        [Header("Progression")]
        public int keysRequired = 2;
        public int keyRoomCount = 2;
        public int restSiteCount = 2;
        public int shrineRoomCount = 1;

        [Header("Monsters")]
        [Tooltip("Standard monsters placed on corridors and Empty rooms. Key Rooms each " +
                 "spawn one additional Standard guardian, so total Standard count = " +
                 "corridorMonsterCount + keyRoomCount.")]
        public int corridorMonsterCount = 4;

        [Tooltip("If true, one roaming Elite is placed with a long patrol route across " +
                 "multiple corridors.")]
        public bool spawnElite = false;

        [Header("Vision")]
        [Tooltip("Party sight radius in tiles — feeds the Fog of War reveal radius.")]
        public float sightRadius = 5.0f;

        [Tooltip("Monster Patrol -> Alert detection radius in tiles. Requires LoS.")]
        public float enemyDetectionRadius = 3.0f;

        [Header("Extras Roll Chances (post-gen)")]
        [Range(0f, 1f)] public float extraChestChance = 0.3f;
        [Range(0f, 1f)] public float extraShrineChance = 0.3f;
        [Range(0f, 1f)] public float extraRestChance = 0.3f;

        [Header("Encounters")]
        public FloorEncounterDataSO encounterPool;

        [Header("Generation Safety")]
        [Tooltip("Max attempts at room placement before the generator gives up and " +
                 "regenerates with a new seed.")]
        public int maxPlacementAttempts = 200;

        [Tooltip("Max full-floor regenerations on validation failure before logging an error.")]
        public int maxRegenerationAttempts = 5;
    }
}
