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
