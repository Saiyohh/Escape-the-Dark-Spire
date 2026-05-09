// CombatBootstrap.cs
// -----------------------------------------------------------------------------
// Scene entry point for the combat scene. Two entry paths:
//
//  1. Dungeon handoff (production path).
//     SceneFlow.LoadCombat populated CombatHandoffPayload.Active before the
//     scene swap. We read party + encounter from there, run the fight,
//     subscribe to CombatEvents.OnCombatEnd, and on end write the result
//     back into CombatHandoffPayload.Result and load the return scene via
//     SceneFlow.ReturnFromCombat.
//
//  2. Editor test path.
//     If CombatHandoffPayload.Active.encounter is null, we fall back to the
//     inspector-assigned party + encounter (the original CombatTest workflow).
//     OnCombatEnd is NOT subscribed in this path so the scene stays loaded
//     for inspection — same behaviour as before Phase 10.
//
// PORT NOTE: Echoes' RelicManager / RelicData / startingRelic wiring was
// stripped — relics are not ported (replaced by Aspect Tree in Pass 5).
// -----------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

namespace DarkSpire
{
    public class CombatBootstrap : MonoBehaviour
    {
        [Header("Editor Test Fallbacks (used when CombatHandoffPayload is empty)")]
        [SerializeField] private CharacterData[] party;
        [SerializeField] private EncounterSO encounter;

        [Header("Starting Gold/Floor")]
        [SerializeField] private int startingGold = 99;
        [SerializeField] private int currentFloor = 1;

        [Header("Manager References (auto-found if null)")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private CombatUIManager combatUIManager;
        [SerializeField] private TargetingSystem targetingSystem;

        [Header("Return Flow")]
        [Tooltip("Seconds to hold on the result screen before triggering the scene-transition " +
                 "overlay. Long enough to let the killing blow's floating damage number " +
                 "finish its arc (~1.6s) plus a beat to read. The overlay's slide-up + " +
                 "slide-down adds ~0.8s on top of this.")]
        [SerializeField] private float returnDelay = 2.0f;

        private bool fromHandoff;

        private void Start()
        {
            // Run-tier UI host. Normally already alive (DungeonBootstrap spawned
            // it on entering the run); this call covers editor-only entry where
            // the combat scene is opened directly. Idempotent.
            MenuCanvasController.GetOrCreate();

            // Auto-find managers if not assigned
            if (combatManager == null)   combatManager = FindAnyObjectByType<CombatManager>();
            if (combatUIManager == null) combatUIManager = FindAnyObjectByType<CombatUIManager>();
            if (targetingSystem == null) targetingSystem = FindAnyObjectByType<TargetingSystem>();

            if (combatManager == null)
            {
                Debug.LogError("[CombatBootstrap] CombatManager not found!");
                return;
            }

            // Resolve source: encounter from payload triggers the handoff path
            // (subscribe to OnCombatEnd, return to dungeon scene). Party may
            // fall back to inspector since slice doesn't have party-select yet
            // and RunContext.party is null in that case.
            CharacterData[] resolvedParty;
            EncounterSO resolvedEncounter;
            if (CombatHandoffPayload.Active.encounter != null)
            {
                resolvedEncounter = CombatHandoffPayload.Active.encounter;
                bool payloadHasParty = CombatHandoffPayload.Active.party != null
                                       && CombatHandoffPayload.Active.party.Length > 0;
                resolvedParty = payloadHasParty ? CombatHandoffPayload.Active.party : party;
                fromHandoff = true;
            }
            else
            {
                resolvedParty     = party;
                resolvedEncounter = encounter;
                fromHandoff       = false;
            }

            if (resolvedParty == null || resolvedParty.Length == 0)
            {
                Debug.LogError("[CombatBootstrap] No party members assigned (handoff empty + inspector empty)!");
                return;
            }
            if (resolvedEncounter == null)
            {
                Debug.LogError("[CombatBootstrap] No encounter assigned (handoff empty + inspector empty)!");
                return;
            }

            combatManager.InitializeCombat(resolvedParty, resolvedEncounter);

            // Hydrate persistent HP/SP from RunContext.partyState onto the
            // freshly-built Units. Skipped on the editor-test path (partyState
            // is null when the combat scene was opened directly), so Units
            // keep their default full-HP/SP from the constructor.
            //
            // Runs AFTER InitializeCombat so the combat-start Stars grant
            // (Divine Right) and any other init hooks fire on full state
            // first, then HP/SP get overwritten with persisted values.
            // Stars are intentionally NOT persisted (per-combat resource).
            if (CombatHandoffPayload.Active.encounter != null && RunContext.partyState != null)
            {
                var playerUnits = combatManager.PlayerUnits;
                int n = Mathf.Min(playerUnits.Count, RunContext.partyState.Length);
                for (int i = 0; i < n; i++)
                {
                    var pm = RunContext.partyState[i];
                    if (pm == null) continue;
                    pm.ApplyTo(playerUnits[i]);
                    // Back-reference so the Inventory helper can find this
                    // unit's pouch without scanning partyState.
                    playerUnits[i].partyMember = pm;
                }
            }

            if (combatUIManager != null)
            {
                combatUIManager.Initialize(
                    startingGold,
                    currentFloor,
                    combatManager.PlayerUnits,
                    combatManager.EnemyUnits);
            }

            Debug.Log($"[CombatBootstrap] Combat initialized: {resolvedParty.Length} party vs " +
                      $"{resolvedEncounter.encounterName} (handoff={fromHandoff})");

            if (fromHandoff)
            {
                CombatEvents.OnCombatEnd += HandleCombatEnd;
                Debug.Log("[CombatBootstrap] Handoff path active — subscribed to OnCombatEnd. " +
                          "Will return to dungeon after combat ends.");
            }
            else
            {
                Debug.Log("[CombatBootstrap] Editor test path — NOT returning to dungeon after combat. " +
                          "(CombatHandoffPayload.Active.encounter was null.)");
            }
        }

        private void OnDestroy()
        {
            CombatEvents.OnCombatEnd -= HandleCombatEnd;
        }

        private void HandleCombatEnd(bool victory)
        {
            Debug.Log($"[CombatBootstrap] OnCombatEnd received (victory={victory}). " +
                      $"Returning to dungeon in {returnDelay}s.");
            // Avoid double-fire if the event ever re-invokes for any reason.
            CombatEvents.OnCombatEnd -= HandleCombatEnd;

            // Capture final HP/SP back into the persistent state so the next
            // combat (and future dungeon healing/UI) sees the carried values.
            // HP=0 is captured as-is — downed members carry into the dungeon.
            if (RunContext.partyState != null && combatManager != null)
            {
                var playerUnits = combatManager.PlayerUnits;
                int n = Mathf.Min(playerUnits.Count, RunContext.partyState.Length);
                for (int i = 0; i < n; i++)
                    RunContext.partyState[i]?.CaptureFrom(playerUnits[i]);
            }

            var outcome = victory ? CombatOutcome.Victory : CombatOutcome.Wipe;
            StartCoroutine(ReturnAfterDelay(outcome));
        }

        private IEnumerator ReturnAfterDelay(CombatOutcome outcome)
        {
            yield return new WaitForSeconds(returnDelay);
            Debug.Log($"[CombatBootstrap] Triggering scene swap (outcome={outcome}).");
            SceneFlow.ReturnFromCombat(outcome);
        }
    }
}
