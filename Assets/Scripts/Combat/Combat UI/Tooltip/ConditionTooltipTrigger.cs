// ConditionTooltipTrigger.cs
// -----------------------------------------------------------------------------
// Tooltip trigger that pulls its content from the ConditionIconUI on the
// same GameObject. Reads the bound ConditionData and renders the canonical
// condition tooltip:
//   header  = ConditionData.displayName + ConditionData.icon
//   body    = ConditionData.description
//
// If the icon hasn't been bound yet (Bind has not been called), the tooltip
// is suppressed — no point flashing an empty panel on a stale or unfilled
// slot.
//
// Authoring is zero-touch — drop the component on the ConditionIcon prefab
// and the trigger finds:
//   • ConditionIconUI → GetComponent on the same GameObject
//   • SpawnPoint      → GetComponentInParent on the unit's HUD container
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("DarkSpire/Tooltip/Condition Tooltip Trigger")]
    public class ConditionTooltipTrigger : UITooltipTrigger
    {
        // Cached reference; auto-resolved on Awake. Not serialized — the
        // ConditionIconUI always lives on the same GameObject as this trigger
        // (both go on the ConditionIcon prefab root).
        private ConditionIconUI conditionIcon;

        protected override void Awake()
        {
            base.Awake();
            conditionIcon = GetComponent<ConditionIconUI>();
        }

        protected override bool BuildContent(out TooltipContent content)
        {
            content = default;
            if (conditionIcon == null) return false;

            var data = conditionIcon.Data;
            if (data == null) return false;

            content = TooltipContent.ForCondition(
                data.displayName, data.icon, data.description);
            return true;
        }
    }
}
