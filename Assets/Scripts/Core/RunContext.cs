using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class RunContext
    {
        public static CharacterData[] party;

        public static PartyMemberRuntime[] partyState;

        public static List<ItemInstance> partyInventory = new();

        public static FloorGenerationConfigSO currentFloorConfig;

        public static GeneratedFloorSO snapshotOverride;

        public static int currentFloorIndex = 1;
        public static int seed;
        public static int keysHeld;
        public static int gold;
        public static float runTime;
        public static int fightsWon;

        public static GeneratedFloorData currentFloor;

        public static void NewRun(CharacterData[] partyIn, int initialSeed)
        {
            party = partyIn;
            partyState = null;
            partyInventory = new List<ItemInstance>();
            seed = initialSeed;
            currentFloorIndex = 1;
            keysHeld = 0;
            gold = 0;
            runTime = 0f;
            fightsWon = 0;
            currentFloor = null;
            snapshotOverride = null;
        }

        public static void EndRun()
        {
            party = null;
            partyState = null;
            partyInventory = new List<ItemInstance>();
            currentFloorConfig = null;
            snapshotOverride = null;
            currentFloor = null;
            keysHeld = 0;
            gold = 0;
            runTime = 0f;
            fightsWon = 0;
            currentFloorIndex = 1;
        }

        public static void InitializePartyState(CharacterData[] roster)
        {
            if (roster == null || roster.Length == 0)
            {
                partyState = null;
                return;
            }
            partyState = new PartyMemberRuntime[roster.Length];
            for (int i = 0; i < roster.Length; i++)
                partyState[i] = PartyMemberRuntime.FromCharacter(roster[i]);
        }
    }
}
