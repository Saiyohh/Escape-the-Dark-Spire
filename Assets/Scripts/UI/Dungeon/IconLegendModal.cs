// IconLegendModal.cs
// -----------------------------------------------------------------------------
// Modal overlay that lists every dungeon icon with its name + description.
// Triggered from the FloorHUD's "?" button. Driven by MapTooltipCatalog so
// every entry lines up with the per-icon hover tooltips and the first-run
// intro card.
//
// Mirrors the RestMenu runtime-fallback pattern — Resources/IconLegend prefab
// wins, otherwise a Canvas is built at runtime. Modal sets
// DungeonInteractor.ModalOpen so E doesn't fire interactions behind the panel,
// and freezes Time.timeScale to pause monster patrols while open.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkSpire
{
    public class IconLegendModal : MonoBehaviour
    {
        public static IconLegendModal Instance { get; private set; }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button showIntroButton;

        private float prevTimeScale = 1f;
        private DungeonInteractor cachedInteractor;
        private bool listBuilt;

        // ─── Public API ──────────────────────────────────────────────────────

        public static IconLegendModal GetOrCreate()
        {
            if (Instance != null) return Instance;
            var lib = ClarityUIPrefabLibrary.Instance;
            var prefab = lib != null ? lib.iconLegend : null;
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<IconLegendModal>();
        }

        public void Open()
        {
            if (!listBuilt) BuildList();
            if (panelRoot != null) panelRoot.SetActive(true);

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            cachedInteractor = FindAnyObjectByType<DungeonInteractor>(FindObjectsInactive.Include);
            if (cachedInteractor != null) cachedInteractor.ModalOpen = true;
            Debug.Log("[IconLegendModal] Opened.");
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            Time.timeScale = prevTimeScale;
            if (cachedInteractor != null) cachedInteractor.ModalOpen = false;
            Debug.Log("[IconLegendModal] Closed.");
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (panelRoot != null) panelRoot.SetActive(false);
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() =>
                {
                    Debug.Log("[IconLegendModal] Close clicked.");
                    Close();
                });
            }
            if (showIntroButton != null)
            {
                showIntroButton.onClick.RemoveAllListeners();
                showIntroButton.onClick.AddListener(() =>
                {
                    Debug.Log("[IconLegendModal] Show intro clicked.");
                    Close();
                    FirstRunIntroCard.ShowAlways();
                });
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── List build ──────────────────────────────────────────────────────

        private void BuildList()
        {
            if (listContainer == null) return;

            // Clear any existing children (defensive — runtime fallback
            // builds the container empty, but a Resources prefab may not).
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            foreach (var entry in MapTooltipCatalog.AllForLegend())
                BuildRow(listContainer, entry);

            listBuilt = true;
        }

        private static void BuildRow(Transform parent, MapTooltipCatalog.Entry entry)
        {
            var row = new GameObject(entry.Name);
            row.transform.SetParent(parent, false);
            var rrt = row.AddComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(0f, 60f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var rle = row.AddComponent<LayoutElement>();
            rle.preferredHeight = 60f;

            // Icon slot — always present so rows line up even when an entry
            // has no authored sprite yet.
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(row.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(48f, 48f);
            var img = iconGO.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (entry.Icon != null) img.sprite = entry.Icon;
            else { img.color = new Color(0.4f, 0.4f, 0.4f, 0.4f); }
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 48f;
            iconLE.preferredHeight = 48f;
            iconLE.flexibleWidth = 0f;

            // Text column.
            var textCol = new GameObject("Text");
            textCol.transform.SetParent(row.transform, false);
            textCol.AddComponent<RectTransform>();
            var vlg = textCol.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            var textLE = textCol.AddComponent<LayoutElement>();
            textLE.flexibleWidth = 1f;

            BuildLabel(textCol.transform, entry.Name, 20f, FontStyles.Bold,
                       new Color(1f, 0.92f, 0.45f));
            BuildLabel(textCol.transform, entry.Description, 16f, FontStyles.Normal,
                       Color.white);
        }

        private static void BuildLabel(Transform parent, string text, float size,
                                       FontStyles style, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            UIFonts.Apply(tmp);
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("IconLegendModal");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860; // above RestMenu (850)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Backdrop
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(go.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = backdrop.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = true;

            // Panel — width fixed at 640, height auto-fits children via
            // ContentSizeFitter so adding/removing legend rows doesn't leave
            // the list clipped or floating outside the panel rect. VLG
            // controls both axes so each child's preferredHeight feeds into
            // the panel's preferred height.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(backdrop.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(640f, 0f); // height driven by fitter
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

            var pVlg = panel.AddComponent<VerticalLayoutGroup>();
            pVlg.padding = new RectOffset(24, 24, 18, 18);
            pVlg.spacing = 10f;
            pVlg.childAlignment = TextAnchor.UpperCenter;
            pVlg.childControlWidth = true;
            pVlg.childControlHeight = true;
            pVlg.childForceExpandWidth = true;
            pVlg.childForceExpandHeight = false;

            var pFitter = panel.AddComponent<ContentSizeFitter>();
            pFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            pFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            titleGO.AddComponent<RectTransform>();
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Icon Guide";
            titleTmp.fontSize = 32f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            UIFonts.Apply(titleTmp);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 48f;

            // Scrollable list area — runs without an actual ScrollRect for
            // simplicity. The panel sizeDelta is tall enough to fit all
            // current entries; if the catalog grows beyond the panel,
            // upgrade to a ScrollRect.
            var listGO = new GameObject("List");
            listGO.transform.SetParent(panel.transform, false);
            var listRT = listGO.AddComponent<RectTransform>();
            listRT.sizeDelta = new Vector2(0f, 0f);
            var lvlg = listGO.AddComponent<VerticalLayoutGroup>();
            lvlg.spacing = 6f;
            lvlg.childAlignment = TextAnchor.UpperCenter;
            lvlg.childControlWidth = true;
            lvlg.childControlHeight = true;
            lvlg.childForceExpandWidth = true;
            var listFitter = listGO.AddComponent<ContentSizeFitter>();
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Buttons row: Show intro + Close. Listeners are wired in Awake
            // against the serialized button refs so they survive when this
            // hierarchy is saved as a Resources prefab and re-loaded.
            var introBtn = BuildButton(panel.transform, "Show intro again");
            var closeBtn = BuildButton(panel.transform, "Close");

            var manager = go.AddComponent<IconLegendModal>();
            manager.panelRoot = backdrop;
            manager.listContainer = listRT;
            manager.closeButton = closeBtn;
            manager.showIntroButton = introBtn;

            backdrop.SetActive(false);

            return go;
        }

        private static Button BuildButton(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 52f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.24f, 1f);
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;
            var colors = btn.colors;
            colors.normalColor      = new Color(0.20f, 0.20f, 0.24f, 1f);
            colors.highlightedColor = new Color(0.32f, 0.32f, 0.40f, 1f);
            colors.pressedColor     = new Color(0.45f, 0.45f, 0.55f, 1f);
            colors.selectedColor    = new Color(0.32f, 0.32f, 0.40f, 1f);
            colors.disabledColor    = new Color(0.14f, 0.14f, 0.18f, 0.6f);
            btn.colors = colors;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 52f;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            UIFonts.Apply(tmp);

            return btn;
        }
    }
}
