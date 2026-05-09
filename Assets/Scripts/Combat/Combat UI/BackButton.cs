// BackButton.cs
// -----------------------------------------------------------------------------
// Singleton back / dismiss button. ONE instance lives on the Combat canvas
// (typically inactive at scene start). Submenus claim it on open and release
// it on close — no per-submenu back button needed.
//
//   public void Open()
//   {
//       panelRoot.SetActive(true);
//       BackButton.Instance?.Bind(this, Close);
//   }
//
//   public void Close()
//   {
//       BackButton.Instance?.Unbind(this);
//       panelRoot.SetActive(false);
//   }
//
// Bind activates the button and subscribes the close action; Unbind clears
// the handler and deactivates. An owner reference guards against cross-
// talk — if submenu A binds, then B binds, A's later Unbind is rejected
// because B owns the button now.
//
// Self-contained styling: matches action buttons (resting black, hover white)
// by reading UI/Button* keys from ColorLibrary. Icon tints with the label
// color for readability across states.
// -----------------------------------------------------------------------------
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    [ExecuteAlways]
    [RequireComponent(typeof(Button))]
    public class BackButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visuals")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label;

        [Header("Disabled (optional)")]
        [SerializeField] private Color disabledBg = new Color(0.30f, 0.30f, 0.30f);
        [SerializeField] private Color disabledFg = new Color(0.55f, 0.55f, 0.55f);

        // ── Singleton access (lazy — works even if GO starts inactive) ─────
        private static BackButton _instance;
        public static BackButton Instance
        {
            get
            {
                if (_instance != null) return _instance;
                // FindAnyObjectByType replaces the now-deprecated
                // FindFirstObjectByType — order doesn't matter for a singleton.
                _instance = FindAnyObjectByType<BackButton>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        // ── Binding state ───────────────────────────────────────────────────
        private object currentOwner;
        private Action currentHandler;

        // Library fallbacks — match ButtonHoverStyleController.
        private static readonly Color FallbackNormalBg   = Color.white;
        private static readonly Color FallbackHoverBg    = Color.black;
        private static readonly Color FallbackNormalText = Color.black;
        private static readonly Color FallbackHoverText  = Color.white;

        private Button button;
        private bool isHovered;
        private bool wasInteractable = true;

        private void Awake()
        {
            if (_instance == null) _instance = this;

            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }

            // Subscribe so subsequent OnCombatStarts always force-close.
            if (Application.isPlaying)
                CombatEvents.OnCombatStart += ForceClose;

            Apply();

            // Initial closed state is owned by the scene/prefab authoring OR
            // CombatUIBootstrap.Start (which runs after every Awake). We can't
            // SetActive(false) here without breaking the first Bind: if the
            // GameObject was inactive at scene init, Awake fires for the first
            // time inside Bind's SetActive(true). A SetActive(false) inside
            // that first Awake overrides Bind's activation and the button
            // never appears.
        }

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        // Refresh in edit mode whenever a serialized field changes — e.g.
        // the user wires backgroundImage / iconImage in the inspector.
        private void OnValidate() => Apply();
#endif

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
            CombatEvents.OnCombatStart -= ForceClose;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Drop any current binding and hide the button. Called by
        /// CombatUIBootstrap on combat start and by the auto-subscription on
        /// OnCombatStart wired in Awake. Safe to call from anywhere.
        /// </summary>
        public void ForceClose()
        {
            currentOwner = null;
            currentHandler = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // ─── Bind / Unbind ───────────────────────────────────────────────────

        /// <summary>
        /// Submenu calls this on open: activates the button and routes the
        /// click to the supplied action. Pass `this` as owner so subsequent
        /// Unbind calls from a different submenu are correctly rejected.
        /// </summary>
        public void Bind(object owner, Action onBackPressed)
        {
            currentOwner = owner;
            currentHandler = onBackPressed;
            isHovered = false; // fresh state on rebind
            gameObject.SetActive(true);
            Apply();
        }

        /// <summary>
        /// Submenu calls this on close. Anti-clobber rule: if a DIFFERENT
        /// submenu currently owns the button (Submenu A binds, then B binds,
        /// then A's Close runs late), reject. But when there's no current
        /// owner — e.g. the very first scene-load Close before anyone has
        /// ever Bound — the call is idempotent and proceeds to ensure the
        /// button is hidden.
        /// </summary>
        public void Unbind(object owner)
        {
            if (currentOwner != null && owner != null
                && !ReferenceEquals(currentOwner, owner)) return;
            currentOwner = null;
            currentHandler = null;
            gameObject.SetActive(false);
        }

        /// <summary>True if this button is currently bound to <paramref name="owner"/>.</summary>
        public bool IsBoundTo(object owner) => ReferenceEquals(currentOwner, owner);

        // ─── Pointer events ──────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData _)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData _)
        {
            isHovered = false;
            Apply();
        }

        private void Update()
        {
            if (button == null) return;
            if (button.interactable != wasInteractable)
            {
                wasInteractable = button.interactable;
                if (!button.interactable) isHovered = false;
                Apply();
            }
        }

        // ─── Click → bound handler ──────────────────────────────────────────

        private void HandleClick() => currentHandler?.Invoke();

        // ─── Style ───────────────────────────────────────────────────────────

        private void Apply()
        {
            if (button != null && !button.interactable)
            {
                ApplyColors(disabledBg, disabledFg);
                return;
            }

            // Same direction as action buttons:
            //   Resting → ButtonNormalBG (black) + ButtonNormalText (white icon)
            //   Hover   → ButtonHoverBG  (white) + ButtonHoverText  (black icon)
            Color bg, fg;
            if (isHovered)
            {
                bg = ColorLibrary.Get("UI", "ButtonHoverBG",   FallbackHoverBg);
                fg = ColorLibrary.Get("UI", "ButtonHoverText", FallbackHoverText);
            }
            else
            {
                bg = ColorLibrary.Get("UI", "ButtonNormalBG",   FallbackNormalBg);
                fg = ColorLibrary.Get("UI", "ButtonNormalText", FallbackNormalText);
            }
            ApplyColors(bg, fg);
        }

        /// <summary>
        /// Dumps the current state to the console so we can tell whether
        /// the wrong colors come from a missing library lookup, swapped
        /// image refs, or a non-tintable source sprite.
        /// </summary>
        [ContextMenu("Diagnose Colors")]
        private void DiagnoseColors()
        {
            bool libExists = ColorLibrary.Instance != null;
            bool hoverBgExists  = ColorLibrary.TryGet("UI", "ButtonHoverBG",  out var hoverBg);
            bool hoverTxExists  = ColorLibrary.TryGet("UI", "ButtonHoverText", out var hoverTx);
            bool normalBgExists = ColorLibrary.TryGet("UI", "ButtonNormalBG",  out var normalBg);
            bool normalTxExists = ColorLibrary.TryGet("UI", "ButtonNormalText", out var normalTx);

            string msg = "[BackButton] Diagnosis\n" +
                $"  ColorLibrary loaded? {libExists}\n" +
                $"  isHovered={isHovered}, button.interactable={(button != null && button.interactable)}\n" +
                $"  UI/ButtonHoverBG   in lib? {hoverBgExists}  value={hoverBg}\n" +
                $"  UI/ButtonHoverText in lib? {hoverTxExists}  value={hoverTx}\n" +
                $"  UI/ButtonNormalBG  in lib? {normalBgExists} value={normalBg}\n" +
                $"  UI/ButtonNormalText in lib? {normalTxExists} value={normalTx}\n" +
                $"  Resting expects bg=ButtonHoverBG (should be WHITE) + fg=ButtonHoverText (should be BLACK).\n" +
                $"  backgroundImage wired? {backgroundImage != null} " +
                    (backgroundImage != null ? $"GO='{backgroundImage.name}' currentColor={backgroundImage.color}" : "") +
                "\n" +
                $"  iconImage wired? {iconImage != null} " +
                    (iconImage != null ? $"GO='{iconImage.name}' currentColor={iconImage.color}" : "");
            Debug.Log(msg, this);
        }

        private void ApplyColors(Color bg, Color fg)
        {
            if (backgroundImage != null) backgroundImage.color = bg;
            if (iconImage != null)       iconImage.color = fg;
            if (label != null)           label.color = fg;
        }
    }
}
