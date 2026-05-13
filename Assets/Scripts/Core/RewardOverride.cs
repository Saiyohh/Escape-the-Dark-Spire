using System;

namespace DarkSpire
{
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
