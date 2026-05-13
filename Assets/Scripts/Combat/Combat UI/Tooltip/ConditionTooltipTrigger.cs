using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("DarkSpire/Tooltip/Condition Tooltip Trigger")]
    public class ConditionTooltipTrigger : UITooltipTrigger
    {
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

            string body = ConditionDescriptionFormatter.Format(data, conditionIcon.Stacks);

            content = TooltipContent.ForCondition(data.displayName, data.icon, body);
            return true;
        }
    }
}
