// MapEntitySpriteLibrary.cs
// -----------------------------------------------------------------------------
// Single source of truth for map-side (dungeon-exploration) sprites. Every
// system rendering on the dungeon map (FloorRenderer, EntitySpawner,
// MapEntityBase variants, FloorGeneratorWindow, debug overlays) pulls
// sprites from here by enum key.
//
// Mirrors the ConditionLibrary / OrbLibrary singleton pattern: one canonical
// asset at Assets/ScriptableObjects/MapEntitySpriteLibrary.asset, registered
// in PlayerSettings.preloadedAssets so the static Instance is wired at game
// start without a Resources folder.
//
// Per-consumer SerializedField references to a specific library asset still
// work (and override the singleton lookup) — useful if a debug/test scene
// wants a stub sprite set. Production code can rely on Instance.
//
// Kept generic-by-tier on monsters: standard/elite/boss share one sprite
// each. The Encounter Manager's unpredictability contract relies on this.
// -----------------------------------------------------------------------------
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "MapEntitySpriteLibrary", menuName = "DarkSpire/Dungeon/Map Entity Sprite Library")]
    public class MapEntitySpriteLibrary : ScriptableObject
    {
        // Canonical project-relative path. The DarkSpire/Map Sprites/Create
        // Library menu both creates the asset here and registers it into
        // PlayerSettings.preloadedAssets so Unity loads it at runtime
        // without a Resources folder.
        public const string AssetPath = "Assets/ScriptableObjects/MapEntitySpriteLibrary.asset";

        // ── Singleton access ─────────────────────────────────────────────────
        private static MapEntitySpriteLibrary _instance;
        public static MapEntitySpriteLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<MapEntitySpriteLibrary>(AssetPath);
#endif
                if (_instance == null)
                    Debug.LogWarning(
                        $"[MapEntitySpriteLibrary] Missing asset at {AssetPath}. " +
                        "Create one via DarkSpire → Map Sprites → Create Library. " +
                        "Map-side rendering will fall back to runtime placeholders.");
                return _instance;
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        [Header("Monster tiers")]
        public Sprite standardMonster;
        public Sprite eliteMonster;
        public Sprite boss;

        [Header("Encounter tiles")]
        public Sprite campsite;
        public Sprite eventTile;
        public Sprite shrine;

        [Header("Collectibles & interactables")]
        public Sprite key;
        public Sprite chest;
        public Sprite chestOpen;
        public Sprite goldPile;
        public Sprite stairway;
        public Sprite stairwayLocked;

        [Header("Boss Gate (directional)")]
        [Tooltip("Direction-facing variants of the boss gate. PlaceBossGate writes " +
                 "the cardinal direction from gate → room into EntityPlacement.strPayload " +
                 "(\"N\"/\"S\"/\"E\"/\"W\") and the renderer picks the matching sprite.")]
        public Sprite bossGateNorth;
        public Sprite bossGateSouth;
        public Sprite bossGateEast;
        public Sprite bossGateWest;
        public Sprite bossGateOpenNorth;
        public Sprite bossGateOpenSouth;
        public Sprite bossGateOpenEast;
        public Sprite bossGateOpenWest;

        /// <summary>
        /// Picks the boss-gate sprite for the given facing code ("N"/"S"/"E"/"W",
        /// matching what PlaceBossGate writes to EntityPlacement.strPayload).
        /// Returns null if the corresponding slot is empty.
        /// </summary>
        public Sprite GetBossGateSprite(string facing, bool open) => open
            ? facing switch
              {
                  "N" => bossGateOpenNorth,
                  "S" => bossGateOpenSouth,
                  "E" => bossGateOpenEast,
                  "W" => bossGateOpenWest,
                  _   => null,
              }
            : facing switch
              {
                  "N" => bossGateNorth,
                  "S" => bossGateSouth,
                  "E" => bossGateEast,
                  "W" => bossGateWest,
                  _   => null,
              };

        [Header("Tile types")]
        public Sprite floorTile;
        public Sprite wallTile;
        [Tooltip("Void outside the walls (TileType.Empty). Visually distinct " +
                 "from wallTile so corridors and rooms read as 3D-looking " +
                 "shapes; behaviorally identical (impassable).")]
        public Sprite emptyTile;
        public Sprite startTile;
        // No 'restTile' slot: TileType.Rest renders using `campsite` since
        // they're conceptually the same thing — the campsite IS the rest tile,
        // and walkover triggers the "Press E to rest" interaction.

        [Header("Indicators")]
        public Sprite alertIndicator;
        public Sprite chaseIndicator;

        [Header("Party")]
        [Tooltip("Sprite for the party token (the player's avatar on the map). " +
                 "PartyToken reads this when its own override field is null.")]
        public Sprite partyToken;

        [Header("Sprite scale overrides")]
        [Tooltip("Per-entity scale multiplier applied when the authored sprite " +
                 "is spawned. Raise to make an icon larger, lower to shrink. " +
                 "1.0 = use the sprite's native size (one tile wide). Affects " +
                 "authored sprites only — the fallback placeholders ignore this " +
                 "since they already use a runtime scale.")]
        public IconScales scales = new IconScales();

        [System.Serializable]
        public class IconScales
        {
            [Range(0.1f, 3f)] public float standardMonster = 1f;
            [Range(0.1f, 3f)] public float eliteMonster    = 1f;
            [Range(0.1f, 3f)] public float boss            = 1f;
            [Range(0.1f, 3f)] public float key             = 1f;
            [Range(0.1f, 3f)] public float chest           = 1f;
            [Range(0.1f, 3f)] public float chestOpen       = 1f;
            [Range(0.1f, 3f)] public float goldPile        = 1f;
            [Range(0.1f, 3f)] public float shrine          = 1f;
            [Range(0.1f, 3f)] public float bossGate        = 1f;
            [Range(0.1f, 3f)] public float campsite        = 1f;
            [Range(0.1f, 3f)] public float eventTile       = 1f;
            [Range(0.1f, 3f)] public float partyToken      = 1f;
        }

        public float GetEntityScale(EntityKind kind) => kind switch
        {
            EntityKind.Key      => scales.key,
            EntityKind.Chest    => scales.chest,
            EntityKind.GoldPile => scales.goldPile,
            EntityKind.Shrine   => scales.shrine,
            EntityKind.BossGate => scales.bossGate,
            _ => 1f,
        };

        public float GetMonsterScale(MonsterTier tier) => tier switch
        {
            MonsterTier.Standard => scales.standardMonster,
            MonsterTier.Elite    => scales.eliteMonster,
            MonsterTier.Boss     => scales.boss,
            _ => 1f,
        };

        public Sprite GetByKey(MapSpriteKey spriteKey) => spriteKey switch
        {
            MapSpriteKey.StandardMonster  => standardMonster,
            MapSpriteKey.EliteMonster     => eliteMonster,
            MapSpriteKey.Boss             => boss,
            MapSpriteKey.Campsite         => campsite,
            MapSpriteKey.EventTile        => eventTile,
            MapSpriteKey.Shrine           => shrine,
            MapSpriteKey.Key              => key,
            MapSpriteKey.Chest            => chest,
            MapSpriteKey.ChestOpen        => chestOpen,
            MapSpriteKey.GoldPile         => goldPile,
            MapSpriteKey.Stairway         => stairway,
            MapSpriteKey.StairwayLocked   => stairwayLocked,
            MapSpriteKey.FloorTile        => floorTile,
            MapSpriteKey.WallTile         => wallTile,
            MapSpriteKey.EmptyTile        => emptyTile,
            MapSpriteKey.StartTile        => startTile,
            MapSpriteKey.AlertIndicator   => alertIndicator,
            MapSpriteKey.ChaseIndicator   => chaseIndicator,
            MapSpriteKey.PartyToken       => partyToken,
            _ => null,
        };

        /// <summary>
        /// Convenience for consumers: prefer the explicitly-assigned
        /// library, otherwise fall back to the singleton. Centralizes the
        /// "did the designer drag one in or should we use the global?"
        /// pattern across FloorRenderer, EntitySpawner, MapEntityBase, etc.
        /// </summary>
        public static MapEntitySpriteLibrary ResolveOrSingleton(MapEntitySpriteLibrary explicitRef)
        {
            return explicitRef != null ? explicitRef : Instance;
        }
    }
}
