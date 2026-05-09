// Minimap.cs
// -----------------------------------------------------------------------------
// Corner panel rendering revealed-only tiles of the current floor. Uses a
// Texture2D drawn at tileSize px per tile, displayed via a RawImage. Reads
// FogOfWar.IsRevealed for the visible mask and PartyToken.GridPos for the
// party-position dot.
//
// LateUpdate redraws only the deltas: tiles that flipped from unrevealed to
// revealed since the last frame, plus the party-token tile (and the previous
// one — cleared back to its base color).
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class Minimap : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RawImage display;
        [SerializeField] private int tileSize = 4;

        [Header("Sources")]
        [SerializeField] private FogOfWar fog;
        [SerializeField] private PartyToken party;

        // Palette.
        private static readonly Color ColorUnrevealed = new(0f, 0f, 0f, 0.85f);
        private static readonly Color ColorWall       = new(0.18f, 0.18f, 0.20f, 1f);
        private static readonly Color ColorFloor      = new(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color ColorStart      = new(0.40f, 0.85f, 0.40f, 1f);
        private static readonly Color ColorStairway   = new(0.30f, 0.95f, 0.50f, 1f);
        private static readonly Color ColorRest       = new(0.40f, 0.65f, 0.95f, 1f);
        private static readonly Color ColorKey        = new(1f, 0.85f, 0.20f, 1f);
        private static readonly Color ColorChest      = new(0.95f, 0.75f, 0.20f, 1f);
        private static readonly Color ColorGold       = new(1f, 0.95f, 0.40f, 1f);
        private static readonly Color ColorShrine     = new(0.80f, 0.40f, 0.95f, 1f);
        private static readonly Color ColorGate       = new(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Color ColorParty      = new(1f, 1f, 0.40f, 1f);

        private GeneratedFloorData floor;
        private Texture2D texture;
        private bool[,] paintedRevealed;
        private Vector2Int prevPartyTile = new(-1, -1);
        private bool dirty;

        // ─── Public API ──────────────────────────────────────────────────────

        public static Minimap GetOrCreateInScene(Transform parent)
        {
            var existing = FindAnyObjectByType<Minimap>();
            if (existing != null) return existing;
            var go = BuildRuntimeFallback();
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<Minimap>();
        }

        public void Bind(GeneratedFloorData floorIn, FogOfWar fogIn, PartyToken partyIn)
        {
            floor = floorIn;
            fog = fogIn;
            party = partyIn;
            BuildTexture();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (floor == null || fog == null || texture == null) return;
            RepaintRevealedDeltas();
            RepaintPartyDot();
            if (dirty)
            {
                texture.Apply(updateMipmaps: false);
                dirty = false;
            }
        }

        // ─── Texture build + paint ───────────────────────────────────────────

        private void BuildTexture()
        {
            if (floor == null) return;
            int w = floor.gridSize.x * tileSize;
            int h = floor.gridSize.y * tileSize;
            if (w <= 0 || h <= 0) return;

            texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            // Initialize all to unrevealed.
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = ColorUnrevealed;
            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false);

            paintedRevealed = new bool[floor.gridSize.x, floor.gridSize.y];
            prevPartyTile = new Vector2Int(-1, -1);

            if (display != null) display.texture = texture;
        }

        private void RepaintRevealedDeltas()
        {
            int gw = floor.gridSize.x, gh = floor.gridSize.y;
            for (int x = 0; x < gw; x++)
            {
                for (int y = 0; y < gh; y++)
                {
                    if (paintedRevealed[x, y]) continue;
                    if (!fog.IsRevealed(x, y)) continue;
                    PaintTile(x, y, ResolveTileColor(x, y));
                    paintedRevealed[x, y] = true;
                    dirty = true;
                }
            }
        }

        private void RepaintPartyDot()
        {
            if (party == null) return;
            var cur = party.GridPos;
            if (cur == prevPartyTile) return;

            // Restore previous tile's base color.
            if (prevPartyTile.x >= 0 && prevPartyTile.y >= 0)
            {
                if (fog.IsRevealed(prevPartyTile))
                    PaintTile(prevPartyTile.x, prevPartyTile.y, ResolveTileColor(prevPartyTile.x, prevPartyTile.y));
                else
                    PaintTile(prevPartyTile.x, prevPartyTile.y, ColorUnrevealed);
            }

            // Paint party dot.
            if (cur.x >= 0 && cur.y >= 0 && cur.x < floor.gridSize.x && cur.y < floor.gridSize.y)
                PaintTile(cur.x, cur.y, ColorParty);

            prevPartyTile = cur;
            dirty = true;
        }

        private Color ResolveTileColor(int x, int y)
        {
            // Entity overlay takes precedence over base tile color so the dot is
            // visible at a glance.
            if (floor.entities != null)
            {
                for (int i = 0; i < floor.entities.Count; i++)
                {
                    var e = floor.entities[i];
                    if (e.position.x != x || e.position.y != y) continue;
                    switch (e.kind)
                    {
                        case EntityKind.Key:      return ColorKey;
                        case EntityKind.Chest:    return ColorChest;
                        case EntityKind.GoldPile: return ColorGold;
                        case EntityKind.Shrine:   return ColorShrine;
                        case EntityKind.BossGate: return ColorGate;
                    }
                }
            }
            switch (floor.tiles[x, y])
            {
                case TileType.Wall:
                case TileType.Empty:    return ColorWall;
                case TileType.Start:    return ColorStart;
                case TileType.Stairway: return ColorStairway;
                case TileType.Rest:     return ColorRest;
                default:                return ColorFloor;
            }
        }

        private void PaintTile(int gridX, int gridY, Color c)
        {
            int x0 = gridX * tileSize;
            int y0 = gridY * tileSize;
            for (int dx = 0; dx < tileSize; dx++)
                for (int dy = 0; dy < tileSize; dy++)
                    texture.SetPixel(x0 + dx, y0 + dy, c);
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("Minimap");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 610;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Frame — top-right corner.
            var frame = new GameObject("Frame");
            frame.transform.SetParent(go.transform, false);
            var rt = frame.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -180f); // below the HUD top bar
            rt.sizeDelta = new Vector2(220f, 220f);
            var bg = frame.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            // Inner display — small inset so the frame border shows.
            var displayGO = new GameObject("Display");
            displayGO.transform.SetParent(frame.transform, false);
            var drt = displayGO.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(4f, 4f);
            drt.offsetMax = new Vector2(-4f, -4f);
            var raw = displayGO.AddComponent<RawImage>();
            raw.color = Color.white;

            var minimap = go.AddComponent<Minimap>();
            minimap.display = raw;
            minimap.tileSize = 4;
            return go;
        }
    }
}
