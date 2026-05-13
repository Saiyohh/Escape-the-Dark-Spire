using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Static run-scoped state shared across scenes (Title -> PartySelect ->
    // DungeonFloor -> Combat -> back). Lives for the lifetime of one run.
    //
    // handoff, victory) fill in the rest.
    public static class RunContext
    {
        public static CharacterData[] party;

        // Runtime per-member state (HP/SP) that carries across scene swaps.
        // Parallel to `party` — partyState[i] tracks party[i]. Seeded by the
        // main menu (party-select stand-in) on Play, hydrated into Units at
        // combat start by CombatBootstrap, captured back on combat end.
        // Null on the editor-test path (combat scene opened directly).
        public static PartyMemberRuntime[] partyState;

        // Party-wide shared item bag — Notion Category=PartyInventory and
        // BrewedPotion items live here. Pouch items are character-locked and
        // live on PartyMemberRuntime.pouchItems. Both persist across scene
        // swaps for the lifetime of the run.
        public static List<ItemInstance> partyInventory = new();

        public static FloorGenerationConfigSO currentFloorConfig;

        // If set, the runtime uses this snapshot instead of running the
        // generator. Useful for tutorials, fixed test floors, screenshots.
        public static GeneratedFloorSO snapshotOverride;

        public static int currentFloorIndex = 1;
        public static int seed;
        public static int keysHeld;
        public static int gold;
        public static float runTime;
        public static int fightsWon;

        // Active floor data, owned by DungeonBootstrap; cleared on floor exit.
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

        // Seed partyState from a roster of CharacterData, each at full HP/SP.
        // Called by the main menu (party-select stand-in) on Play.
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
