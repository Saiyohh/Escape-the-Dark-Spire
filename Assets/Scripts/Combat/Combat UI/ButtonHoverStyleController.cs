using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    [RequireComponent(typeof(Button))]
    public class ButtonHoverStyleController : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler,  IPointerUpHandler
    {
        [Header("Targets")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional icon Image (e.g. back-arrow). Tinted with the same color " +
                 "as the label so it stays readable across states.")]
        [SerializeField] private Image iconImage;
        [Tooltip("Optional. When wired, label outline is driven through SetOutline " +
                 "so each state can swap its outline color/width.")]
        [SerializeField] private TMPOutlineController outline;

        [Header("Style Mode")]
        [Tooltip("When true, normal/hover color keys are swapped. Button rests in " +
                 "the action-button-hover look (white BG, black text) and flips to " +
                 "the action-button-normal look (black BG, white text). Used for " +
                 "back buttons, cost badges, anything that should visually invert " +
                 "the primary action bar.")]
        [SerializeField] private bool invertColors = false;

        [Header("Outline widths per state (color comes from ColorLibrary)")]
        [Range(0f, 1f)]
        [SerializeField] private float normalOutlineWidth  = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float hoverOutlineWidth   = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] private float pressedOutlineWidth = 0.25f;

        [Header("Disabled (serialized — not in library yet)")]
        [SerializeField] private Color disabledBg   = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color disabledText = new Color(0.55f, 0.55f, 0.55f);

        private Button button;
        private bool isHovered;
        private bool isPressed;
        private bool wasInteractable = true;

        // Color-library fallbacks — used if the library is missing the key OR
        // the library asset itself isn't present. Approximates the previous
        // hardcoded defaults so things still look reasonable in that case.
        private static readonly Color FallbackNormalBg     = Color.white;
        private static readonly Color FallbackHoverBg      = Color.black;
        private static readonly Color FallbackPressedBg    = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color FallbackNormalText   = Color.black;
        private static readonly Color FallbackHoverText    = Color.white;
        private static readonly Color FallbackOutline      = Color.black;

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
                if (!button.interactable) { isHovered = false; isPressed = false; }
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
            isPressed = false;
            ApplyState();
        }

        public void OnPointerDown(PointerEventData _)
        {
            if (button != null && !button.interactable) return;
            isPressed = true;
            ApplyState();
        }

        public void OnPointerUp(PointerEventData _)
        {
            isPressed = false;
            ApplyState();
        }

        // ─── State application ──────────────────────────────────────────────

        private void ApplyState()
        {
            if (button != null && !button.interactable)
            {
                Apply(disabledBg, disabledText, new Color(0, 0, 0, 0), 0f);
                return;
            }

            // Pull library colors once per state change.
            Color normalBg     = ColorLibrary.Get("UI", "ButtonNormalBG",   FallbackNormalBg);
            Color hoverBg      = ColorLibrary.Get("UI", "ButtonHoverBG",    FallbackHoverBg);
            Color pressedBg    = ColorLibrary.Get("UI", "ButtonPressedBG",  FallbackPressedBg);
            Color normalText   = ColorLibrary.Get("UI", "ButtonNormalText", FallbackNormalText);
            Color hoverText    = ColorLibrary.Get("UI", "ButtonHoverText", FallbackHoverText);
            Color outlineColor = ColorLibrary.Get("UI", "ButtonOutlineColor", FallbackOutline);

            // Inverted mode swaps normal ↔ hover. (No pressed state when inverted —
            // back buttons / cost badges press through their hover color.)
            if (invertColors)
            {
                if (isPressed || isHovered)
                    Apply(normalBg, normalText, outlineColor, hoverOutlineWidth);
                else
                    Apply(hoverBg, hoverText, new Color(0, 0, 0, 0), normalOutlineWidth);
                return;
            }

            // Standard action-button mode.
            if (isPressed)
                Apply(pressedBg, hoverText, outlineColor, pressedOutlineWidth);
            else if (isHovered)
                Apply(hoverBg, hoverText, outlineColor, hoverOutlineWidth);
            else
                Apply(normalBg, normalText, new Color(0, 0, 0, 0), normalOutlineWidth);
        }

        private void Apply(Color bg, Color text, Color outlineColor, float outlineWidth)
        {
            if (backgroundImage != null) backgroundImage.color = bg;
            if (label != null) label.color = text;
            if (iconImage != null) iconImage.color = text; // icon shares text color so it stays readable
            if (outline != null) outline.SetOutline(outlineColor, outlineWidth);
        }
    }
}
