// FloorHUD.cs
// -----------------------------------------------------------------------------
// Dungeon-only HUD: floor number, keys (X/Y), gold, MM:SS timer, and a row of
// up to 4 mini party-HP bars. Lives inside DungeonFloor.unity (not on the
// persistent MenuCanvas) so it disappears automatically when the scene
// transitions to Combat / Victory / GameOver.
//
// Push-driven via DungeonEvents (gold/key/HP). The timer polls
// RunContext.runTime in Update — cheaper than firing per-frame events.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class FloorHUD : MonoBehaviour
    {
        [Header("Top bar labels")]
        [SerializeField] private TMP_Text floorLabel;
        [SerializeField] private TMP_Text keysLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text timerLabel;

        [Header("Party HP bars")]
        [SerializeField] private RectTransform hpBarContainer;
        [SerializeField] private PartyHpBar[] hpBars;

        private int lastWholeSecond = -1;

        // ─── Public API ──────────────────────────────────────────────────────

        public static FloorHUD GetOrCreateInScene(Transform parent)
        {
            var existing = FindAnyObjectByType<FloorHUD>();
            if (existing != null) return existing;
            var go = BuildRuntimeFallback();
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<FloorHUD>();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            DungeonEvents.OnGoldChanged += HandleGoldChanged;
            DungeonEvents.OnKeyCollected += HandleKeyCollected;
            DungeonEvents.OnPartyHpChanged += HandlePartyHpChanged;
        }

        private void OnDisable()
        {
            DungeonEvents.OnGoldChanged -= HandleGoldChanged;
            DungeonEvents.OnKeyCollected -= HandleKeyCollected;
            DungeonEvents.OnPartyHpChanged -= HandlePartyHpChanged;
        }

        private void Start()
        {
            // Initial hydration so the HUD doesn't lag one event behind on
            // first scene load.
            HydrateFloor();
            HandleGoldChanged(RunContext.gold);
            HandleKeyCollected(RunContext.keysHeld, ResolveKeysRequired());
            HandlePartyHpChanged();
            UpdateTimerLabel(RunContext.runTime, force: true);
        }

        private void Update()
        {
            UpdateTimerLabel(RunContext.runTime, force: false);
        }

        // ─── Handlers ────────────────────────────────────────────────────────

        private void HydrateFloor()
        {
            if (floorLabel != null)
                floorLabel.text = $"Floor {RunContext.currentFloorIndex}";
        }

        private void HandleGoldChanged(int gold)
        {
            if (goldLabel != null) goldLabel.text = $"Gold {gold}";
        }

        private void HandleKeyCollected(int total, int required)
        {
            if (keysLabel != null) keysLabel.text = $"Keys {total}/{required}";
        }

        private void HandlePartyHpChanged()
        {
            if (hpBars == null) return;
            var state = RunContext.partyState;
            int n = state != null ? state.Length : 0;
            for (int i = 0; i < hpBars.Length; i++)
            {
                var bar = hpBars[i];
                if (bar == null) continue;
                if (i < n && state[i] != null && state[i].characterData != null)
                {
                    bar.gameObject.SetActive(true);
                    bar.Bind(state[i]);
                }
                else
                {
                    bar.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateTimerLabel(float t, bool force)
        {
            int whole = Mathf.FloorToInt(t);
            if (!force && whole == lastWholeSecond) return;
            lastWholeSecond = whole;
            if (timerLabel == null) return;
            int m = whole / 60;
            int s = whole % 60;
            timerLabel.text = $"{m:00}:{s:00}";
        }

        private int ResolveKeysRequired()
        {
            var cfg = RunContext.currentFloorConfig;
            return cfg != null ? cfg.keysRequired : 0;
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("FloorHUD");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600; // below pickups (700)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Top bar root — anchored top-stretch.
            var bar = new GameObject("TopBar");
            bar.transform.SetParent(go.transform, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 80f);

            var bg = bar.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 24, 12, 12);
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var floorLabel = BuildBarLabel(bar.transform, "Floor 1");
            var keysLabel  = BuildBarLabel(bar.transform, "Keys 0/0");
            var goldLabel  = BuildBarLabel(bar.transform, "Gold 0");
            var timerLabel = BuildBarLabel(bar.transform, "00:00");

            // Spacer pushes timer to the right.
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bar.transform, false);
            spacer.AddComponent<RectTransform>();
            var sle = spacer.AddComponent<LayoutElement>();
            sle.flexibleWidth = 999f;
            // Reorder so spacer sits before timer.
            timerLabel.transform.SetAsLastSibling();
            spacer.transform.SetSiblingIndex(timerLabel.transform.GetSiblingIndex());

            // HP bar container — under the top bar.
            var hpRow = new GameObject("HPBars");
            hpRow.transform.SetParent(go.transform, false);
            var hprt = hpRow.AddComponent<RectTransform>();
            hprt.anchorMin = new Vector2(0f, 1f);
            hprt.anchorMax = new Vector2(1f, 1f);
            hprt.pivot     = new Vector2(0.5f, 1f);
            hprt.anchoredPosition = new Vector2(0f, -80f);
            hprt.sizeDelta = new Vector2(0f, 60f);

            var hpHlg = hpRow.AddComponent<HorizontalLayoutGroup>();
            hpHlg.padding = new RectOffset(24, 24, 4, 4);
            hpHlg.spacing = 12f;
            hpHlg.childAlignment = TextAnchor.MiddleLeft;
            hpHlg.childControlWidth = false;
            hpHlg.childControlHeight = true;
            hpHlg.childForceExpandWidth = false;
            hpHlg.childForceExpandHeight = true;

            var bars = new PartyHpBar[4];
            for (int i = 0; i < 4; i++)
                bars[i] = PartyHpBar.BuildRuntime(hpRow.transform);

            var hud = go.AddComponent<FloorHUD>();
            hud.floorLabel = floorLabel;
            hud.keysLabel  = keysLabel;
            hud.goldLabel  = goldLabel;
            hud.timerLabel = timerLabel;
            hud.hpBarContainer = hprt;
            hud.hpBars = bars;
            return go;
        }

        private static TMP_Text BuildBarLabel(Transform parent, string text)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 130f;
            le.preferredWidth = 180f;
            return tmp;
        }
    }
}
