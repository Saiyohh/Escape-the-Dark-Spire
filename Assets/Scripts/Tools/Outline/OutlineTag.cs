// OutlineTag.cs
// -----------------------------------------------------------------------------
// Categorical tag used by the outline system to pick a color + width preset
// from the OutlineProfile asset. Renderers that need an outline (SpriteOutline,
// UIOutline, TMPOutlineTag) expose a tag field; the profile lookup handles
// the rest.
//
// Add new tags as gameplay needs arise. The enum is designed to be extended —
// existing profile assets get a fallback (the profile's default style) for any
// tag value that doesn't have an explicit override.
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    public enum OutlineTag
    {
        None,
        Ally,
        Enemy,
        Interactable,
        Hover,
        Selected,
        Objective,
        Reward,
        Danger,
        Locked,
    }
}
