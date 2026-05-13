using System;

namespace DarkSpire
{
    [Serializable]
    public struct RoadmapAnnotation
    {
        public EncounterType targetType;
        public int slotIndex;
        public RewardOverride reward;
    }
}
