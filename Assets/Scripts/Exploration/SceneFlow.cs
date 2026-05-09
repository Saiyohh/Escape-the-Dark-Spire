using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkSpire
{
    // Static helper that owns the dungeon <-> combat scene swaps. Captures
    // run state into RunStateHolder before unloading the dungeon scene, then
    // restores it on return.
    //
    // Both scenes (dungeon + combat) MUST be in File > Build Settings >
    // Scenes In Build for SceneManager.LoadScene to find them by name.
    public static class SceneFlow
    {
        public const string DungeonSceneName = "DungeonFloor";
        // The existing combat scene is named CombatTest in the slice. Phase 11
        // will rename it to "Combat" — update this constant alongside.
        public const string CombatSceneName  = "CombatTest";
        public const string GameOverSceneName = "GameOver";
        public const string VictorySceneName  = "Victory";

        // Single-trigger guard. When two monsters reach the party on the same
        // frame, the second LoadCombat call would clobber CombatHandoffPayload
        // and re-save state to RunStateHolder. The flag is cleared in
        // ReturnFromCombat so the next dungeon visit can fight again.
        private static bool combatLoadInFlight;

        public static bool IsCombatLoadInFlight => combatLoadInFlight;

        public static void LoadCombat(EncounterResult result, MonsterSpawn pendingMonster, Vector2Int returnGridPos)
        {
            if (combatLoadInFlight)
            {
                Debug.Log("[SceneFlow] Combat load already in flight — second monster collision ignored.");
                return;
            }
            combatLoadInFlight = true;

            var holder = RunStateHolder.GetOrCreate();
            holder.SaveForCombat(RunContext.currentFloor, returnGridPos);

            CombatHandoffPayload.ResetActive();
            CombatHandoffPayload.ResetResult();
            CombatHandoffPayload.Active.party             = RunContext.party;
            CombatHandoffPayload.Active.encounter         = result.encounter;
            CombatHandoffPayload.Active.rewardOverride    = result.rewardOverride;
            CombatHandoffPayload.Active.returnSceneName   = DungeonSceneName;
            CombatHandoffPayload.Active.returnGridPos     = returnGridPos;
            CombatHandoffPayload.Active.pendingMonster    = pendingMonster;
            CombatHandoffPayload.Active.hasPendingMonster = true;
            CombatHandoffPayload.Active.triggeringType    = result.type;

            Debug.Log($"[SceneFlow] -> Combat: {result.type} slot {result.slotIndex} " +
                      $"{(result.encounter != null ? result.encounter.name : "(null)")}");

            if (!CanLoadScene(CombatSceneName))
            {
                Debug.LogError($"[SceneFlow] Combat scene '{CombatSceneName}' not in Build Settings. " +
                               "Add it via File > Build Settings.");
                return;
            }
            SwapScene(CombatSceneName);
        }

        public static void ReturnFromCombat(CombatOutcome outcome)
        {
            // Clear the in-flight guard before doing anything else so that if
            // ReturnFromCombat itself triggers further state changes, the next
            // dungeon-load can register new combats.
            combatLoadInFlight = false;

            var holder = RunStateHolder.Instance;

            switch (outcome)
            {
                case CombatOutcome.Victory:
                    if (holder != null && CombatHandoffPayload.Active.hasPendingMonster)
                    {
                        var spawn = CombatHandoffPayload.Active.pendingMonster;
                        if (spawn.tier == MonsterTier.Boss)
                            holder.bossDefeated = true;
                        else
                            holder.defeatedMonsters.Add(spawn.start);
                    }
                    CombatRewards.Apply(
                        CombatHandoffPayload.Active.rewardOverride,
                        out int goldGained, out bool keyDropped);
                    CombatHandoffPayload.Result.outcome     = CombatOutcome.Victory;
                    CombatHandoffPayload.Result.goldEarned  = goldGained;
                    CombatHandoffPayload.Result.keyDropped  = keyDropped;
                    RunContext.fightsWon++;
                    if (holder != null && holder.bossDefeated)
                        DungeonEvents.InvokeBossDefeated();
                    LoadDungeon();
                    break;

                case CombatOutcome.Flee:
                    // Per spec: monster returns to its patrol; encounter is
                    // already in the EM seen pool. Don't mark defeated.
                    Debug.Log("[SceneFlow] Flee — returning to dungeon, monster preserved.");
                    LoadDungeon();
                    break;

                case CombatOutcome.Wipe:
                default:
                    CombatHandoffPayload.Result.outcome = CombatOutcome.Wipe;
                    LoadGameOver();
                    break;
            }
        }

        public static void LoadDungeon()
        {
            if (!CanLoadScene(DungeonSceneName))
            {
                Debug.LogError($"[SceneFlow] Dungeon scene '{DungeonSceneName}' not in Build Settings.");
                return;
            }
            SwapScene(DungeonSceneName);
        }

        public static void LoadVictory()
        {
            if (!CanLoadScene(VictorySceneName))
            {
                Debug.LogError($"[SceneFlow] Victory scene '{VictorySceneName}' not in Build Settings. " +
                               "Add it via File > Build Settings.");
                return;
            }
            SwapScene(VictorySceneName);
        }

        public static void LoadGameOver()
        {
            if (!CanLoadScene(GameOverSceneName))
            {
                Debug.LogError($"[SceneFlow] GameOver scene '{GameOverSceneName}' not in Build Settings. " +
                               "Add it via File > Build Settings.");
                return;
            }
            SwapScene(GameOverSceneName);
        }

        // Hands the load to SceneTransitionOverlay if available so every
        // dungeon <-> combat swap shares the same fade. Falls back to a raw
        // SceneManager.LoadScene if no overlay is present yet (e.g., first
        // time the dungeon is opened directly via the editor).
        private static void SwapScene(string sceneName)
        {
            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
            {
                overlay.LoadSceneTransition(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private static bool CanLoadScene(string sceneName)
        {
            // GetSceneByName returns a default Scene struct if not loaded; we
            // need to check the build settings. Use Application.CanStreamedLevelBeLoaded.
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }
}
