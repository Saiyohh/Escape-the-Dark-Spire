using UnityEngine;

namespace DarkSpire
{
    // Bundle of every encounter pool a floor needs, plus its roadmap
    // annotations. EncounterManager.SetupForFloor(this) hands each pool
    // ref to the matching Sub-Manager.
    [CreateAssetMenu(fileName = "FED_NewFloor", menuName = "DarkSpire/Encounters/Floor Encounter Data")]
    public class FloorEncounterDataSO : ScriptableObject
    {
        [Header("Encounter Pools")]
        public MonsterEncounterPoolDataSO monsters;
        public EliteEncounterPoolDataSO elites;
        public BossEncounterPoolDataSO bosses;

        [Header("Slice Stubs (text payload only)")]
        public TextEncounterPoolDataSO campsites;
        public TextEncounterPoolDataSO events;
        public TextEncounterPoolDataSO shrines;

        // overrides, EM passes Sub-Manager results through unchanged.
        [Header("Roadmap (Phase 8)")]
        public FloorRoadmapAnnotationsSO annotations;
    }
}
