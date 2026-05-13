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

            // Resolve {X}/{stacks}/{total} placeholders against the icon's live
            // stack count. Lets one authored description ("Increases DEF by {X}
            // for this round.") serve both the keyword tooltip in skill
            // descriptions (stacks=0 path) and the live unit-icon tooltip here.
            string body = ConditionDescriptionFormatter.Format(data, conditionIcon.Stacks);

            content = TooltipContent.ForCondition(data.displayName, data.icon, body);
            return true;
        }
    }
}
