using UnityEngine;

namespace DarkSpire
{
    public static class CombatHandoffPayload
    {
        public static class Active
        {
            public static CharacterData[] party;
            public static EncounterSO encounter;
            public static RewardOverride rewardOverride;
            public static string returnSceneName;
            public static Vector2Int returnGridPos;

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
