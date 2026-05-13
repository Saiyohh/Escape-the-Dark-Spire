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

        public OrbInstance BoundOrb { get; private set; }

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
            if (hoverCollider == null) hoverCollider = GetComponent<Collider2D>();
        }

        public void RenderFilled(
            OrbInstance orb,
            Sprite sprite, Color color,
            int passiveValue, int activeValue, bool showActive)
        {
            BoundOrb = orb;
            ApplyVisuals(sprite, color);

            SetLabel(passiveLabel, passiveContainer, passiveValue, show: passiveValue > 0);

            SetLabel(activeLabel, activeContainer, activeValue, show: showActive && activeValue > 0);

            if (hoverCollider != null) hoverCollider.enabled = true;
        }

        public void RenderEmpty(Sprite emptySprite, Color emptyColor)
        {
            BoundOrb = null;
            ApplyVisuals(emptySprite, emptyColor);
            SetLabel(passiveLabel, passiveContainer, 0, show: false);
            SetLabel(activeLabel,  activeContainer,  0, show: false);

            if (hoverCollider != null) hoverCollider.enabled = false;
        }

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
