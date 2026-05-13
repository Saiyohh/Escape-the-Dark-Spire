using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    [AddComponentMenu("DarkSpire/Outline/UI Outline")]
    public class UIOutline : MonoBehaviour
    {
        [Header("Style Source")]
        [Tooltip("Profile asset to read color + width from when useOverride is false.")]
        public OutlineProfile profile;

        [Tooltip("Tag used to look up style on the profile.")]
        public OutlineTag outlineTag = OutlineTag.None;

        [Header("Per-Instance Override (optional)")]
        public bool useOverride;
        public Color overrideColor = Color.white;
        [Min(0f)] public float overrideWidthPixels = 2f;

        private Outline uguiOutline;

        private void OnEnable()
        {
            EnsureOutline();
            ApplyStyle();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            EnsureOutline();
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            if (uguiOutline == null) EnsureOutline();

            GetStyle(out Color color, out float widthPx);
            uguiOutline.effectColor = color;
            uguiOutline.effectDistance = new Vector2(widthPx, -widthPx);
            uguiOutline.useGraphicAlpha = true;
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

        private void GetStyle(out Color color, out float widthPx)
        {
            if (useOverride || profile == null)
            {
                color = overrideColor;
                widthPx = overrideWidthPixels;
                return;
            }
            var style = profile.GetStyle(outlineTag);
            color = style.color;
            widthPx = style.widthPixels;
        }

        private void EnsureOutline()
        {
            uguiOutline = GetComponent<Outline>();
            if (uguiOutline == null)
                uguiOutline = gameObject.AddComponent<Outline>();
        }
    }
}
