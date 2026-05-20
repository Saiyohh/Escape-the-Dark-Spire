using UnityEngine;

namespace DarkSpire
{
    // Shared base for map-side entity components. Owns the SpriteRenderer
    // ref handed in by EntitySpawner and registers/unregisters with the
    // DungeonRegistry. Subclasses implement IInteractable / IWalkOver /
    // IBlocker as needed and call SwapSprite or Tint to express state.
    public abstract class MapEntityBase : MonoBehaviour, IDungeonEntity
    {
        public Vector2Int GridPos { get; protected set; }

        protected SpriteRenderer sr;
        protected EntityPlacement placement;

        // Backing field for the explicit library handed in at Initialize().
        // Subclasses should access via the `library` property below so the
        // singleton fallback applies if no explicit ref was passed.
        private MapEntitySpriteLibrary _library;
        protected MapEntitySpriteLibrary library =>
            MapEntitySpriteLibrary.ResolveOrSingleton(_library);

        public virtual void Initialize(
            EntityPlacement placement,
            SpriteRenderer sr,
            MapEntitySpriteLibrary library)
        {
            this.placement = placement;
            this.sr = sr;
            this._library = library;
            GridPos = placement.position;
            DungeonRegistry.Instance?.Register(this);

            // Spawn an above-tile "[E] ..." indicator for interactables so the
            // player learns the E-key affordance organically. The indicator
            // reads PromptText every frame via a delegate so state changes
            // (chest opened, gate keys collected, shrine used) are reflected
            // live without an explicit refresh API.
            if (this is IInteractable interactable)
            {
                int promptOrder = sr != null ? sr.sortingOrder + 2 : 9;
                InteractPromptIndicator.AttachTo(
                    transform,
                    GridPos,
                    () => interactable.PromptText,
                    InteractPromptIndicator.Range.Adjacency,
                    promptOrder);
            }
        }

        protected virtual void OnDestroy()
        {
            DungeonRegistry.Instance?.Unregister(this);
        }

        protected void SwapSprite(Sprite sprite, Color? colorOverride = null)
        {
            if (sr == null) return;
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.color = colorOverride ?? Color.white;
            }
            else if (colorOverride.HasValue)
            {
                sr.color = colorOverride.Value;
            }
        }
    }
}
