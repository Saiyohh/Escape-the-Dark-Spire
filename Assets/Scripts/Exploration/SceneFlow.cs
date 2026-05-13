using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkSpire
{
    public static class SceneFlow
    {
        public const string DungeonSceneName = "DungeonFloor";
        public const string CombatSceneName  = "CombatTest";
        public const string GameOverSceneName = "GameOver";
        public const string VictorySceneName  = "Victory";

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
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }
}
