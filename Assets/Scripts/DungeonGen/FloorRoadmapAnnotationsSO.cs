using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Per-floor list of slot overrides. Plugged into the floor's
    // FloorEncounterDataSO; DungeonManager.SetupForFloor(this) loads it.
    //
    // Slice authoring (Floor 1):
    //   { Monster, 0, { guaranteedKey: true } }  // 1st Standard fight drops a key
    //   { Monster, 2, { guaranteedKey: true } }  // 3rd Standard fight drops the second key
    [CreateAssetMenu(fileName = "FRA_NewFloor", menuName = "DarkSpire/Encounters/Floor Roadmap Annotations")]
    public class FloorRoadmapAnnotationsSO : ScriptableObject
    {
        public List<RoadmapAnnotation> annotations = new();
    }
}
