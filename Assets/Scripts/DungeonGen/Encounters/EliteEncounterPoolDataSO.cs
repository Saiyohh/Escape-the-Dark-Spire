using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "EEP_NewFloor", menuName = "DarkSpire/Encounters/Elite Pool")]
    public class EliteEncounterPoolDataSO : ScriptableObject, IEncounterPoolData
    {
        public List<EncounterSO> elitePool = new();
    }
}
