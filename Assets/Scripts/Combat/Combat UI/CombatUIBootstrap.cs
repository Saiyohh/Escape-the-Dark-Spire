// CombatUIBootstrap.cs
// -----------------------------------------------------------------------------
// Single explicit source of truth for "the action UI is in its idle state."
// Lives on the combat canvas as a once-per-scene component. Does two things:
//
//   1. On Awake (scene load) — force every modal / submenu / shared button
//      into its closed state, before any unit turn fires. Fixes the long-
//      standing class of bug where a designer-authored "active" GameObject
//      sat visible on screen until the first turn-event ran.
//
//   2. On CombatEvents.OnCombatStart — repeat the reset. Belt and suspenders
//      for cases where combat re-initializes mid-scene (e.g. a re-enter
//      flow, a transition between encounters in the same scene) and the UI
//      shouldn't carry stale binding state forward.
//
// The bootstrap doesn't OWN the visibility of any element — each submenu
// still self-manages via OnEnable / turn events at runtime. It just ensures
// the starting state is correct, which the per-element logic alone can't
// guarantee because no turn has fired yet at scene load.
//
// To extend: add another serialized field for a new modal piece + reset it
// in ResetAll. Keeping the list explicit (rather than auto-discovery) so
// authoring stays readable and side-effects don't surprise designers.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public class CombatUIBootstrap : MonoBehaviour
    {
        [Header("Submenus / modals to force-closed at scene load + combat start")]
        [Tooltip("Skill submenu — closed at boot to ensure no stale binding " +
                 "leaks into the first player turn.")]
        [SerializeField] private SkillSubmenuUI skillSubmenu;

        [Tooltip("Optional explicit BackButton reference. If null, the bootstrap " +
                 "uses BackButton.Instance (singleton lookup).")]
        [SerializeField] private BackButton backButton;

        [Tooltip("Any other GameObjects that should start hidden but might be " +
                 "left active by authoring (popup panels, modal overlays, etc.). " +
                 "These are SetActive(false) brute-force — use the typed slots " +
                 "above for elements that need their Close() called.")]
        [SerializeField] private GameObject[] hideOnLoad;

        private void Awake()
        {
            // Subscribe in Awake so we don't miss the very first OnCombatStart,
            // but DEFER the initial reset to Start. Awake order across
            // components isn't guaranteed; running ResetAll here would race
            // submenu Awakes (e.g. SkillSubmenuUI.Awake calls its own Close,
            // and a Close that fires before that Awake leaves the panel in a
            // half-initialized state that breaks the next OpenFor).
            CombatEvents.OnCombatStart += ResetAll;
        }

        private void Start()
        {
            // Runs once, AFTER every component's Awake has completed. Now it's
            // safe to assume each submenu has wired up its serialized refs
            // (panelRoot, etc.) and we can flip them all to closed state
            // cleanly.
            ResetAll();
        }

        private void OnDestroy()
        {
            CombatEvents.OnCombatStart -= ResetAll;
        }

        /// <summary>
        /// Idempotent. Drops every modal back to its closed state and
        /// releases any shared bindings. Safe to call from anywhere; the
        /// per-element Close/ForceClose methods all handle being called
        /// when already closed.
        /// </summary>
        public void ResetAll()
        {
            if (skillSubmenu != null)
                skillSubmenu.Close();

            // Prefer the serialized slot so the dependency is visible in the
            // scene; fall back to the singleton lookup so the bootstrap still
            // works in scenes that wire the BackButton elsewhere.
            var bb = backButton != null ? backButton : BackButton.Instance;
            if (bb != null) bb.ForceClose();

            if (hideOnLoad != null)
            {
                foreach (var go in hideOnLoad)
                    if (go != null && go.activeSelf) go.SetActive(false);
            }
        }
    }
}
