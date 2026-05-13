using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public static class TMPOutline
    {
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

        public static void SetFaceDilate(TMP_Text text, float faceDilate)
        {
            if (!IsReady(text)) return;
            var mat = text.fontMaterial;
            if (mat == null) return;

            mat.SetFloat(ShaderUtilities.ID_FaceDilate, Mathf.Clamp(faceDilate, -1f, 1f));
            text.UpdateMeshPadding();
        }

        public static void Clear(TMP_Text text)
        {
            if (!IsReady(text)) return;
            var mat = text.fontMaterial;
            if (mat == null) return;

            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0, 0, 0, 0));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            text.UpdateMeshPadding();
        }

        public static void ResetToShared(TMP_Text text)
        {
            if (text == null || text.font == null || text.font.material == null) return;
            text.fontSharedMaterial = text.font.material;
            text.UpdateMeshPadding();
        }

        private static bool IsReady(TMP_Text text)
        {
            return text != null
                && text.font != null
                && text.fontSharedMaterial != null;
        }
    }
}
