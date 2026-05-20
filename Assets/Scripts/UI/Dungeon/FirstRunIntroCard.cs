// FirstRunIntroCard.cs
// -----------------------------------------------------------------------------
// One-time onboarding modal shown the first time the player enters the dungeon
// on this install. Covers controls, the goal, what items to look for, and what
// danger looks like — directly answering the playtester confusion that
// motivated this clarity pass.
//
// Gating: PlayerPrefs key "DarkSpire.HasSeenIntro" (1 = seen). Survives game
// restarts and run resets — RunStateHolder.ClearForNewRun would re-show on
// every "Descend" which is annoying once you know the game.
//
// Mirrors RestMenu / IconLegendModal — Resources/FirstRunIntro prefab wins,
// otherwise a Canvas is built at runtime.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkSpire
{
    public class FirstRunIntroCard : MonoBehaviour
    {
        public const string PrefsKey = "DarkSpire.HasSeenIntro";

        public static FirstRunIntroCard Instance { get; private set; }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button gotItButton;

        private float prevTimeScale = 1f;
        private DungeonInteractor cachedInteractor;

        // ─── Public API ──────────────────────────────────────────────────────

        public static bool HasSeenIntro => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        /// <summary>Force-show the card regardless of the PlayerPrefs gate.
        /// Used by the legend modal's "show intro again" entry point.</summary>
        public static void ShowAlways()
        {
            var card = Instance ?? GetOrCreate();
            card?.Open();
        }

        /// <summary>Show only if the player hasn't seen the card before.
        /// Called from DungeonBootstrap on first dungeon entry.</summary>
        public static void ShowIfFirstRun()
        {
            if (HasSeenIntro) return;
            var card = Instance ?? GetOrCreate();
            card?.Open();
        }

        public static FirstRunIntroCard GetOrCreate()
        {
            if (Instance != null) return Instance;
            var lib = ClarityUIPrefabLibrary.Instance;
            var prefab = lib != null ? lib.firstRunIntro : null;
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<FirstRunIntroCard>();
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            cachedInteractor = FindAnyObjectByType<DungeonInteractor>(FindObjectsInactive.Include);
            if (cachedInteractor != null) cachedInteractor.ModalOpen = true;
            Debug.Log("[FirstRunIntroCard] Opened.");
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            Time.timeScale = prevTimeScale;
            if (cachedInteractor != null) cachedInteractor.ModalOpen = false;

            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[FirstRunIntroCard] Closed (HasSeenIntro = 1).");
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (gotItButton != null)
            {
                gotItButton.onClick.AddListener(() =>
                {
                    Debug.Log("[FirstRunIntroCard] Got it clicked.");
                    Close();
                });
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("FirstRunIntroCard");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 870; // above IconLegendModal (860)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Backdrop.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(go.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = backdrop.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);

            // Panel.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(backdrop.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 600f);
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 28, 28);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            BuildLabel(panel.transform,
                "Welcome to Dark Spire",
                36, FontStyles.Bold, new Color(1f, 0.92f, 0.45f),
                TextAlignmentOptions.Center, preferredHeight: 56f);

            BuildBody(panel.transform,
                "<b>Move</b>  WASD / Arrows\n" +
                "<b>Interact</b>  Press <b>E</b> next to a chest, shrine, or gate.");

            BuildBody(panel.transform,
                "<b>Your goal:</b>  Find the boss, defeat it, then step on the " +
                "<color=#A080FF>purple stairway</color> to escape.");

            BuildBody(panel.transform,
                "<b>Pick these up:</b>\n" +
                "  <color=#FFE07A>Keys</color> — needed to open the boss gate\n" +
                "  <color=#FFC400>Gold piles</color> — currency\n" +
                "  <color=#80FF80>Campsites</color> — press <b>E</b> to heal and revive (one use)");

            BuildBody(panel.transform,
                "<b>Watch out:</b>  Monsters patrol the halls. When one " +
                "<b>spots</b> you a <color=#FFD700>!</color> pops above its head, " +
                "then it chases. Bumping into a monster starts a fight.");

            BuildBody(panel.transform,
                "<i>Stuck? Click the <b>?</b> button in the top-right at any " +
                "time to bring up the icon guide.</i>");

            // Got it button.
            var btn = BuildButton(panel.transform, "Got it");

            var manager = go.AddComponent<FirstRunIntroCard>();
            manager.panelRoot   = backdrop;
            manager.gotItButton = btn;

            backdrop.SetActive(false);
            return go;
        }

        private static TMP_Text BuildLabel(Transform parent, string text, float size,
                                          FontStyles style, Color color,
                                          TextAlignmentOptions align,
                                          float preferredHeight)
        {
            var goLabel = new GameObject("Label");
            goLabel.transform.SetParent(parent, false);
            goLabel.AddComponent<RectTransform>();
            var tmp = goLabel.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.raycastTarget = false;
            UIFonts.Apply(tmp);
            if (preferredHeight > 0f)
            {
                var le = goLabel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return tmp;
        }

        private static void BuildBody(Transform parent, string text)
        {
            BuildLabel(parent, text, 20, FontStyles.Normal, Color.white,
                       TextAlignmentOptions.TopLeft, preferredHeight: 0f);
        }

        private static Button BuildButton(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 56f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.28f, 1f);
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;
            var colors = btn.colors;
            colors.normalColor      = new Color(0.22f, 0.22f, 0.28f, 1f);
            colors.highlightedColor = new Color(0.36f, 0.36f, 0.46f, 1f);
            colors.pressedColor     = new Color(0.50f, 0.50f, 0.62f, 1f);
            colors.selectedColor    = new Color(0.36f, 0.36f, 0.46f, 1f);
            colors.disabledColor    = new Color(0.16f, 0.16f, 0.20f, 0.6f);
            btn.colors = colors;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            UIFonts.Apply(tmp);

            return btn;
        }
    }
}
