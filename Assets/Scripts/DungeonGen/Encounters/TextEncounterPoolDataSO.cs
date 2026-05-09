using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Slice stub used by Campsite / Event / Shrine sub-managers. When the
    // real interactions are designed, this SO is replaced by a typed pool
    // (CampsiteEncounterPoolDataSO etc.) without changing the EM contract.
    [CreateAssetMenu(fileName = "TEP_NewPool", menuName = "DarkSpire/Encounters/Text Pool (slice stub)")]
    public class TextEncounterPoolDataSO : ScriptableObject, IEncounterPoolData
    {
        [TextArea(1, 3)]
        public List<string> textPool = new();
    }
}
