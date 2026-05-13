using UnityEngine;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tooltip/Static Tooltip Trigger")]
    public class StaticTooltipTrigger : UITooltipTrigger
    {
        [Header("Header (optional)")]
        [Tooltip("Header text. Leave empty to render the tooltip with no " +
                 "header row at all.")]
        [SerializeField] private string headerText;

        [Tooltip("Icon shown beside the header text. Leave null to hide the " +
                 "icon slot but keep the text.")]
        [SerializeField] private Sprite headerIcon;

        [Header("Body")]
        [TextArea(2, 6)]
        [SerializeField] private string bodyText;

        public void SetContent(string name, Sprite icon, string body)
        {
            headerText = name;
            headerIcon = icon;
            bodyText = body;
        }

        protected override bool BuildContent(out TooltipContent content)
        {
            if (string.IsNullOrEmpty(headerText) && string.IsNullOrEmpty(bodyText))
            {
                content = default;
                return false;
            }
            content = TooltipContent.ForStatic(headerText, headerIcon, bodyText);
            return true;
        }
    }
}
