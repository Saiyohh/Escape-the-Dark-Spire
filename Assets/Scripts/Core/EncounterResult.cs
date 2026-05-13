using System;

namespace DarkSpire
{
    [Serializable]
    public struct EncounterResult
    {
        public EncounterSO encounter;     // null for stub sub-managers
        public string displayText;        // populated for stubs
        public RewardOverride rewardOverride;
        public EncounterType type;
        public int slotIndex;             // 0-indexed call count for this Sub-Manager this floor
    }
}
