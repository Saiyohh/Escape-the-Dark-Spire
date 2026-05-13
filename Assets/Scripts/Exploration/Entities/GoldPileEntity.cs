using UnityEngine;

namespace DarkSpire
{
    public class GoldPileEntity : MapEntityBase, IWalkOver
    {
        public void OnWalkedOver(in InteractionContext ctx)
        {
            int amount = placement.intPayload > 0 ? placement.intPayload : 10;
            RunContext.gold += amount;
            RunStateHolder.Instance?.removedEntities.Add(GridPos);
            Debug.Log($"[Gold] +{amount} (total: {RunContext.gold})");

            var notif = PickupNotificationManager.Instance;
            var lib = MapEntitySpriteLibrary.Instance;
            if (notif != null)
                notif.ShowPickup($"+{amount} Gold gained.", lib != null ? lib.goldPile : null);

            DungeonEvents.InvokeGoldChanged(RunContext.gold);

            DungeonRegistry.Instance?.Unregister(this);
            Destroy(gameObject);
        }
    }
}
