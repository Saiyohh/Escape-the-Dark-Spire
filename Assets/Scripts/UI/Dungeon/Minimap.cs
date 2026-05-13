using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class Minimap : MonoBehaviour
    {
        [Header("Display (required — author in scene)")]
        [Tooltip("RawImage that the runtime texture is assigned to. " +
                 "Its RectTransform size on the Canvas IS the on-screen " +
                 "minimap size — set it however you like in the Inspector.")]
        [SerializeField] private RawImage display;

        [Tooltip("Pixels per tile in the painted texture. Higher = chunkier " +
                 "blocks. The RawImage's RectTransform stretches the texture, " +
                 "so this controls internal pixel-art resolution, not on-screen size.")]
        [SerializeField] private int tileSize = 4;

        // Resolved at runtime by DungeonBootstrap via Bind(...). Not authored.
        private FogOfWar fog;
        private PartyToken party;

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
        private bool warnedAboutMissingDisplay;

        // ─── Public API ──────────────────────────────────────────────────────

        public void Bind(GeneratedFloorData floorIn, FogOfWar fogIn, PartyToken partyIn)
        {
            floor = floorIn;
            fog = fogIn;
            party = partyIn;
            if (display == null)
            {
                WarnAboutMissingDisplay();
                return;
            }
            BuildTexture();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            if (display == null) WarnAboutMissingDisplay();
        }

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

        private void WarnAboutMissingDisplay()
        {
            if (warnedAboutMissingDisplay) return;
            warnedAboutMissingDisplay = true;
            Debug.LogWarning(
                "[Minimap] No `display` RawImage assigned. Author the minimap " +
                "in the scene — run " +
                "Tools > DarkSpire > Scenes > Scaffold Minimap into open scene " +
                "for a starting frame, then wire the inner RawImage to the " +
                "Minimap component's Display field.", this);
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

            display.texture = texture;
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
    }
}
