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
