using UnityEngine;

namespace DarkSpire
{
    // Instantiates one SpriteRenderer per tile from a GeneratedFloorData.
    // Falls back to a runtime-created white sprite tinted by tile type when
    // the MapEntitySpriteLibrary has no authored sprites yet, so Phase 3 is
    // verifiable before art lands.
    //
    // Tiles occupy unit cells: tile (x, y) -> world center (x + 0.5, y + 0.5).
    public class FloorRenderer : MonoBehaviour
    {
        [Tooltip("Optional override. Leave null to use the singleton " +
                 "MapEntitySpriteLibrary.Instance. DungeonBootstrap resolves " +
                 "the library and assigns it via SetSpriteLibrary at runtime.")]
        [SerializeField] private MapEntitySpriteLibrary spriteLibrary;
        [SerializeField] private int sortingOrder = 0;

        // Resolves the live library: prefers the explicit reference, falls
        // back to the singleton. Reads each access so a singleton that lands
        // mid-frame (asset import, late preload) is picked up.
        private MapEntitySpriteLibrary Library =>
            MapEntitySpriteLibrary.ResolveOrSingleton(spriteLibrary);

        private Transform parent;
        private Sprite whiteSprite;

        // Rest-tile overlays — campsite sprites layered ON TOP of a Floor tile
        // at every TileType.Rest position. Tracked here so the walkover
        // dispatcher can ask us to remove a specific overlay when the player
        // uses the rest, without touching the underlying Floor sprite.
        private readonly System.Collections.Generic.Dictionary<Vector2Int, GameObject> restOverlays = new();

        private static readonly Color WallTint  = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color EmptyTint = new Color(0.04f, 0.04f, 0.05f); // darker than Wall — reads as "outside"
        private static readonly Color FloorTint = new Color(0.62f, 0.62f, 0.65f);
        private static readonly Color StartTint = new Color(0.27f, 0.53f, 1.00f);
        private static readonly Color StairTint = new Color(0.63f, 0.50f, 1.00f);
        private static readonly Color RestTint  = new Color(0.25f, 1.00f, 0.50f);

        public void SetSpriteLibrary(MapEntitySpriteLibrary lib) => spriteLibrary = lib;

        public void Render(GeneratedFloorData floor)
        {
            EnsureWhiteSprite();
            ResetParent();
            restOverlays.Clear();

            var holder = RunStateHolder.Instance;
            for (int x = 0; x < floor.gridSize.x; x++)
            {
                for (int y = 0; y < floor.gridSize.y; y++)
                {
                    var t = floor.tiles[x, y];
                    SpawnTile(t, x, y);

                    // Rest tiles get a Floor sprite underneath + a campsite
                    // overlay on top. Skip the overlay for rests already used
                    // (resume from combat after the player camped here).
                    if (t == TileType.Rest)
                    {
                        var pos = new Vector2Int(x, y);
                        bool used = holder != null && holder.IsRestUsed(pos);
                        if (!used) SpawnRestOverlay(pos);
                    }
                }
            }
        }

        public void MarkRestUsed(Vector2Int pos)
        {
            if (restOverlays.TryGetValue(pos, out var go))
            {
                if (go != null) DestroyImmediateOrPlay(go);
                restOverlays.Remove(pos);
            }
        }

        public void Clear()
        {
            if (parent != null) DestroyImmediateOrPlay(parent.gameObject);
            parent = null;
        }

        private void EnsureWhiteSprite()
        {
            if (whiteSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(
                tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            whiteSprite.name = "RuntimeWhite";
        }

        private void ResetParent()
        {
            if (parent != null) DestroyImmediateOrPlay(parent.gameObject);
            var go = new GameObject("FloorTiles");
            go.transform.SetParent(transform, worldPositionStays: false);
            parent = go.transform;
        }

        private void SpawnTile(TileType type, int x, int y)
        {
            var go = new GameObject($"T_{x}_{y}_{type}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            ApplySprite(sr, type);
        }

        private void ApplySprite(SpriteRenderer sr, TileType type)
        {
            // Try authored sprite first — pulls from explicit override or
            // the singleton, whichever is set.
            Sprite authored = null;
            var lib = Library;
            if (lib != null)
            {
                authored = type switch
                {
                    TileType.Wall     => lib.wallTile,
                    TileType.Empty    => lib.emptyTile,
                    TileType.Floor    => lib.floorTile,
                    TileType.Start    => lib.startTile,
                    TileType.Stairway => lib.stairway,
                    // Rest tiles render the Floor sprite underneath. The
                    // campsite glyph is added on top by SpawnRestOverlay so
                    // that consuming the rest can destroy the overlay without
                    // also wiping the floor.
                    TileType.Rest     => lib.floorTile,
                    _ => null,
                };
            }

            if (authored != null)
            {
                sr.sprite = authored;
                sr.color = Color.white;
                return;
            }

            // Fallback: tinted white sprite.
            sr.sprite = whiteSprite;
            sr.color = type switch
            {
                TileType.Wall     => WallTint,
                TileType.Empty    => EmptyTint,
                TileType.Floor    => FloorTint,
                TileType.Start    => StartTint,
                TileType.Stairway => StairTint,
                // Rest base tile uses Floor's tint — the campsite is layered
                // on top of it via the overlay.
                TileType.Rest     => FloorTint,
                _ => Color.magenta,
            };
        }

        private void SpawnRestOverlay(Vector2Int pos)
        {
            var lib = Library;
            Sprite campsiteSprite = lib != null ? lib.campsite : null;
            // Even without a campsite sprite authored, spawn an overlay GameObject
            // so the marker exists for tracking; tint it for the placeholder.

            var go = new GameObject($"RestOverlay_{pos.x}_{pos.y}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder + 1; // above the Floor base
            if (campsiteSprite != null)
            {
                sr.sprite = campsiteSprite;
                sr.color = Color.white;
            }
            else
            {
                sr.sprite = whiteSprite;
                sr.color = RestTint;
            }

            restOverlays[pos] = go;
        }

        private static void DestroyImmediateOrPlay(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
