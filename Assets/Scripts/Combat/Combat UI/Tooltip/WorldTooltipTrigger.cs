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
