// SkillCardStyleController.cs
// -----------------------------------------------------------------------------
// Pointer-driven style for skill cards. Single controller manages two
// regions on the card with OPPOSITE flip behaviors:
//
//   Card body (background + name label)
//     • Resting → UI/ButtonNormalBG  + UI/ButtonNormalText
//     • Hover   → UI/ButtonHoverBG   + UI/ButtonHoverText
//     (Identical to ButtonHoverStyleController — the card body matches the
//      action bar visually.)
//
//   Cost badge (background + cost label)
//     • Resting → UI/ButtonHoverBG   + UI/ButtonHoverText
//     • Hover   → UI/ButtonNormalBG  + UI/ButtonNormalText
//     (Inverted — when card body is dark, badge is light, and vice versa.)
//
// Both regions update from the same pointer event, so the card flips as a
// unit. No pressed state — pressing acts like hover.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    [RequireComponent(typeof(Button))]
    public class SkillCardStyleController : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Card Body (standard flip — matches action buttons)")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional outline controller on the body label. When wired, " +
                 "outline color/width swap with the same per-state values as " +
                 "ButtonHoverStyleController. Color comes from UI/ButtonOutlineColor.")]
        [SerializeField] private TMPOutlineController bodyOutline;

        [Header("Cost Badge (inverted flip — opposite of card body)")]
        [SerializeField] private Image costBadgeImage;
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("Optional outline controller on the cost label. Same per-state " +
                 "behavior as bodyOutline.")]
        [SerializeField] private TMPOutlineController costOutline;

        [Header("Outline Widths Per State")]
        [Range(0f, 1f)]
        [SerializeField] private float normalOutlineWidth = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float hoverOutlineWidth  = 0.2f;

        [Header("Disabled (can't afford)")]
        [SerializeField] private Color disabledBg   = new Color(0.30f, 0.30f, 0.30f);
        [SerializeField] private Color disabledText = new Color(0.55f, 0.55f, 0.55f);

        // Fallbacks for when the library / keys aren't present.
        private static readonly Color FallbackNormalBg   = Color.white;
        private static readonly Color FallbackHoverBg    = Color.black;
        private static readonly Color FallbackNormalText = Color.black;
        private static readonly Color FallbackHoverText  = Color.white;

        private Button button;
        private bool isHovered;
        private bool wasInteractable = true;

        private void Awake()
        {
            button = GetComponent<Button>();
            ApplyState();
        }

        private void OnEnable() => ApplyState();

        private void Update()
        {
            if (button == null) return;
            if (button.interactable != wasInteractable)
            {
                wasInteractable = button.interactable;
                if (!button.interactable) isHovered = false;
                ApplyState();
            }
        }

        // ─── Pointer events ──────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData _)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            ApplyState();
        }

        public void OnPointerExit(PointerEventData _)
        {
            isHovered = false;
            ApplyState();
        }

        // ─── State application ──────────────────────────────────────────────

        private void ApplyState()
        {
            if (button != null && !button.interactable)
            {
                // No outline at all in disabled state — keeps the muted look readable.
                ApplyBody(disabledBg, disabledText, new Color(0, 0, 0, 0), 0f);
                ApplyBadge(disabledBg, disabledText, new Color(0, 0, 0, 0), 0f);
                return;
            }

            // Pull all five library colors once per state change so the body
            // and badge flips stay in lockstep.
            Color normalBg     = ColorLibrary.Get("UI", "ButtonNormalBG",   FallbackNormalBg);
            Color hoverBg      = ColorLibrary.Get("UI", "ButtonHoverBG",    FallbackHoverBg);
            Color normalText   = ColorLibrary.Get("UI", "ButtonNormalText", FallbackNormalText);
            Color hoverText    = ColorLibrary.Get("UI", "ButtonHoverText",  FallbackHoverText);
            Color outlineColor = ColorLibrary.Get("UI", "ButtonOutlineColor", Color.black);

            // Outline width pattern matches ButtonHoverStyleController:
            //   resting  → normalOutlineWidth (often 0 = no outline)
            //   hovered  → hoverOutlineWidth
            float widthBody  = isHovered ? hoverOutlineWidth : normalOutlineWidth;
            float widthBadge = isHovered ? hoverOutlineWidth : normalOutlineWidth;

            // The outline COLOR also needs to read against whatever BG the
            // region currently has. Body and badge BGs always invert each
            // other, so an outline color that contrasts the body BG already
            // contrasts the badge text-color region.
            Color outlineForBody  = outlineColor;
            Color outlineForBadge = outlineColor;

            if (isHovered)
            {
                ApplyBody (hoverBg,  hoverText,  outlineForBody,  widthBody);
                ApplyBadge(normalBg, normalText, outlineForBadge, widthBadge);
            }
            else
            {
                ApplyBody (normalBg, normalText, outlineForBody,  widthBody);
                ApplyBadge(hoverBg,  hoverText,  outlineForBadge, widthBadge);
            }
        }

        private void ApplyBody(Color bg, Color text, Color outlineCol, float outlineWidth)
        {
            if (backgroundImage != null) backgroundImage.color = bg;
            if (label != null) label.color = text;
            if (bodyOutline != null) bodyOutline.SetOutline(outlineCol, outlineWidth);
        }

        private void ApplyBadge(Color bg, Color text, Color outlineCol, float outlineWidth)
        {
            if (costBadgeImage != null) costBadgeImage.color = bg;
            if (costLabel != null) costLabel.color = text;
            if (costOutline != null) costOutline.SetOutline(outlineCol, outlineWidth);
        }
    }
}
