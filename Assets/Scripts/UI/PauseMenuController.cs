// PauseMenuController.cs
// -----------------------------------------------------------------------------
// Persistent pause menu — Esc key toggles a small modal panel with Resume +
// Quit buttons. One singleton across the run; created on first call (typically
// from MainMenuController.Start) and survives every subsequent scene swap via
// DontDestroyOnLoad.
//
// While open: Time.timeScale = 0 (gameplay coroutines halt; UI animations that
// use Time.unscaledDeltaTime keep working — including the SceneTransitionOverlay
// fade for the Quit-to-Menu route).
//
// Esc handling cooperates with TargetingSystem: when the player is mid-targeting
// in combat, TargetingSystem owns Esc to cancel targeting and the pause menu
// suppresses its own Esc handler that frame. After targeting clears, Esc
// resumes normal pause-toggle behavior.
//
// Future: a sibling TabMenuController (Tab key, no Time.timeScale freeze, full-
// screen party/inventory view) can mirror this class's structure — singleton
// access, runtime-built canvas, key-toggle Update loop, scene-transition-aware
// Quit. Both would coexist via a small shared "modal stack" rule (only one
// open at a time, dismissed top-to-bottom on Esc).
//
// Authored prefab path: Resources/PauseMenu (Canvas + Panel + Button column).
// If absent, GetOrCreate falls back to a runtime-built canvas with sensible
// styling so you can verify the loop end-to-end before art lands.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DarkSpire
{
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("References")]
        [Tooltip("Toggled active on Open / inactive on Close. Defaults to this " +
                 "GameObject if null. Self-rooted panels should be authored " +
                 "INACTIVE to avoid the SetActive-during-Awake race.")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        [Header("Behavior")]
        [Tooltip("Set Time.timeScale = 0 while open and restore to 1 on close. " +
                 "Disable for menus that should run alongside gameplay (e.g., " +
                 "the future tab/party menu).")]
        [SerializeField] private bool pauseTimeScale = true;

        [Tooltip("Scene name loaded when Quit is pressed. Routes through " +
                 "SceneTransitionOverlay so the slide-up cover plays.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("Scenes where the pause menu should NOT respond to Esc. The " +
                 "main menu has its own escape behavior; PartySelect is pre-game.")]
        [SerializeField] private string[] suppressInScenes = { "MainMenu", "PartySelect" };

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        // ─── Public API ──────────────────────────────────────────────────────

        public static PauseMenuController GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>("PauseMenu");
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<PauseMenuController>();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (!CanOpen()) return;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (pauseTimeScale) Time.timeScale = 0f;
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (pauseTimeScale) Time.timeScale = 1f;
        }

        public void QuitToMainMenu()
        {
            // Always restore time before swapping — the destination scene
            // shouldn't load with a frozen timescale even if we crashed mid-pause.
            Time.timeScale = 1f;

            if (panelRoot != null) panelRoot.SetActive(false);

            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
                overlay.LoadSceneTransition(mainMenuSceneName);
            else
                SceneManager.LoadScene(mainMenuSceneName);
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (panelRoot == null) panelRoot = gameObject;

            // NOTE: don't SetActive(false) here when panelRoot == gameObject
            // (would deactivate ourselves before Awake completes). Authoring
            // requirement: the panel root child must start INACTIVE in the
            // prefab; the runtime fallback handles this explicitly below.
            if (panelRoot != gameObject) panelRoot.SetActive(false);

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Close);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitToMainMenu);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Defensive: a paused scene that destroys the menu shouldn't leave
            // timescale frozen.
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private void Update()
        {
            // Only the Esc-key toggle lives on Update — coroutines aren't a
            // good fit for an arbitrarily long pause window.
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb[Key.Escape].wasPressedThisFrame) return;

            if (IsOpen)
            {
                Close();
                return;
            }
            if (CanOpen()) Open();
        }

        // ─── Open gating ─────────────────────────────────────────────────────

        private bool CanOpen()
        {
            // Suppress in scenes that own their own Esc behavior (main menu's
            // own quit button row, party-select navigation, etc.).
            string activeScene = SceneManager.GetActiveScene().name;
            if (suppressInScenes != null)
            {
                for (int i = 0; i < suppressInScenes.Length; i++)
                {
                    if (suppressInScenes[i] == activeScene) return false;
                }
            }

            // Yield to TargetingSystem when it's actively asking the player to
            // pick a target — Esc cancels targeting in that mode and we don't
            // want a double-fire.
            var ts = TargetingSystem.Instance;
            if (ts != null && ts.IsTargeting) return false;

            return true;
        }

        // ─── Runtime fallback (no Resources/PauseMenu prefab authored) ───────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("PauseMenu");

            // Canvas: screen-space overlay, sortingOrder below the scene
            // transition (1000) so the fade still covers the menu, but above
            // every other gameplay UI.
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // Panel root — toggled active on Open. Authored INACTIVE on this
            // separate child GameObject so toggling doesn't deactivate the
            // controller itself (sidesteps the SetActive-during-Awake trap
            // we hit on SkillSubmenu / BackButton earlier).
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(go.transform, false);

            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // Dim backdrop so the gameplay underneath reads as paused.
            var bgImage = panelGO.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
            bgImage.raycastTarget = true;

            // Button column centered on the screen.
            var col = new GameObject("ButtonColumn");
            col.transform.SetParent(panelGO.transform, false);
            var colRT = col.AddComponent<RectTransform>();
            colRT.anchorMin = new Vector2(0.5f, 0.5f);
            colRT.anchorMax = new Vector2(0.5f, 0.5f);
            colRT.pivot = new Vector2(0.5f, 0.5f);
            colRT.sizeDelta = new Vector2(360f, 240f);
            colRT.anchoredPosition = Vector2.zero;

            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            var resume = CreateRuntimeButton(col.transform, "Resume");
            var quit   = CreateRuntimeButton(col.transform, "Quit to Menu");

            // Title above the buttons.
            var title = new GameObject("Title");
            title.transform.SetParent(panelGO.transform, false);
            var titleRT = title.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.5f);
            titleRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleRT.pivot = new Vector2(0.5f, 0.5f);
            titleRT.sizeDelta = new Vector2(400f, 80f);
            titleRT.anchoredPosition = new Vector2(0f, 180f);

            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Paused";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 64f;
            titleText.color = Color.white;

            // Wire the controller LAST so its Awake sees the panelRoot ref.
            var controller = go.AddComponent<PauseMenuController>();
            controller.panelRoot = panelGO;
            controller.resumeButton = resume;
            controller.quitButton   = quit;

            // Authored-inactive state for the panel — kept consistent with
            // the prefab guidance.
            panelGO.SetActive(false);

            return go;
        }

        private static Button CreateRuntimeButton(Transform parent, string label)
        {
            var go = new GameObject(label.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 64);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor      = new Color(0.10f, 0.10f, 0.12f, 1f);
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.pressedColor     = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.selectedColor    = new Color(0.20f, 0.20f, 0.22f, 1f);
            btn.colors = colors;

            // Label uses TMP so it picks up the project's TMP defaults if any.
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28f;
            tmp.color = Color.white;

            return btn;
        }
    }
}
