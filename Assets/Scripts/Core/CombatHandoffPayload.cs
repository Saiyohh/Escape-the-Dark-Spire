using UnityEngine;

namespace DarkSpire
{
    // Static input/output bag passed between the dungeon scene and the combat
    // scene. SceneFlow.LoadCombat populates Active before the swap;
    // CombatBootstrap reads Active on entry, writes Result on combat end,
    // and SceneFlow.ReturnFromCombat consumes Result back in the dungeon scene.
    //
    // Static (not a singleton MonoBehaviour) so it survives scene unloads
    // for free without needing DontDestroyOnLoad on a carrier object.
    public static class CombatHandoffPayload
    {
        public static class Active
        {
            public static CharacterData[] party;
            public static EncounterSO encounter;
            public static RewardOverride rewardOverride;
            public static string returnSceneName;
            public static Vector2Int returnGridPos;

            // The map monster the player walked into. Used to mark it
            // defeated in RunStateHolder on Victory so it doesn't respawn.
            public static MonsterSpawn pendingMonster;
            public static bool hasPendingMonster;

            public static EncounterType triggeringType;
        }

        public static class Result
        {
            public static CombatOutcome outcome;
            public static int goldEarned;
            public static bool keyDropped;
        }

        public static void ResetActive()
        {
            Active.party = null;
            Active.encounter = null;
            Active.rewardOverride = default;
            Active.returnSceneName = null;
            Active.returnGridPos = default;
            Active.pendingMonster = default;
            Active.hasPendingMonster = false;
            Active.triggeringType = default;
        }

        public static void ResetResult()
        {
            Result.outcome = CombatOutcome.Unknown;
            Result.goldEarned = 0;
            Result.keyDropped = false;
        }
    }
}
