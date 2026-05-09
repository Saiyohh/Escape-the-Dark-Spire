using System;

namespace DarkSpire
{
    // Return type from every Sub-Manager.GetNext() and the EncounterManager
    // dispatch. Carries either a real EncounterSO (Monster/Elite/Boss) or a
    // displayText payload (slice stubs for Campsite/Event/Shrine).
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
