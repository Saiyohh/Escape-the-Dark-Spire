using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "MapEntitySpriteLibrary", menuName = "DarkSpire/Dungeon/Map Entity Sprite Library")]
    public class MapEntitySpriteLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/MapEntitySpriteLibrary.asset";

        private static MapEntitySpriteLibrary _instance;
        public static MapEntitySpriteLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
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

        [Header("Indicators")]
        public Sprite alertIndicator;
        public Sprite chaseIndicator;

        [Header("Party")]
        [Tooltip("Sprite for the party token (the player's avatar on the map). " +
                 "PartyToken reads this when its own override field is null.")]
        public Sprite partyToken;

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

        public static MapEntitySpriteLibrary ResolveOrSingleton(MapEntitySpriteLibrary explicitRef)
        {
            return explicitRef != null ? explicitRef : Instance;
        }
    }
}
