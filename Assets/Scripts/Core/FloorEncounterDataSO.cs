using UnityEngine;

namespace DarkSpire
{
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

        [Header("Roadmap (Phase 8)")]
        public FloorRoadmapAnnotationsSO annotations;
    }
}
