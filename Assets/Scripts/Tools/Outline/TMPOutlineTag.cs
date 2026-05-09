// TMPOutlineTag.cs
// -----------------------------------------------------------------------------
// Tints a TMP text's outline per-instance without forking the SDF font asset.
//
// Instead of using TMP's inside-of-glyph `_OutlineColor` (which eats glyph
// face area), this drives the SDF shader's **Underlay** layer — a drop-shadow
// feature that renders BEHIND the glyph. By setting the underlay offset to
// zero and dilating outward, the underlay becomes a proper outside-only
// outline. The font asset itself is untouched; every TMP text using the same
// font keeps sharing its font texture. Only the per-instance material is
// modified (TMP_Text.fontMaterial is an auto-instanced copy of the shared
// material, so setting properties here doesn't leak to other text objects).
//
// Tag → (color, dilate) comes from the OutlineProfile, same pattern as
// SpriteOutline / UIOutline. Override fields let individual instances
// diverge, with a Reset-to-Default affordance in the custom editor.
//
// Reasons this works without a custom shader:
//   • TMP's UNDERLAY_ON shader keyword is already baked into the standard
//     Distance Field shader
//   • TMP materials already expose _UnderlayColor, _UnderlayOffsetX/Y,
//     _UnderlayDilate, _UnderlaySoftness — we just drive them
//   • fontMaterial is a per-instance override (Unity cheats it in via the
//     renderer), so edits don't propagate to other text
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    [AddComponentMenu("DarkSpire/Outline/TMP Outline Tag")]
    public class TMPOutlineTag : MonoBehaviour
    {
        [Header("Style Source")]
        public OutlineProfile profile;
        public OutlineTag outlineTag = OutlineTag.None;

        [Header("Per-Instance Override (optional)")]
        public bool useOverride;
        public Color overrideColor = Color.black;

        [Tooltip("TMP uses SDF-normalized dilate (0-1) rather than pixels. " +
                 "~0.3 gives a visible outside outline at most text sizes.")]
        [Range(0f, 1f)] public float overrideUnderlayDilate = 0.3f;

        [Header("Underlay Softness")]
        [Tooltip("0 = hard outline, higher values = blurred / glow-ish outline.")]
        [Range(0f, 1f)] public float softness = 0f;

        private TMP_Text text;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            ApplyStyle();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            text = GetComponent<TMP_Text>();
            ApplyStyle();
        }

        public void SetTag(OutlineTag newTag)
        {
            outlineTag = newTag;
            useOverride = false;
            ApplyStyle();
        }

        public void ResetOverride()
        {
            useOverride = false;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            GetStyle(out Color color, out float dilate);

            // fontMaterial is TMP's per-instance copy. Safe to mutate.
            var mat = text.fontMaterial;
            if (mat == null) return;

            // Turn underlay on — some materials start with it off.
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);

            // Underlay drives the OUTSIDE outline.
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);

            // Kill the default inside-glyph outline to avoid double-outlining.
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);

            // Force refresh if text is already rendered.
            text.UpdateMeshPadding();
        }

        private void GetStyle(out Color color, out float dilate)
        {
            if (useOverride || profile == null)
            {
                color = overrideColor;
                dilate = overrideUnderlayDilate;
                return;
            }
            var style = profile.GetStyle(outlineTag);
            color = style.color;
            dilate = style.underlayDilate;
        }
    }
}
