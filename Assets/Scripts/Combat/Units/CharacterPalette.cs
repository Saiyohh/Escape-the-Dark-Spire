using UnityEngine;

namespace DarkSpire
{
    public static class CharacterPalette
    {
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

        public static Color DefaultHighlight(Alignment a)
        {
            var sig = DefaultSignature(a);
            return Color.Lerp(sig, Color.white, 0.55f);
        }
    }
}
