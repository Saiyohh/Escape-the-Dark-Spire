using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class RestMenu : MonoBehaviour
    {
        public static RestMenu Instance { get; private set; }

        [Header("Panel root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Buttons")]
        [SerializeField] private Button restoreButton;
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button viewStatsButton;
        [SerializeField] private Button leaveButton;

        [Header("Stats sub-panel")]
        [SerializeField] private GameObject statsRoot;
        [SerializeField] private TMP_Text statsLabel;

        private float prevTimeScale = 1f;

        // ─── Public API ──────────────────────────────────────────────────────

        public static RestMenu GetOrCreate()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<GameObject>("RestMenu");
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<RestMenu>();
        }

        public void Open(Vector2Int tilePos)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (statsRoot != null) statsRoot.SetActive(false);

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            Time.timeScale = prevTimeScale;
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

            if (panelRoot != null) panelRoot.SetActive(false);
            if (statsRoot != null) statsRoot.SetActive(false);

            if (restoreButton  != null) restoreButton.onClick.AddListener(OnRestore);
            if (reviveButton   != null) reviveButton.onClick.AddListener(OnRevive);
            if (viewStatsButton != null) viewStatsButton.onClick.AddListener(OnViewStats);
            if (leaveButton    != null) leaveButton.onClick.AddListener(OnLeave);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Button handlers ─────────────────────────────────────────────────

        private void OnRestore()
        {
            var lines = new List<(string text, Sprite icon)>();
            var state = RunContext.partyState;
            if (state != null)
            {
                for (int i = 0; i < state.Length; i++)
                {
                    var pm = state[i];
                    if (pm == null || pm.characterData == null) continue;
                    pm.currentHP = pm.characterData.maxHP;
                    pm.currentSP = pm.characterData.maxSP;
                    lines.Add(($"{pm.characterData.characterName} fully restored.", pm.characterData.headIcon));
                }
            }
            DungeonEvents.InvokePartyHpChanged();
            EmitToasts(lines);
            Close();
        }

        private void OnRevive()
        {
            var lines = new List<(string text, Sprite icon)>();
            var state = RunContext.partyState;
            if (state != null)
            {
                for (int i = 0; i < state.Length; i++)
                {
                    var pm = state[i];
                    if (pm == null || pm.characterData == null) continue;
                    if (pm.currentHP > 0) continue;
                    pm.currentHP = pm.characterData.maxHP;
                    pm.currentSP = pm.characterData.maxSP;
                    lines.Add(($"{pm.characterData.characterName} revived.", pm.characterData.headIcon));
                }
            }
            if (lines.Count > 0)
            {
                DungeonEvents.InvokePartyHpChanged();
                EmitToasts(lines);
            }
            Close();
        }

        private void OnViewStats()
        {
            if (statsRoot != null) statsRoot.SetActive(true);
            if (statsLabel == null) return;

            var sb = new System.Text.StringBuilder();
            var state = RunContext.partyState;
            if (state == null || state.Length == 0)
            {
                sb.AppendLine("No party state available.");
            }
            else
            {
                for (int i = 0; i < state.Length; i++)
                {
                    var pm = state[i];
                    if (pm == null || pm.characterData == null) continue;
                    sb.AppendLine($"{pm.characterData.characterName}");
                    sb.AppendLine($"  HP {pm.currentHP}/{pm.characterData.maxHP}   SP {pm.currentSP}/{pm.characterData.maxSP}");
                }
            }
            statsLabel.text = sb.ToString();
        }

        private void OnLeave() => Close();

        private static void EmitToasts(List<(string text, Sprite icon)> lines)
        {
            if (lines == null || lines.Count == 0) return;
            var toasts = PickupNotificationManager.Instance ?? PickupNotificationManager.GetOrCreate();
            toasts?.ShowMulti(lines);
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("RestMenu");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850; // above pause (800)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Dim backdrop covers the screen, blocks clicks.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(go.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = backdrop.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            bgImg.raycastTarget = true;

            // Panel — center, 520x420.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(backdrop.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(520f, 460f);
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.10f, 0.10f, 0.12f, 0.95f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            // Title.
            var title = BuildLabel(panel.transform, "Campsite", 36, TextAlignmentOptions.Center, 56f);

            var restoreBtn  = BuildButton(panel.transform, "Restore HP/SP");
            var reviveBtn   = BuildButton(panel.transform, "Revive Fallen");
            var viewBtn     = BuildButton(panel.transform, "View Stats");
            var leaveBtn    = BuildButton(panel.transform, "Leave");

            // Stats sub-panel — sibling under panel, hidden by default.
            var stats = new GameObject("Stats");
            stats.transform.SetParent(panel.transform, false);
            var srt = stats.AddComponent<RectTransform>();
            srt.sizeDelta = new Vector2(0f, 200f);
            var sImg = stats.AddComponent<Image>();
            sImg.color = new Color(0f, 0f, 0f, 0.4f);
            var statsLabel = BuildLabel(stats.transform, "", 22, TextAlignmentOptions.TopLeft, 0f);
            // Make label fill the stats sub-panel.
            var slrt = statsLabel.rectTransform;
            slrt.anchorMin = Vector2.zero;
            slrt.anchorMax = Vector2.one;
            slrt.offsetMin = new Vector2(12f, 12f);
            slrt.offsetMax = new Vector2(-12f, -12f);

            var manager = go.AddComponent<RestMenu>();
            manager.panelRoot = backdrop;
            manager.restoreButton  = restoreBtn;
            manager.reviveButton   = reviveBtn;
            manager.viewStatsButton = viewBtn;
            manager.leaveButton    = leaveBtn;
            manager.statsRoot = stats;
            manager.statsLabel = statsLabel;

            backdrop.SetActive(false);
            stats.SetActive(false);

            return go;
        }

        private static TMP_Text BuildLabel(Transform parent, string text, float size, TextAlignmentOptions align, float preferredHeight)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            if (preferredHeight > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return tmp;
        }

        private static Button BuildButton(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 56f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.24f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor      = new Color(0.20f, 0.20f, 0.24f, 1f);
            colors.highlightedColor = new Color(0.30f, 0.30f, 0.36f, 1f);
            colors.pressedColor     = new Color(0.14f, 0.14f, 0.18f, 1f);
            colors.selectedColor    = new Color(0.30f, 0.30f, 0.36f, 1f);
            colors.disabledColor    = new Color(0.12f, 0.12f, 0.14f, 0.6f);
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;

            var labelTmp = BuildLabel(go.transform, label, 24, TextAlignmentOptions.Center, 0f);
            var lrt = labelTmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
