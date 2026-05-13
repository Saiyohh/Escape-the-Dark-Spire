using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class CombatUIManager : MonoBehaviour
    {
        [Header("Shared Canvases")]
        [Tooltip("Screen Space - Overlay canvas. Hosts all 2D UI: buttons, " +
                 "skill panel, floating numbers, dice roll, banners, log.")]
        [SerializeField] private Canvas combatCanvas;

        [Tooltip("World Space canvas (scale 0.01). Hosts all in-world UI: " +
                 "per-unit HUDs, ChanceBoxes, anything anchored to a unit.")]
        [SerializeField] private Canvas worldCanvas;

        [Header("Overlay UI")]
        [SerializeField] private ActionButtonsUI actionButtons;
        [SerializeField] private SkillSubmenuUI skillSubmenu;

        [Header("Systems")]
        [SerializeField] private FloatingNumberManager floatingNumberManager;

        [Header("World UI Prefabs")]
        [Tooltip("Turn Indicator is now a world-space SpriteRenderer prefab — drag in " +
                 "any GameObject with a TurnIndicator component. Spawned once on Awake " +
                 "as a child of CombatUI; the script re-parents it under the active " +
                 "unit's display each turn and back here between turns.")]
        [SerializeField] private GameObject turnIndicatorPrefab;

        public static CombatUIManager Instance { get; private set; }

        public static Canvas CombatCanvas => Instance != null ? Instance.combatCanvas : null;

        public static Canvas WorldCanvas => Instance != null ? Instance.worldCanvas : null;

        private void Awake()
        {
            Instance = this;
            if (skillSubmenu != null) skillSubmenu.Close();
            SpawnWorldUI();
        }

        private void SpawnWorldUI()
        {
            if (turnIndicatorPrefab != null
                && GetComponentInChildren<TurnIndicator>(includeInactive: true) == null)
            {
                // Parent under CombatUI itself — that becomes the indicator's
                // "stable root" between turns. The TurnIndicator script
                // re-parents itself under the active unit's display on
                // OnUnitTurnStart and back here on OnUnitTurnEnd.
                Instantiate(turnIndicatorPrefab, transform);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(int startingGold, int currentFloor,
            List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            // ActionButtonsUI and SkillSubmenuUI self-subscribe to CombatEvents
            // in their OnEnable, so nothing to wire here. Room reserved for
            // dice roll UI, phase banner, turn order, combat log — Batch 2.
        }
    }
}
