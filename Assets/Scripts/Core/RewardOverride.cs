using System;

namespace DarkSpire
{
    // Applied by DungeonManager.ApplyAnnotations to a specific slot in a
    // Sub-Manager's queue. Extend as new override kinds appear.
    [Serializable]
    public struct RewardOverride
    {
        public bool guaranteedKey;
        public int bonusGold;
        public EncounterSO bonusItemDrop;

        public bool HasAny =>
            guaranteedKey || bonusGold != 0 || bonusItemDrop != null;
    }
}
