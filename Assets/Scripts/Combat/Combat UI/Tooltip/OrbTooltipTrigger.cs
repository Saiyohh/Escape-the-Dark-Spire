using UnityEngine;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tooltip/Orb Tooltip Trigger")]
    public class OrbTooltipTrigger : WorldTooltipTrigger
    {
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
