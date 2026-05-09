// StaticTooltipTrigger.cs
// -----------------------------------------------------------------------------
// Drop-in tooltip trigger whose content is authored entirely in the inspector.
// Use on UI elements that don't have a backing data SO yet — chance icons,
// the Stars HUD widget, etc.
//
// If you want to populate a static trigger at runtime instead of authoring
// in-place, call SetContent(name, icon, body) — useful if a parent component
// wants to feed text dynamically (e.g. a chance icon that needs to say
// "Attack — 75%").
// -----------------------------------------------------------------------------
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
            // Suppress the tooltip entirely if there's nothing to display —
            // no header AND no body means the panel would be empty.
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
