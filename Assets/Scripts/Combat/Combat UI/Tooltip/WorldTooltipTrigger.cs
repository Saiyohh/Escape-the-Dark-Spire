using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class WorldTooltipTrigger : TooltipTrigger
    {
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
        }
    }
}
