using UnityEngine;

namespace DarkSpire
{
    public abstract class MapEntityBase : MonoBehaviour, IDungeonEntity
    {
        public Vector2Int GridPos { get; protected set; }

        protected SpriteRenderer sr;
        protected EntityPlacement placement;

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
