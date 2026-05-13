using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    [AddComponentMenu("DarkSpire/Outline/Sprite Outline")]
    public class SpriteOutline : MonoBehaviour
    {
        public const string ShaderName = "DarkSpire/Sprite";

        [Header("Style Source")]
        [Tooltip("Profile asset to read color + width from when useOverride is false.")]
        public OutlineProfile profile;

        [Tooltip("Tag used to look up style on the profile.")]
        public OutlineTag outlineTag = OutlineTag.None;

        [Header("Per-Instance Override (optional)")]
        public bool useOverride;
        public Color overrideColor = Color.white;
        [Min(0f)] public float overrideWidthPixels = 2f;

        [Header("Shader Params")]
        [Tooltip("0 = hard edge, higher values = feathered outline.")]
        [Range(0f, 4f)] public float softness = 0.5f;

        [Tooltip("Alpha value above which a sprite pixel counts as opaque when " +
                 "scanning for the silhouette boundary. Raise to ignore soft sprite " +
                 "edges, lower to pick them up.")]
        [Range(0.01f, 1f)] public float alphaThreshold = 0.3f;

        [Tooltip("0 = crisp, flat outline. Higher = dithered pencil-on-paper look " +
                 "(outline alpha and brightness wobble with world-anchored grain, " +
                 "same noise field as the body paper grain).")]
        [Range(0f, 1f)] public float outlineGrainStrength = 0.5f;

        [Tooltip("Relative scale of the outline grain vs. the body grain. 1 = same. " +
                 ">1 finer outline noise, <1 coarser.")]
        [Range(0.25f, 4f)] public float outlineGrainScale = 1f;

        [Tooltip("True = outline renders behind the sprite (shows through outside " +
                 "pixels). False = outline overlays the sprite's edge.")]
        public bool outlineBehind = true;

        [Tooltip("If true, auto-create a material with the outline shader when the " +
                 "assigned material doesn't use it.")]
        public bool autoAssignMaterial = true;

        private static readonly int IdOutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int IdOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int IdOutlineSoftness = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int IdOutlineAlphaThresh = Shader.PropertyToID("_OutlineAlphaThresh");
        private static readonly int IdOutlineGrainStrength = Shader.PropertyToID("_OutlineGrainStrength");
        private static readonly int IdOutlineGrainScale = Shader.PropertyToID("_OutlineGrainScale");
        private static readonly int IdOutlineBehind = Shader.PropertyToID("_OutlineBehind");

        private SpriteRenderer self;
        private MaterialPropertyBlock block;

        private void OnEnable()
        {
            self = GetComponent<SpriteRenderer>();
            EnsureOutlineMaterial();
            ApplyStyle();
        }

        private void OnDisable()
        {
            if (self != null && block != null)
            {
                block.SetFloat(IdOutlineWidth, 0f);
                self.SetPropertyBlock(block);
            }
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            self = GetComponent<SpriteRenderer>();
            EnsureOutlineMaterial();
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            if (self == null) self = GetComponent<SpriteRenderer>();
            if (block == null) block = new MaterialPropertyBlock();

            GetStyle(out Color color, out float widthPx);

            self.GetPropertyBlock(block);
            block.SetColor(IdOutlineColor, color);
            block.SetFloat(IdOutlineWidth, widthPx);
            block.SetFloat(IdOutlineSoftness, softness);
            block.SetFloat(IdOutlineAlphaThresh, alphaThreshold);
            block.SetFloat(IdOutlineGrainStrength, outlineGrainStrength);
            block.SetFloat(IdOutlineGrainScale, outlineGrainScale);
            block.SetFloat(IdOutlineBehind, outlineBehind ? 1f : 0f);
            self.SetPropertyBlock(block);
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

        public void SetOutlineEnabled(bool on)
        {
            if (block == null) block = new MaterialPropertyBlock();
            self.GetPropertyBlock(block);
            if (on)
            {
                GetStyle(out Color color, out float widthPx);
                block.SetColor(IdOutlineColor, color);
                block.SetFloat(IdOutlineWidth, widthPx);
            }
            else
            {
                block.SetFloat(IdOutlineWidth, 0f);
            }
            self.SetPropertyBlock(block);
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

        private void EnsureOutlineMaterial()
        {
            if (!autoAssignMaterial || self == null) return;
            var shader = Shader.Find(ShaderName);
            if (shader == null) return; // shader not compiled yet

            var mat = self.sharedMaterial;
            if (mat != null && mat.shader == shader) return;

            var found = FindProjectMaterialUsingOutlineShader(shader);
            if (found != null)
            {
                self.sharedMaterial = found;
            }
            else
            {
                self.sharedMaterial = new Material(shader) { name = "RuntimeSpriteOutlineMat" };
            }
        }

        private static Material FindProjectMaterialUsingOutlineShader(Shader shader)
        {
            return null;
        }
    }
}
