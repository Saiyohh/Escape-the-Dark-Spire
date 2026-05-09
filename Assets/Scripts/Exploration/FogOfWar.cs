using UnityEngine;

namespace DarkSpire
{
    // Per-tile fog overlay. Each tile gets a black SpriteRenderer whose alpha
    // is driven by:
    //   - inside sight radius (with soft falloff)  -> 0 (fully clear)
    //   - previously revealed but not currently lit -> revealedAlpha (~0.3)
    //   - never seen                                -> 1 (fully obscured)
    //
    // Drawn above floors and entities (sortingOrder 10) so it visually masks
    // entities in unseen tiles for free. Reveal does not require line-of-sight
    // through walls — circular radius only, per the GDD Exploration spec.
    public class FogOfWar : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 10;

        [Tooltip("If true, tiles previously inside the sight radius stay dimmed at " +
                 "revealedAlpha so the player can retrace the path. If false, fog " +
                 "outside the current radius snaps back to fully opaque (pure vision).")]
        [SerializeField] private bool persistRevealed = false;

        [Range(0f, 1f)]
        [Tooltip("Fog alpha over a tile that has been seen but is not currently lit. " +
                 "Only used when persistRevealed = true.")]
        [SerializeField] private float revealedAlpha = 0.3f;

        [Tooltip("Width (in tiles) of the soft falloff band at the sight-radius edge.")]
        [SerializeField] private float falloffWidth = 1.0f;

        private GeneratedFloorData floor;
        private float sightRadius;
        private SpriteRenderer[,] fogTiles;
        private bool[,] revealed;
        private Sprite whiteSprite;
        private Transform parent;

        public void Bind(GeneratedFloorData floor, float sightRadius)
        {
            this.floor = floor;
            this.sightRadius = Mathf.Max(0.5f, sightRadius);
            BuildOverlay();
        }

        // Read-only accessors for downstream consumers (e.g. Minimap) that want
        // to know what's been seen without holding a ref to the raw bool[,].
        public Vector2Int GridSize =>
            floor != null ? floor.gridSize : Vector2Int.zero;

        public bool IsRevealed(Vector2Int p) => IsRevealed(p.x, p.y);

        public bool IsRevealed(int x, int y)
        {
            if (revealed == null) return false;
            int w = revealed.GetLength(0), h = revealed.GetLength(1);
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            return revealed[x, y];
        }

        public void UpdateForPartyAt(Vector2Int partyTile)
        {
            if (fogTiles == null || floor == null) return;
            int w = floor.gridSize.x, h = floor.gridSize.y;

            float inner = Mathf.Max(0f, sightRadius - falloffWidth);

            // Bound the iteration to a square around the party — saves work on
            // big grids (45x45) without changing the visible result.
            int margin = Mathf.CeilToInt(sightRadius) + 1;
            int xMin = Mathf.Max(0, partyTile.x - margin);
            int xMax = Mathf.Min(w - 1, partyTile.x + margin);
            int yMin = Mathf.Max(0, partyTile.y - margin);
            int yMax = Mathf.Min(h - 1, partyTile.y + margin);

            // First, paint tiles outside the active update box back to their
            // resting alpha (revealed or unseen) — only needed once on first
            // call when the box is small. Cheap to always do; bail out if not.
            // Optimization deferred: most floors will iterate the full grid
            // once on first reveal, then only the local box thereafter.

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float alpha;
                    bool inBox = x >= xMin && x <= xMax && y >= yMin && y <= yMax;
                    if (inBox)
                    {
                        float dx = x - partyTile.x;
                        float dy = y - partyTile.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);

                        // In pure-vision mode, tiles inside the radius lerp from
                        // 0 -> 1 across the falloff band. In persistRevealed mode,
                        // they lerp 0 -> revealedAlpha and stay dimmed afterward.
                        float outsideAlpha = persistRevealed && revealed[x, y] ? revealedAlpha : 1f;

                        if (d <= inner)
                        {
                            alpha = 0f;
                            revealed[x, y] = true;
                        }
                        else if (d <= sightRadius)
                        {
                            float t = falloffWidth > 0.0001f
                                ? (d - inner) / falloffWidth
                                : 1f;
                            alpha = Mathf.Lerp(0f, outsideAlpha, t);
                            revealed[x, y] = true;
                        }
                        else
                        {
                            alpha = persistRevealed && revealed[x, y] ? revealedAlpha : 1f;
                        }
                    }
                    else
                    {
                        alpha = persistRevealed && revealed[x, y] ? revealedAlpha : 1f;
                    }
                    var sr = fogTiles[x, y];
                    if (sr != null)
                    {
                        var c = sr.color;
                        if (!Mathf.Approximately(c.a, alpha))
                        {
                            c.a = alpha;
                            sr.color = c;
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            if (parent != null) DestroyImmediateOrPlay(parent.gameObject);
            parent = null;
            fogTiles = null;
            revealed = null;
        }

        // ------------------------------------------------------------------

        private void BuildOverlay()
        {
            if (parent != null) DestroyImmediateOrPlay(parent.gameObject);
            EnsureWhiteSprite();

            var go = new GameObject("FogTiles");
            go.transform.SetParent(transform, worldPositionStays: false);
            parent = go.transform;

            int w = floor.gridSize.x, h = floor.gridSize.y;
            fogTiles = new SpriteRenderer[w, h];
            revealed = new bool[w, h];

            var black = new Color(0f, 0f, 0f, 1f);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var t = new GameObject($"F_{x}_{y}");
                    t.transform.SetParent(parent, false);
                    t.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    var sr = t.AddComponent<SpriteRenderer>();
                    sr.sprite = whiteSprite;
                    sr.sortingOrder = sortingOrder;
                    sr.color = black;
                    fogTiles[x, y] = sr;
                }
            }
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
            whiteSprite.name = "RuntimeFog";
        }

        private static void DestroyImmediateOrPlay(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
