using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "FRA_NewFloor", menuName = "DarkSpire/Encounters/Floor Roadmap Annotations")]
    public class FloorRoadmapAnnotationsSO : ScriptableObject
    {
        public List<RoadmapAnnotation> annotations = new();
    }
}
