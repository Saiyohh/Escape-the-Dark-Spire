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
            CombatEvents.OnCombatStart += ResetAll;
        }

        private void Start()
        {
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
