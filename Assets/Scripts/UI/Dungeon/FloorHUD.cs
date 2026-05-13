using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class FloorHUD : MonoBehaviour
    {
        [Header("Top bar labels (required)")]
        [SerializeField] private TMP_Text floorLabel;
        [SerializeField] private TMP_Text keysLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text timerLabel;

        [Header("Party HP bars (optional — leave empty to hide the row)")]
        [SerializeField] private RectTransform hpBarContainer;
        [SerializeField] private PartyHpBar[] hpBars;

        private int lastWholeSecond = -1;
        private bool warnedAboutMissingRefs;

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
            WarnIfMissingRefs();

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
