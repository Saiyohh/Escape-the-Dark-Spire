using System;
using System.Collections;
using UnityEngine;

namespace DarkSpire
{
    // The party's single map-token. Owns the grid position and the lerp
    // animation between tiles. Movement is gated by the active floor's wall
    // map and an "is currently lerping" flag — GridMovement asks via
    // TryMove(dir) and respects the bool result.
    public class PartyToken : MonoBehaviour
    {
        [SerializeField] private float lerpDuration = 0.08f;
        [SerializeField] private int sortingOrder = 7;

        [Tooltip("Optional per-instance override. Leave null to use " +
                 "MapEntitySpriteLibrary.partyToken (singleton).")]
        [SerializeField] private Sprite spriteOverride;

        public Vector2Int GridPos { get; private set; }
        public bool IsMoving { get; private set; }

        public event Action<Vector2Int, Vector2Int> OnMoved;   // (from, to)
        public event Action<Vector2Int> OnPlaced;

        private GeneratedFloorData floor;
        private SpriteRenderer sr;
        private Sprite whiteSprite;
        private Coroutine lerpCo;

        private void Awake()
        {
            EnsureRenderer();
        }

        public void Bind(GeneratedFloorData floor)
        {
            this.floor = floor;
        }

        public void Place(Vector2Int tile)
        {
            if (lerpCo != null) StopCoroutine(lerpCo);
            lerpCo = null;
            IsMoving = false;
            GridPos = tile;
            transform.position = TileToWorld(tile);
            OnPlaced?.Invoke(tile);
        }

        public bool TryMove(Vector2Int dir)
        {
            if (IsMoving) return false;
            if (floor == null) return false;
            if (dir == Vector2Int.zero) return false;

            // Combat is loading (cover-up sliding, scene about to swap) —
            // refuse further input so the player can't take another step
            // before the swap completes. Without this, holding a direction
            // would walk past the triggering monster's tile during the
            // ~0.4s slide-up.
            if (SceneFlow.IsCombatLoadInFlight) return false;

            var target = GridPos + dir;
            if (!floor.InBounds(target)) return false;
            if (!IsPassableForParty(target)) return false;

            var from = GridPos;
            GridPos = target;
            if (lerpCo != null) StopCoroutine(lerpCo);
            lerpCo = StartCoroutine(LerpTo(from, target));
            return true;
        }

        private bool IsPassableForParty(Vector2Int p)
        {
            // Wall + Empty (the void outside walls) both block movement.
            if (floor.tiles[p.x, p.y].IsImpassable()) return false;

            // Locked Boss Gate (and any future IBlocker) refuses passage.
            var registry = DungeonRegistry.Instance;
            if (registry != null && registry.IsBlocked(p, out var reason))
            {
                if (!string.IsNullOrEmpty(reason))
                    Debug.Log($"[PartyToken] Blocked: {reason}");
                return false;
            }
            return true;
        }

        private IEnumerator LerpTo(Vector2Int from, Vector2Int to)
        {
            IsMoving = true;
            var startWorld = transform.position;
            var endWorld = TileToWorld(to);
            float t = 0f;
            while (t < lerpDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(startWorld, endWorld, Mathf.Clamp01(t / lerpDuration));
                yield return null;
            }
            transform.position = endWorld;
            IsMoving = false;
            OnMoved?.Invoke(from, to);
        }

        public static Vector3 TileToWorld(Vector2Int tile)
            => new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);

        private void EnsureRenderer()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;

            // Resolution order:
            //   1. spriteOverride (per-instance authored)
            //   2. MapEntitySpriteLibrary.partyToken (singleton)
            //   3. runtime white square (debug placeholder)
            Sprite resolved = spriteOverride;
            if (resolved == null)
            {
                var lib = MapEntitySpriteLibrary.Instance;
                if (lib != null) resolved = lib.partyToken;
            }

            if (resolved != null)
            {
                sr.sprite = resolved;
                sr.color = Color.white;
            }
            else
            {
                // Placeholder: white square + cyan tint so it's obviously a
                // debug glyph. Authored sprite replaces both branches.
                sr.color = new Color(0.85f, 0.95f, 1.0f, 1f);
                if (sr.sprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                    tex.filterMode = FilterMode.Point;
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    whiteSprite = Sprite.Create(
                        tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
                    whiteSprite.name = "RuntimePartyToken";
                    sr.sprite = whiteSprite;
                    transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                }
            }
        }
    }
}
