using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "BEP_NewFloor", menuName = "DarkSpire/Encounters/Boss Pool")]
    public class BossEncounterPoolDataSO : ScriptableObject, IEncounterPoolData
    {
        public List<EncounterSO> bossPool = new();
    }
}
