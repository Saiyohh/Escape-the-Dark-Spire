using System;

namespace DarkSpire
{
    // One designer-authored override keyed to a specific slot in a specific
    // Sub-Manager's queue. DungeonManager.ApplyAnnotations matches incoming
    // EncounterResults by (targetType, slotIndex) and writes `reward` into
    // result.rewardOverride.
    //
    // Example: Floor 1 slice annotates Monster slot 0 and slot 2 with
    // guaranteedKey=true so the 1st and 3rd Standard fights drop the floor's
    // two keys regardless of pool order.
    [Serializable]
    public struct RoadmapAnnotation
    {
        public EncounterType targetType;
        public int slotIndex;
        public RewardOverride reward;
    }
}
