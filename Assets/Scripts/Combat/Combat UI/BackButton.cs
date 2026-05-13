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

        private static BackButton _instance;
        public static BackButton Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<BackButton>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        private object currentOwner;
        private Action currentHandler;

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

            if (Application.isPlaying)
                CombatEvents.OnCombatStart += ForceClose;

            Apply();

        }

        private void OnEnable() => Apply();

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
            CombatEvents.OnCombatStart -= ForceClose;
            if (_instance == this) _instance = null;
        }

        public void ForceClose()
        {
            currentOwner = null;
            currentHandler = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void Bind(object owner, Action onBackPressed)
        {
            currentOwner = owner;
            currentHandler = onBackPressed;
            isHovered = false; // fresh state on rebind
            gameObject.SetActive(true);
            Apply();
        }

        public void Unbind(object owner)
        {
            if (currentOwner != null && owner != null
                && !ReferenceEquals(currentOwner, owner)) return;
            currentOwner = null;
            currentHandler = null;
            gameObject.SetActive(false);
        }

        public bool IsBoundTo(object owner) => ReferenceEquals(currentOwner, owner);

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

        private void HandleClick() => currentHandler?.Invoke();

        private void Apply()
        {
            if (button != null && !button.interactable)
            {
                ApplyColors(disabledBg, disabledFg);
                return;
            }

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
