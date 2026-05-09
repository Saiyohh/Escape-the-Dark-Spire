// TMPOutline.cs
// -----------------------------------------------------------------------------
// Runtime helpers for tweaking a TextMeshPro label's outline per-instance,
// without needing a separate font asset. Accessing `TMP_Text.fontMaterial`
// auto-clones the font's shared material for that one label, so writes to
// _OutlineColor / _OutlineWidth / _OutlineSoftness don't bleed into other
// labels using the same font.
//
// Property ID constants come from TMP's ShaderUtilities (avoids per-call
// Shader.PropertyToID lookups).
//
// Width / Softness are normalized [0, 1]. A width of ~0.2 is a typical
// readable outline; values near 1 get blobby. Softness 0 = crisp edge.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public static class TMPOutline
    {
        /// <summary>
        /// Apply an outline to a single TMP_Text. Uses the per-instance
        /// material so other labels sharing the font asset are unaffected.
        /// Re-applies mesh padding so thick outlines don't get clipped.
        /// Returns silently if the TMP hasn't finished initializing yet
        /// (e.g. during prefab import or when the font asset isn't assigned).
        ///
        /// faceDilate thickens the glyph's interior (range -1..+1). Pair with
        /// a thicker outline to keep the inner readable — e.g. outlineWidth =
        /// 0.35 + faceDilate = 0.15 gives a chunky bold feel without the face
        /// collapsing to a sliver.
        /// </summary>
        public static void Set(TMP_Text text, Color color, float width,
            float softness = 0f, float faceDilate = 0f)
        {
            if (!IsReady(text)) return;

            // fontMaterial property auto-creates an instance on first access.
            var mat = text.fontMaterial;
            if (mat == null) return;

            mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth,    Mathf.Clamp01(width));
            mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, Mathf.Clamp01(softness));
            mat.SetFloat(ShaderUtilities.ID_FaceDilate,      Mathf.Clamp(faceDilate, -1f, 1f));

            // Recalculate padding so wider outlines don't clip against the glyph
            // rect — especially important on UI canvases with masks.
            text.UpdateMeshPadding();
        }

        /// <summary>
        /// Set only the face dilate on a per-instance material. Use when you
        /// want to thicken/thin the glyph without touching outline values.
        /// Range -1..+1; 0 = default.
        /// </summary>
        public static void SetFaceDilate(TMP_Text text, float faceDilate)
        {
            if (!IsReady(text)) return;
            var mat = text.fontMaterial;
            if (mat == null) return;

            mat.SetFloat(ShaderUtilities.ID_FaceDilate, Mathf.Clamp(faceDilate, -1f, 1f));
            text.UpdateMeshPadding();
        }

        /// <summary>Convenience: kill the outline (width 0, color transparent).</summary>
        public static void Clear(TMP_Text text)
        {
            if (!IsReady(text)) return;
            var mat = text.fontMaterial;
            if (mat == null) return;

            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0, 0, 0, 0));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            text.UpdateMeshPadding();
        }

        /// <summary>
        /// Restore the label to the font asset's shared material (drops the
        /// per-instance clone). Call when you want the label back in sync
        /// with the font asset's authored outline.
        /// </summary>
        public static void ResetToShared(TMP_Text text)
        {
            if (text == null || text.font == null || text.font.material == null) return;
            text.fontSharedMaterial = text.font.material;
            text.UpdateMeshPadding();
        }

        /// <summary>
        /// Safe-guard: TMP can fire OnEnable / OnValidate before its font or
        /// shared material finish initializing (prefab import, scene open,
        /// missing font assignment). Accessing `fontMaterial` in that window
        /// throws UnassignedReferenceException. Check here first.
        /// </summary>
        private static bool IsReady(TMP_Text text)
        {
            return text != null
                && text.font != null
                && text.fontSharedMaterial != null;
        }
    }
}
