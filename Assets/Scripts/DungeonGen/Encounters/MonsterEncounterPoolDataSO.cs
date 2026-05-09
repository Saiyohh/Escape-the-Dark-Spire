using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Authored pool for Standard monster encounters. Priority pool plays in
    // order (or randomized order if randomizePriorityOrder = true) for the
    // first N fights of the floor; general pool is shuffled and forms the
    // randomized tail.
    [CreateAssetMenu(fileName = "MEP_NewFloor", menuName = "DarkSpire/Encounters/Monster Pool")]
    public class MonsterEncounterPoolDataSO : ScriptableObject, IEncounterPoolData
    {
        [Tooltip("Fights that play first on the floor. Designer-curated for the opening sequence.")]
        public List<EncounterSO> priorityPool = new();

        [Tooltip("Fights that play after the priority queue empties. Always shuffled.")]
        public List<EncounterSO> generalPool = new();

        [Tooltip("If true, priorityPool order is shuffled at floor setup; if false, the " +
                 "authored order is preserved (curated opening fights).")]
        public bool randomizePriorityOrder = false;
    }
}
