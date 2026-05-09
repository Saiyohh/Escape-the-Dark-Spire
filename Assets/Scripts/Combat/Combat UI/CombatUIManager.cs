// CombatUIManager.cs
// -----------------------------------------------------------------------------
// Top-level combat UI coordinator. Owns the two canvases the rest of the UI
// nests under:
//
//   • Combat (Screen Space - Overlay) — buttons, skill panel, floating
//     numbers, dice roll UI, phase banner, turn order, log. One canvas
//     for ALL screen-space UI in combat.
//
//   • World (World Space) — per-unit HUDs, ChanceBoxes, anything that
//     needs to live in world coords and follow units. One canvas for
//     ALL world-space UI in combat. Children use a WorldFollow component
//     to track their unit's transform position.
//
// Static accessors (CombatCanvas / WorldCanvas) let HUD spawners and the
// floating-number manager fetch the shared canvas without scene lookups.
//
// Scene setup:
//   CombatUI (GameObject, scene root)
//     + CombatUIManager script (wired to combatCanvas + worldCanvas slots)
//     + FloatingNumberManager script (uses combatCanvas for screen-space arc)
//     ├─ CombatCanvas (Screen Space - Overlay)
//     │     ├─ ActionButtonsUI + SkillSubmenuUI
//     │     ├─ (FloatingNumber + FloatingFlare children spawn here at runtime)
//     │     └─ (future: dice roll / phase banner / turn order / log)
//     └─ WorldCanvas (World Space, scale 0.01)
//           ├─ (HUD_Player + HUD_Enemy children spawn here at runtime)
//           └─ (ChanceBox children spawn here at runtime)
// -----------------------------------------------------------------------------
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

        /// <summary>
        /// The shared Screen Space - Overlay canvas. HUDs / popups / panels
        /// that animate in pixel coords parent under this. Read by
        /// FloatingNumberManager and any future overlay widget.
        /// </summary>
        public static Canvas CombatCanvas => Instance != null ? Instance.combatCanvas : null;

        /// <summary>
        /// The shared World Space canvas (scale 0.01). Per-unit HUDs and
        /// ChanceBoxes parent under this. Children use a WorldFollow
        /// component to track their unit's transform position.
        /// </summary>
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

        /// <summary>
        /// Called by CombatBootstrap after the combat state is initialized.
        /// Use this for any per-run context we want to propagate (gold, floor,
        /// unit rosters). Stub kept so bootstrap wiring doesn't have to change.
        /// </summary>
        public void Initialize(int startingGold, int currentFloor,
            List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            // ActionButtonsUI and SkillSubmenuUI self-subscribe to CombatEvents
            // in their OnEnable, so nothing to wire here. Room reserved for
            // dice roll UI, phase banner, turn order, combat log — Batch 2.
        }
    }
}
