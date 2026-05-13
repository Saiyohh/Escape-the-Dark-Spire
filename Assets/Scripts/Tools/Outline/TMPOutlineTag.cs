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
