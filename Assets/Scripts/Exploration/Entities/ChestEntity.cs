using UnityEngine;

namespace DarkSpire
{
    // Adjacency-interact (E key). Once opened: rolls a small gold reward
    // and swaps to the chestOpen sprite (or a darker tint when no authored
    // sprite is set). Slice ships gold-only; later phases add potions/keys.
    public class ChestEntity : MapEntityBase, IInteractable
    {
        public string PromptText => opened ? "" : "[E] Open chest";
        public bool CanInteract(in InteractionContext ctx) => !opened;

        private bool opened;

        public override void Initialize(EntityPlacement placement, SpriteRenderer sr, MapEntitySpriteLibrary library)
        {
            base.Initialize(placement, sr, library);
            // Resume: restore opened state if this chest was opened pre-combat.
            if (RunStateHolder.Instance != null && RunStateHolder.Instance.IsChestOpened(GridPos))
            {
                opened = true;
                var openSprite = library != null ? library.chestOpen : null;
                SwapSprite(openSprite, new Color(0.45f, 0.28f, 0.10f));
            }
        }

        public void Interact(in InteractionContext ctx)
        {
            if (opened) return;
            opened = true;

            int gold = Random.Range(10, 31);     // [10, 30]
            RunContext.gold += gold;
            RunStateHolder.Instance?.openedChests.Add(GridPos);
            Debug.Log($"[Chest] Opened. +{gold} gold (total: {RunContext.gold})");
            // Phase 11: flash message

            var openSprite = library != null ? library.chestOpen : null;
            SwapSprite(openSprite, new Color(0.45f, 0.28f, 0.10f));
        }
    }
}
