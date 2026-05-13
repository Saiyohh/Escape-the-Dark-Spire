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
            Time.timeScale = 1f;

            if (panelRoot != null) panelRoot.SetActive(false);

            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
                overlay.LoadSceneTransition(mainMenuSceneName);
            else
                SceneManager.LoadScene(mainMenuSceneName);
        }

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
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private void Update()
        {
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

        private bool CanOpen()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (suppressInScenes != null)
            {
                for (int i = 0; i < suppressInScenes.Length; i++)
                {
                    if (suppressInScenes[i] == activeScene) return false;
                }
            }

            var ts = TargetingSystem.Instance;
            if (ts != null && ts.IsTargeting) return false;

            return true;
        }

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("PauseMenu");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(go.transform, false);

            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var bgImage = panelGO.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
            bgImage.raycastTarget = true;

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

            var controller = go.AddComponent<PauseMenuController>();
            controller.panelRoot = panelGO;
            controller.resumeButton = resume;
            controller.quitButton   = quit;

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
