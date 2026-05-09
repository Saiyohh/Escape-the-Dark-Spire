// CombatTestPreset.cs
// -----------------------------------------------------------------------------
// Bundle of test data for a combat scene launch. Store your party, encounter,
// and condition list here once; the CombatTestLauncher menu command copies
// these values onto the scene's CombatBootstrap every time you load the
// scene, so you never re-wire inspectors between runs.
//
// You can keep multiple presets (Easy fight, Boss, Full party vs wraith pack,
// Range-validation scenario, etc.) and point the launcher at whichever one.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "CombatTestPreset", menuName = "DarkSpire/Testing/Combat Test Preset")]
    public class CombatTestPreset : ScriptableObject
    {
        [Header("Party")]
        [Tooltip("Up to 4 characters spawned in rank order (index 0 = front).")]
        public CharacterData[] party;

        [Header("Encounter")]
        public EncounterSO encounter;

        // Conditions are now loaded from ConditionLibrary.Instance at runtime.
        // No per-preset conditions list — add/remove ConditionData assets in
        // the library directly (they auto-register via the postprocessor).

        [Header("Initial State")]
        [Tooltip("Starting gold passed to the UI manager.")]
        public int startingGold = 99;

        [Tooltip("Floor number passed to the UI manager.")]
        public int currentFloor = 1;

        [Header("Description")]
        [TextArea(2, 4)]
        [Tooltip("Freeform note — what scenario does this preset simulate?")]
        public string notes;
    }
}
