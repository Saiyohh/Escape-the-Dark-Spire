// TooltipContent.cs
// -----------------------------------------------------------------------------
// Plain payload describing what a tooltip should display. Triggers build one
// of these in BuildContent() and hand it to the TooltipController; the View
// reads it and toggles its sub-elements accordingly.
//
// Two body modes:
//   • Single block — set BodyText. Used by conditions, chance icons, stars.
//   • Passive/Evoke — set PassiveText AND EvokeText (BodyText ignored). Used
//     by Defect orbs; the View renders two labeled paragraphs.
//
// Header mode:
//   • If HeaderText is null/empty, the entire header row is hidden — that's
//     the orb path (per spec, orbs have no header at all).
//   • HeaderIcon is optional; if null the icon slot is hidden but the text
//     still shows.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public struct TooltipContent
    {
        public string HeaderText;
        public Sprite HeaderIcon;

        public string BodyText;

        public string PassiveText;
        public string EvokeText;

        public bool HasHeader => !string.IsNullOrEmpty(HeaderText);

        // Two-block mode kicks in whenever either passive or evoke text is set.
        // Conditions / chance / stars leave both empty and use BodyText.
        public bool HasPassiveEvoke =>
            !string.IsNullOrEmpty(PassiveText) || !string.IsNullOrEmpty(EvokeText);

        public static TooltipContent ForCondition(string name, Sprite icon, string description)
            => new TooltipContent { HeaderText = name, HeaderIcon = icon, BodyText = description };

        public static TooltipContent ForOrb(string passive, string evoke)
            => new TooltipContent { PassiveText = passive, EvokeText = evoke };

        public static TooltipContent ForStatic(string name, Sprite icon, string description)
            => new TooltipContent { HeaderText = name, HeaderIcon = icon, BodyText = description };
    }
}
