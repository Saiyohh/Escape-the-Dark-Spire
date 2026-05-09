// CharacterPalette.cs
// -----------------------------------------------------------------------------
// Default signature + highlight colors per Alignment. Runtime-accessible so
// UI layers (future HUD, damage number tints, portrait borders, callout text)
// can share the same palette as the editor inspectors.
//
// "Signature" = the character's primary color, fully saturated. Designed for
// use as a solid background behind white text (callouts, badges, name plates).
//
// "Highlight" = a lighter variant for background fills, selection glows,
// subtle tints. Readable as a soft accent that doesn't dominate.
//
// Individual CharacterData assets override these defaults via their own
// signatureColor / highlightColor fields. CharacterLibrary.Instance.Get(alignment)
// returns the authored CharacterData at runtime; if no asset exists for an
// alignment, call sites fall back to DefaultSignature/Highlight here.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public static class CharacterPalette
    {
        /// <summary>Fully-saturated primary color. Solid background + white text works.</summary>
        public static Color DefaultSignature(Alignment a) => a switch
        {
            Alignment.Ironclad    => new Color(0.85f, 0.28f, 0.26f),  // red
            Alignment.Silent      => new Color(0.35f, 0.70f, 0.42f),  // green
            Alignment.Defect      => new Color(0.32f, 0.58f, 0.86f),  // blue
            Alignment.Necrobinder => new Color(0.62f, 0.41f, 0.78f),  // purple
            Alignment.Regent      => new Color(0.92f, 0.50f, 0.76f),  // pink
            Alignment.Nightsaint  => new Color(0.95f, 0.62f, 0.26f),  // orange
            _                     => new Color(0.60f, 0.60f, 0.60f),  // neutral gray
        };

        /// <summary>Lighter variant for highlights, fills, soft tints. ~35% lerped to white.</summary>
        public static Color DefaultHighlight(Alignment a)
        {
            var sig = DefaultSignature(a);
            return Color.Lerp(sig, Color.white, 0.55f);
        }
    }
}
