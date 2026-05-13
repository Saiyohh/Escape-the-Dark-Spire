using UnityEngine;

namespace DarkSpire
{
    public static class CombatRewards
    {
        public static void Apply(RewardOverride o, out int goldGained, out bool keyDropped)
        {
            goldGained = 0;
            keyDropped = false;
            if (o.guaranteedKey)
            {
                RunContext.keysHeld++;
                keyDropped = true;
                Debug.Log($"[CombatRewards] Annotation key drop. keys={RunContext.keysHeld}");
            }
            if (o.bonusGold != 0)
            {
                RunContext.gold += o.bonusGold;
                goldGained = o.bonusGold;
                Debug.Log($"[CombatRewards] Annotation gold +{o.bonusGold}. total={RunContext.gold}");
            }
        }
    }
}
