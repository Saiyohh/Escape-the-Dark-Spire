using UnityEngine;

namespace DarkSpire
{
    public class KeyEntity : MapEntityBase, IWalkOver
    {
        public void OnWalkedOver(in InteractionContext ctx)
        {
            RunContext.keysHeld++;
            RunStateHolder.Instance?.removedEntities.Add(GridPos);
            Debug.Log($"[Key] Picked up. Total: {RunContext.keysHeld}");

            var notif = PickupNotificationManager.Instance;
            var lib = MapEntitySpriteLibrary.Instance;
            if (notif != null)
                notif.ShowPickup("+1 Key gained.", lib != null ? lib.key : null);

            int required = RunContext.currentFloorConfig != null
                ? RunContext.currentFloorConfig.keysRequired
                : 0;
            DungeonEvents.InvokeKeyCollected(RunContext.keysHeld, required);

            DungeonRegistry.Instance?.Unregister(this);
            Destroy(gameObject);
        }
    }
}
