// FloorHUD.cs
// -----------------------------------------------------------------------------
// Persistent run-scoped HUD: floor number, keys (X/Y), gold, MM:SS timer, an
// optional row of party-HP bars, and the icon-guide "?" button. Lives across
// scene loads via DontDestroyOnLoad so it acts as a consistent top bar in
// BOTH the dungeon and combat scenes. Hidden automatically in non-gameplay
// scenes (MainMenu / Victory / GameOver).
//
// HAND-AUTHORED. The hierarchy MUST be authored in DungeonFloor.unity with
// all serialized references wired in the Inspector. Run
// Tools > DarkSpire > Scenes > Scaffold FloorHUD into open scene once to
// generate a starting hierarchy; after that, tweak freely.
//
// Push-driven via DungeonEvents (gold/key/HP). The timer polls
// RunContext.runTime each frame and the HUD also drives RunContext.runTime
// itself (using unscaledDeltaTime) — formerly done by DungeonBootstrap, but
// moved here so the timer keeps ticking during combat now that the HUD
// persists across scenes.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DarkSpire
{
    public class FloorHUD : MonoBehaviour
    {
        public static FloorHUD Instance { get; private set; }

        [Header("Top bar labels (required)")]
        [SerializeField] private TMP_Text floorLabel;
        [SerializeField] private TMP_Text keysLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text timerLabel;

        [Header("Party HP bars (optional — leave empty to hide the row)")]
        [SerializeField] private RectTransform hpBarContainer;
        [SerializeField] private PartyHpBar[] hpBars;

        [Header("Icon guide")]
        [Tooltip("Optional. Wire a Button child of the top bar to open the icon " +
                 "legend modal. Run Tools > DarkSpire > Scenes > Add Legend Button " +
                 "to FloorHUD to drop one in automatically.")]
        [SerializeField] private Button legendButton;

        [Header("Cross-scene visibility")]
        [Tooltip("Scenes (by name) where this HUD should be hidden. Default: " +
                 "the title / victory / game-over scenes — anywhere outside a run.")]
        [SerializeField] private string[] hideInScenes = { "MainMenu", "Victory", "GameOver" };

        private int lastWholeSecond = -1;
        private bool warnedAboutMissingRefs;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            // Persistent singleton — first authored copy wins, subsequent
            // duplicates (re-entering DungeonFloor after combat) self-destruct.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (legendButton != null)
            {
                legendButton.onClick.RemoveAllListeners();
                legendButton.onClick.AddListener(OnLegendButtonClicked);
            }

            SceneManager.activeSceneChanged += HandleSceneChanged;
            ApplyVisibilityForScene(SceneManager.GetActiveScene().name);
        }

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

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            WarnIfMissingRefs();

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
            // Drive the run-timer ourselves — DungeonBootstrap used to do this
            // but only existed inside the dungeon scene. The HUD is persistent
            // so the timer keeps ticking during combat. unscaledDeltaTime so
            // any modal that sets timeScale = 0 doesn't freeze the wall clock.
            RunContext.runTime += Time.unscaledDeltaTime;
            UpdateTimerLabel(RunContext.runTime, force: false);
        }

        // ─── Scene visibility ────────────────────────────────────────────────

        private void HandleSceneChanged(Scene prev, Scene next)
        {
            ApplyVisibilityForScene(next.name);
            // Re-hydrate labels on scene change so the HUD is correct on the
            // dungeon → combat → dungeon round trip.
            if (gameObject.activeSelf)
            {
                HydrateFloor();
                HandleGoldChanged(RunContext.gold);
                HandleKeyCollected(RunContext.keysHeld, ResolveKeysRequired());
                HandlePartyHpChanged();
                UpdateTimerLabel(RunContext.runTime, force: true);
            }
        }

        private void ApplyVisibilityForScene(string sceneName)
        {
            bool hidden = false;
            if (hideInScenes != null)
            {
                for (int i = 0; i < hideInScenes.Length; i++)
                {
                    if (hideInScenes[i] == sceneName) { hidden = true; break; }
                }
            }
            if (gameObject.activeSelf == hidden) gameObject.SetActive(!hidden);
        }

        // ─── Button handlers ─────────────────────────────────────────────────

        private void OnLegendButtonClicked()
        {
            Debug.Log("[FloorHUD] Legend button clicked.");
            var modal = IconLegendModal.Instance ?? IconLegendModal.GetOrCreate();
            modal?.Open();
        }

        private void WarnIfMissingRefs()
        {
            if (warnedAboutMissingRefs) return;
            if (floorLabel != null && keysLabel != null &&
                goldLabel != null && timerLabel != null) return;

            warnedAboutMissingRefs = true;
            Debug.LogWarning(
                "[FloorHUD] One or more required label references are not wired " +
                "in the Inspector (floorLabel / keysLabel / goldLabel / timerLabel). " +
                "Author the HUD in the scene — run " +
                "Tools > DarkSpire > Scenes > Scaffold FloorHUD into open scene " +
                "for a starting layout.", this);
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
    }
}
