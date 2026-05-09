using UnityEngine;

namespace DarkSpire
{
    // Applies a RewardOverride post-combat. Phase 10 handles annotation
    // overrides only (key drops, bonus gold). Standard combat rewards
    // (per-enemy gold, item drops, EXP) land in a later phase alongside
    // the proper inventory/levelup systems.
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
            // o.bonusItemDrop — Phase 11+ when inventory exists.
        }
    }
}
