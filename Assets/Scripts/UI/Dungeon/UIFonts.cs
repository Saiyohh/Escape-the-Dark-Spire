// UIFonts.cs
// -----------------------------------------------------------------------------
// Single point for resolving the project's default UI font. Every runtime-built
// TMP text (flash messages, pickup toasts, [E] prompts, modal labels, the icon
// hover panel) calls UIFonts.Apply(tmp) after configuring its other properties
// so the look stays consistent across the whole exploration UI.
//
// The font lives on ClarityUIPrefabLibrary.defaultFont. If that's unset (or
// the library asset doesn't exist yet) Apply is a no-op and TMP's default
// font is used — nothing breaks, the look just isn't unified.
// -----------------------------------------------------------------------------
using TMPro;

namespace DarkSpire
{
    public static class UIFonts
    {
        public static TMP_FontAsset Default
        {
            get
            {
                var lib = ClarityUIPrefabLibrary.Instance;
                return lib != null ? lib.defaultFont : null;
            }
        }

        /// <summary>
        /// Assign the project's default UI font to <paramref name="label"/>.
        /// No-op if the library or its font reference is null.
        /// </summary>
        public static void Apply(TMP_Text label)
        {
            if (label == null) return;
            var font = Default;
            if (font != null) label.font = font;
        }
    }
}
