// OrbSlotView.cs
// -----------------------------------------------------------------------------
// Per-slot view component for the orb tray. Sits on the slotPrefab that
// OrbSlotsUI instantiates. Holds serialized references to the slot's renderer
// (UI Image OR world-space SpriteRenderer — whichever the prefab uses) and
// to two TMP_Text labels for the canonical "passive number" + "active
// number" display per the GDD's HUD section.
//
// OrbSlotsUI computes the values via OrbManager.GetPassiveDisplayValue /
// GetActiveDisplayValue and calls the per-slot Render methods. This component
// owns ONLY presentation — no game logic.
//
// Default visibility (per GDD):
//   • Passive number — always shown when an orb is in the slot, hidden if 0.
//   • Active number  — shown only when ShouldShowActiveNumberOnIcon (Dark);
//                      other orbs surface the active number via the (future)
//                      hover tooltip.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class OrbSlotView : MonoBehaviour
    {
        [Header("Renderer (one of these — wire whichever the prefab uses)")]
        [SerializeField] private Image image;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Number labels")]
        [Tooltip("Passive value — always visible when an orb is present and the " +
                 "value > 0. Conventionally pinned to the bottom-right of the icon.")]
        [SerializeField] private TMP_Text passiveLabel;

        [Tooltip("Active value (Evoke). Hidden by default; shown when the orb's " +
                 "type wants both numbers always visible (Dark). Other orbs " +
                 "surface the active value via the hover tooltip.")]
        [SerializeField] private TMP_Text activeLabel;

        [Header("Optional containers (toggled together with their label)")]
        [Tooltip("If set, this GameObject is enabled/disabled along with the " +
                 "passive label — useful when the label sits on a colored pill.")]
        [SerializeField] private GameObject passiveContainer;
        [Tooltip("If set, toggled with the active label.")]
        [SerializeField] private GameObject activeContainer;

        [Header("Hover detection")]
        [Tooltip("Collider2D used by OrbTooltipTrigger for cursor hit-tests. " +
                 "Auto-resolved on Awake via GetComponent if left null. The " +
                 "collider is disabled when the slot is empty (no orb to " +
                 "describe) and re-enabled on RenderFilled, so empty slots " +
                 "don't intercept the cursor or fire empty tooltips.")]
        [SerializeField] private Collider2D hoverCollider;

        [Header("Animation target")]
        [Tooltip("Optional child Transform used by OrbSlotsUI's channel-in " +
                 "grow + passive pulse animations. Scaling this child instead " +
                 "of the slot root means the BoxCollider2D on the root keeps " +
                 "its authored size during the animation. If null, falls back " +
                 "to spriteRenderer's transform, then image's transform, then " +
                 "this transform (legacy prefabs).")]
        [SerializeField] private Transform visualTransform;

        /// <summary>The orb instance most recently rendered into this slot,
        /// or null when the slot is empty. Read by OrbTooltipTrigger to
        /// populate the hover tooltip with the orb's passive/evoke text.</summary>
        public OrbInstance BoundOrb { get; private set; }

        /// <summary>Transform that animations should scale — the visible
        /// orb sprite, NOT the slot root (which carries the collider used
        /// for hover detection). See the visualTransform field above for
        /// fallback order.</summary>
        public Transform VisualTransform
        {
            get
            {
                if (visualTransform != null) return visualTransform;
                if (spriteRenderer != null) return spriteRenderer.transform;
                if (image != null) return image.transform;
                return transform;
            }
        }

        private void Awake()
        {
            // Cache the collider once. Field stays optional in the inspector
            // so legacy prefabs without one still work (collider toggling
            // becomes a no-op for them).
            if (hoverCollider == null) hoverCollider = GetComponent<Collider2D>();
        }

        // ─── Public API used by OrbSlotsUI ────────────────────────────────────

        public void RenderFilled(
            OrbInstance orb,
            Sprite sprite, Color color,
            int passiveValue, int activeValue, bool showActive)
        {
            BoundOrb = orb;
            ApplyVisuals(sprite, color);

            // Passive: always show when value > 0 (per GDD's "minimum display value of 1").
            SetLabel(passiveLabel, passiveContainer, passiveValue, show: passiveValue > 0);

            // Active: show only when caller says so AND value > 0.
            SetLabel(activeLabel, activeContainer, activeValue, show: showActive && activeValue > 0);

            // Re-enable cursor hit-testing for the orb tooltip.
            if (hoverCollider != null) hoverCollider.enabled = true;
        }

        public void RenderEmpty(Sprite emptySprite, Color emptyColor)
        {
            BoundOrb = null;
            ApplyVisuals(emptySprite, emptyColor);
            SetLabel(passiveLabel, passiveContainer, 0, show: false);
            SetLabel(activeLabel,  activeContainer,  0, show: false);

            // Empty slot: drop hit-testing so the cursor passes through and
            // OrbTooltipTrigger never fires for a no-orb hover. Combined with
            // the BoundOrb == null guard in OrbTooltipTrigger.BuildContent,
            // this is belt + suspenders.
            if (hoverCollider != null) hoverCollider.enabled = false;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void ApplyVisuals(Sprite sprite, Color color)
        {
            if (image != null)
            {
                image.sprite = sprite;
                image.color  = color;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.color  = color;
            }
        }

        private static void SetLabel(TMP_Text label, GameObject container, int value, bool show)
        {
            if (label != null)
            {
                if (show) label.text = value.ToString();
                label.gameObject.SetActive(show);
            }
            if (container != null) container.SetActive(show);
        }
    }
}
