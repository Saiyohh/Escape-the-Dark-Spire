// OrbTooltipTrigger.cs
// -----------------------------------------------------------------------------
// Tooltip trigger for the Defect's orb tray. The orb tray is world-space
// (OrbSlotsUI reparents under the bearer's UnitDisplay so it follows rank
// shifts), and per UnitDisplay's notes the project runs the new Input
// System — so neither OnMouseEnter nor IPointerEnterHandler fires on these
// SpriteRenderer slots without a Physics2DRaycaster setup.
//
// Inheriting from WorldTooltipTrigger gives us the right detection mode:
// each frame, poll the orb slot's own Collider2D against the cursor world
// position. The actual placement + stacking is delegated to the orb tray's
// TooltipSpawnPoint, which is auto-resolved via GetComponentInParent — so
// every orb in the tray fires its tooltip into the same anchored stack.
//
// Renders the canonical orb tooltip per spec:
//   • NO header (orbs are visually distinct enough that the icon itself
//     identifies them; the GDD calls for showing only Passive/Evoke text).
//   • body = "Passive: …" + "Evoke: …" from OrbDataSO.
//
// Authoring is intentionally zero-touch — drop the component on the slot
// prefab and that's it. The component finds:
//   • OrbSlotView   → GetComponent on the same GameObject (where it lives)
//   • Collider2D    → required by WorldTooltipTrigger; auto-added by Unity
//                     if missing (size it to the sprite)
//   • SpawnPoint    → GetComponentInParent (lives on OrbSlotsUI tray root)
//   • Camera        → Camera.main
// -----------------------------------------------------------------------------
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
