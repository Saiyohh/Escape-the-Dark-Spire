// MapEntityHoverTrigger.cs
// -----------------------------------------------------------------------------
// Cursor-poll hover trigger for dungeon entities (entities, tiles, monsters).
// Mirrors WorldTooltipTrigger's polling model (the New Input System silently
// skips OnMouseEnter on world sprites without a Physics2D raycaster setup),
// but renders into the simpler MapHoverTooltip singleton instead of the
// combat-prefab-driven TooltipController stack.
//
// Authoring:
//   • Add this component to a world-space GameObject with a SpriteRenderer.
//   • The trigger auto-adds a sized BoxCollider2D if none exists.
//   • Configure via Setup(name, description, icon) — usually called by the
//     entity's spawner or the entity's own Initialize.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    public class MapEntityHoverTrigger : MonoBehaviour
    {
        private string title;
        private string body;
        private Sprite icon;
        private Collider2D selfCollider;
        private SpriteRenderer sr;
        private bool isHovered;

        public void Setup(string title, string body, Sprite icon)
        {
            this.title = title;
            this.body = body;
            this.icon = icon;
            EnsureCollider();
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            EnsureCollider();
        }

        private void OnDisable()
        {
            if (isHovered)
            {
                isHovered = false;
                MapHoverTooltip.Instance?.Hide(this);
            }
        }

        private void OnDestroy()
        {
            // Defensive — if the GameObject is destroyed mid-hover (entity
            // picked up, gate unlocked + replaced) make sure the tooltip
            // doesn't linger.
            if (isHovered) MapHoverTooltip.Instance?.Hide(this);
        }

        private void EnsureCollider()
        {
            if (selfCollider != null) return;
            selfCollider = GetComponent<Collider2D>();
            if (selfCollider != null) return;

            // Size the collider to the sprite if we have one, otherwise to a
            // unit tile (1×1). isTrigger=true so the collider doesn't affect
            // any future physics — it's just a hit volume for cursor polling.
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (sr != null && sr.sprite != null)
            {
                box.size = sr.sprite.bounds.size;
                box.offset = Vector2.zero;
            }
            else
            {
                box.size = new Vector2(1f, 1f);
            }
            selfCollider = box;
        }

        private void Update()
        {
            if (selfCollider == null) return;
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 mouseScreen = mouse.position.ReadValue();
            Vector3 mouseWorld = cam.ScreenToWorldPoint(
                new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

            bool overlap = selfCollider.OverlapPoint(mouseWorld);

            var tooltip = MapHoverTooltip.Instance;
            if (overlap && !isHovered)
            {
                isHovered = true;
                if (tooltip == null) tooltip = MapHoverTooltip.GetOrCreate();
                tooltip?.Show(this, title, body, icon, mouseScreen);
            }
            else if (!overlap && isHovered)
            {
                isHovered = false;
                tooltip?.Hide(this);
            }
            else if (overlap && isHovered)
            {
                // Keep the panel near the cursor as it moves over the icon.
                tooltip?.Move(mouseScreen);
            }
        }
    }
}
