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
            MenuCanvasController.GetOrCreate();

            if (combatManager == null)   combatManager = FindAnyObjectByType<CombatManager>();
            if (combatUIManager == null) combatUIManager = FindAnyObjectByType<CombatUIManager>();
            if (targetingSystem == null) targetingSystem = FindAnyObjectByType<TargetingSystem>();

            if (combatManager == null)
            {
                Debug.LogError("[CombatBootstrap] CombatManager not found!");
                return;
            }

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

            if (CombatHandoffPayload.Active.encounter != null && RunContext.partyState != null)
            {
                var playerUnits = combatManager.PlayerUnits;
                int n = Mathf.Min(playerUnits.Count, RunContext.partyState.Length);
                for (int i = 0; i < n; i++)
                {
                    var pm = RunContext.partyState[i];
                    if (pm == null) continue;
                    pm.ApplyTo(playerUnits[i]);
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
            CombatEvents.OnCombatEnd -= HandleCombatEnd;

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
