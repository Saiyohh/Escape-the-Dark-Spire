using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "TEP_NewPool", menuName = "DarkSpire/Encounters/Text Pool (slice stub)")]
    public class TextEncounterPoolDataSO : ScriptableObject, IEncounterPoolData
    {
        [TextArea(1, 3)]
        public List<string> textPool = new();
    }
}
