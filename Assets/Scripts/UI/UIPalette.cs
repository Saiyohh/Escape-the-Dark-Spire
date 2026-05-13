using UnityEngine;

namespace DarkSpire
{
    public static class UIPalette
    {
        // Backgrounds.
        public static readonly Color SceneBackgroundDark   = new(0.05f, 0.05f, 0.07f);
        public static readonly Color PanelBackgroundNeutral = new(0.10f, 0.10f, 0.12f, 0.95f);
        public static readonly Color PanelBackdropDim      = new(0f, 0f, 0f, 0.5f);
        public static readonly Color BarBackground         = new(0f, 0f, 0f, 0.55f);

        // Text.
        public static readonly Color TextPrimary   = Color.white;
        public static readonly Color TextSecondary = new(0.8f, 0.8f, 0.85f);
        public static readonly Color TextMuted     = new(0.65f, 0.65f, 0.7f);

        // Accents.
        public static readonly Color AccentGold    = new(1f, 0.85f, 0.20f);
        public static readonly Color AccentGreen   = new(0.6f, 0.95f, 0.55f);
        public static readonly Color AccentRed     = new(0.95f, 0.4f, 0.4f);
        public static readonly Color AccentBlue    = new(0.30f, 0.55f, 0.90f);

        // Buttons.
        public static readonly Color ButtonNormal      = new(0.20f, 0.20f, 0.24f, 1f);
        public static readonly Color ButtonHighlighted = new(0.30f, 0.30f, 0.36f, 1f);
        public static readonly Color ButtonPressed     = new(0.14f, 0.14f, 0.18f, 1f);
        public static readonly Color ButtonDisabled    = new(0.12f, 0.12f, 0.14f, 0.6f);
    }
}
