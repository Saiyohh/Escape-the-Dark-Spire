using UnityEngine;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tooltip/Orb Tooltip Trigger")]
    public class OrbTooltipTrigger : WorldTooltipTrigger
    {
        // Cached reference; auto-resolved on Awake. Not serialized — the
        // OrbSlotView always lives on the same GameObject as this trigger
        // (both go on the slot prefab root), so there's no use case for
        // an inspector override.
        private OrbSlotView orbSlot;

        protected override void Awake()
        {
            base.Awake();
            orbSlot = GetComponent<OrbSlotView>();
        }

        protected override bool BuildContent(out TooltipContent content)
        {
            content = default;
            if (orbSlot == null) return false;

            var orb = orbSlot.BoundOrb;
            if (orb == null || orb.data == null) return false;

            content = TooltipContent.ForOrb(
                orb.data.passiveDescription, orb.data.evokeDescription);
            return true;
        }
    }
}
