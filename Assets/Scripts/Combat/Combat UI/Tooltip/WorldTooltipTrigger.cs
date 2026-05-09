// WorldTooltipTrigger.cs
// -----------------------------------------------------------------------------
// Concrete tooltip trigger base for world-space (non-UI) elements. The
// project runs the new Input System, which means OnMouseEnter/Exit and
// IPointerEnterHandler don't fire on world sprites without a Physics2D
// raycaster setup. Mirroring the pattern UnitDisplay uses, this trigger
// polls Physics2D.OverlapPoint(cursorWorldPos) every frame and tracks
// hover transitions itself.
//
// Authoring shape:
//   • The GameObject must have a Collider2D sized to the visible sprite.
//     A BoxCollider2D auto-sized via the SpriteRenderer's bounds is the
//     usual choice. Without a collider Physics2D.OverlapPoint never hits.
//   • The trigger uses the trigger's own SpriteRenderer (or any Renderer)
//     to compute the on-screen anchor rect — see TooltipTrigger.GetAnchorScreenRect.
//
// Subclasses must implement BuildContent (inherited from TooltipTrigger).
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class WorldTooltipTrigger : TooltipTrigger
    {
        // The cursor world-space projection uses Camera.main. Combat scenes
        // tag their camera MainCamera, so no inspector override is needed
        // here — keep the trigger's authoring surface empty.
        private Collider2D selfCollider;
        private bool isHovered;

        protected override void Awake()
        {
            base.Awake();
            selfCollider = GetComponent<Collider2D>();
        }

        protected virtual void OnEnable()
        {
            isHovered = false;
        }

        // OnDisable overridden in TooltipTrigger handles HideTooltip; piggy-back.
        protected override void OnDisable()
        {
            base.OnDisable();
            isHovered = false;
        }

        private void Update()
        {
            if (selfCollider == null) return;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 mouseScreen = mouse.position.ReadValue();
            Vector3 mouseWorld = cam.ScreenToWorldPoint(
                new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

            // Direct collider test rather than a global OverlapPoint — cheaper
            // and avoids picking up overlapping siblings (a UnitDisplay
            // collider behind the orb tray, etc.).
            bool overlap = selfCollider.OverlapPoint(mouseWorld);

            if (overlap && !isHovered)
            {
                isHovered = true;
                ShowTooltip();
            }
            else if (!overlap && isHovered)
            {
                isHovered = false;
                HideTooltip();
            }
            // Anchor tracking when the on-screen position shifts is handled
            // by TooltipSpawnPoint.LateUpdate — no per-trigger update needed.
        }
    }
}
