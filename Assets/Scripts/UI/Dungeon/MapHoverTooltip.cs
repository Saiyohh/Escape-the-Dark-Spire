// MapHoverTooltip.cs
// -----------------------------------------------------------------------------
// Singleton overlay that displays a small name + description panel near the
// cursor when a dungeon entity is hovered. Mirrors the FlashMessageController /
// PickupNotificationManager runtime-fallback pattern — works without any
// Resources prefab or scene authoring, but a Resources/MapHoverTooltip prefab
// (if present) wins.
//
// One shared panel, one shared screen-space canvas. Hover trigger calls Show
// to feed content + cursor position; Hide releases. Move repositions only.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class MapHoverTooltip : MonoBehaviour
    {
        public static MapHoverTooltip Instance { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Image iconImage;

        // The trigger currently showing the tooltip — used to ignore stale
        // Hide() calls from a trigger whose hover ended after another already
        // took over.
        private object currentOwner;

        // ─── Public API ──────────────────────────────────────────────────────

        public static MapHoverTooltip GetOrCreate()
        {
            if (Instance != null) return Instance;
            var lib = ClarityUIPrefabLibrary.Instance;
            var prefab = lib != null ? lib.mapHoverTooltip : null;
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<MapHoverTooltip>();
        }

        public void Show(object owner, string title, string body, Sprite icon, Vector2 screenPos)
        {
            currentOwner = owner;
            if (titleLabel != null) titleLabel.text = title ?? string.Empty;
            if (bodyLabel != null) bodyLabel.text = body ?? string.Empty;
            if (iconImage != null)
            {
                bool hasIcon = icon != null;
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon) iconImage.sprite = icon;
            }
            if (panelRoot != null) panelRoot.SetActive(true);
            Move(screenPos);
        }

        public void Move(Vector2 screenPos)
        {
            if (panelRect == null) return;
            var parentRT = panelRect.parent as RectTransform;
            if (parentRT == null) return;

            // Offset the tooltip up + right of the cursor so it doesn't sit
            // under the icon being hovered. Clamp inside the screen so the
            // panel never spills off the edge.
            Vector2 target = screenPos + new Vector2(20f, 20f);

            // Force-rebuild so we read the current size.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Vector2 size = panelRect.rect.size;

            target.x = Mathf.Clamp(target.x, size.x * 0.5f, Screen.width  - size.x * 0.5f);
            target.y = Mathf.Clamp(target.y, size.y * 0.5f, Screen.height - size.y * 0.5f);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRT, target, null, out Vector2 localPoint))
            {
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.localPosition = localPoint;
            }
        }

        public void Hide(object owner)
        {
            // Only honor Hide from the trigger that last showed. Otherwise a
            // mouse-out from a previously-hovered trigger could wipe the
            // tooltip the new hovered one just showed.
            if (currentOwner != null && currentOwner != owner) return;
            currentOwner = null;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("MapHoverTooltip");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 760; // above flash messages (750), below pause (800)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Panel — anchored to canvas CENTER so the localPoint we get back
            // from ScreenPointToLocalPointInRectangle (which is in canvas-
            // center coordinates) lines up with the panel's localPosition.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(360f, 60f);

            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0f, 0f, 0f, 0.85f);
            pImg.raycastTarget = false;

            var hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 14, 10, 10);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Icon
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(panel.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(40f, 40f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 40f;
            iconLE.preferredHeight = 40f;
            iconLE.flexibleWidth = 0f;
            iconGO.SetActive(false);

            // Text column
            var textCol = new GameObject("Text");
            textCol.transform.SetParent(panel.transform, false);
            textCol.AddComponent<RectTransform>();
            var vlg = textCol.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            var textLE = textCol.AddComponent<LayoutElement>();
            textLE.preferredWidth = 280f;
            textLE.flexibleWidth = 0f;

            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(textCol.transform, false);
            titleGO.AddComponent<RectTransform>();
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "";
            title.fontSize = 22f;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.92f, 0.45f);
            title.alignment = TextAlignmentOptions.TopLeft;
            title.raycastTarget = false;
            UIFonts.Apply(title);

            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(textCol.transform, false);
            bodyGO.AddComponent<RectTransform>();
            var body = bodyGO.AddComponent<TextMeshProUGUI>();
            body.text = "";
            body.fontSize = 18f;
            body.color = Color.white;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.raycastTarget = false;
            UIFonts.Apply(body);

            var manager = go.AddComponent<MapHoverTooltip>();
            manager.panelRoot  = panel;
            manager.panelRect  = prt;
            manager.titleLabel = title;
            manager.bodyLabel  = body;
            manager.iconImage  = iconImg;

            panel.SetActive(false);
            return go;
        }
    }
}
