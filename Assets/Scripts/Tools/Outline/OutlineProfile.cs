// OutlineProfile.cs
// -----------------------------------------------------------------------------
// ScriptableObject mapping OutlineTag → (color, widthPixels). Single source of
// truth for every outlined object in the project. Drop one asset into
// `Assets/Scriptable Objects/` and every SpriteOutline / UIOutline /
// TMPOutlineTag with a matching tag will read its style from it.
//
// Structure:
//   • defaultColor / defaultWidthPixels — fallback when a tag has no override
//   • tagOverrides[] — explicit per-tag styles (sparse; missing tags use defaults)
//
// Designers use the custom inspector to:
//   • Edit the default style once
//   • "Reset to default" any tag override back to the defaults
// -----------------------------------------------------------------------------
using System;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "OutlineProfile", menuName = "DarkSpire/Outline Profile")]
    public class OutlineProfile : ScriptableObject
    {
        [Header("Defaults")]
        [Tooltip("Fallback color used for any tag without an explicit override.")]
        public Color defaultColor = Color.white;

        [Tooltip("Fallback width (in source pixels) used for any tag without an explicit override.")]
        [Min(0f)] public float defaultWidthPixels = 2f;

        [Tooltip("Fallback TMP underlay dilate (0-1) used for any text-outline tag without " +
                 "an explicit override. TMP uses normalized SDF units, not pixels.")]
        [Range(0f, 1f)] public float defaultUnderlayDilate = 0.3f;

        [Header("Per-Tag Overrides")]
        public TagStyle[] tagOverrides = System.Array.Empty<TagStyle>();

        /// <summary>Returns the style for the given tag, falling back to defaults.</summary>
        public TagStyle GetStyle(OutlineTag tag)
        {
            if (tagOverrides != null)
            {
                for (int i = 0; i < tagOverrides.Length; i++)
                    if (tagOverrides[i].tag == tag) return tagOverrides[i];
            }
            return new TagStyle
            {
                tag = tag,
                color = defaultColor,
                widthPixels = defaultWidthPixels,
                underlayDilate = defaultUnderlayDilate,
            };
        }

        /// <summary>One style row. Pixel width is used by sprite/UI outlines;
        /// underlay dilate is used by TMP text (different unit system).</summary>
        [Serializable]
        public struct TagStyle
        {
            public OutlineTag tag;
            public Color color;
            [Min(0f)] public float widthPixels;
            [Range(0f, 1f)] public float underlayDilate;
        }
    }
}
