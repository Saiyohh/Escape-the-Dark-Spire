using UnityEngine;

namespace DarkSpire
{
    public class BossGateEntity : MapEntityBase, IInteractable, IBlocker
    {
        public bool IsBlocking => !unlocked;

        public string BlockReason =>
            $"Locked. Need {KeysRequired} keys (have {RunContext.keysHeld}).";

        public string PromptText =>
            unlocked ? "" : $"[E] Open gate ({RunContext.keysHeld}/{KeysRequired})";

        public bool CanInteract(in InteractionContext ctx) => !unlocked;

        private bool unlocked;
        private int KeysRequired => placement.intPayload > 0 ? placement.intPayload : 2;

        public override void Initialize(EntityPlacement placement, SpriteRenderer sr, MapEntitySpriteLibrary library)
        {
            base.Initialize(placement, sr, library);
            if (RunStateHolder.Instance != null && RunStateHolder.Instance.IsGateUnlocked(GridPos))
            {
                unlocked = true;
                var openSprite = library != null
                    ? library.GetBossGateSprite(placement.strPayload, open: true)
                    : null;
                SwapSprite(openSprite, new Color(0.50f, 0.50f, 0.50f, 0.65f));
            }
        }

        public void Interact(in InteractionContext ctx)
        {
            if (unlocked) return;

            if (RunContext.keysHeld >= KeysRequired)
            {
                unlocked = true;
                RunStateHolder.Instance?.unlockedGates.Add(GridPos);
                Debug.Log($"[BossGate] Unlocked with {RunContext.keysHeld} keys.");
                var openSprite = library != null
                    ? library.GetBossGateSprite(placement.strPayload, open: true)
                    : null;
                SwapSprite(openSprite, new Color(0.50f, 0.50f, 0.50f, 0.65f));
                DungeonEvents.InvokeFlashMessage("Gate unlocked.");
            }
            else
            {
                Debug.Log($"[BossGate] {BlockReason}");
                DungeonEvents.InvokeFlashMessage(
                    $"Need {KeysRequired} keys (have {RunContext.keysHeld}).");
            }
        }
    }
}
