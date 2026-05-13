using TMPro;
using UnityEngine;

namespace DarkSpire
{
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class TMPOutlineController : MonoBehaviour
    {
        [Tooltip("TMP target. Auto-populated from this GameObject if left empty.")]
        [SerializeField] private TMP_Text target;

        [SerializeField] private Color outlineColor = Color.black;

        [Tooltip("Outline thickness. Normalized 0..1 — ~0.2 reads well at typical " +
                 "font sizes; values near 1 get blobby.")]
        [Range(0f, 1f)] [SerializeField] private float outlineWidth = 0.2f;

        [Tooltip("Outline edge softness. 0 = crisp, 1 = very diffuse glow.")]
        [Range(0f, 1f)] [SerializeField] private float outlineSoftness = 0f;

        [Tooltip("Face dilate — thickens (+) or thins (-) the glyph interior. " +
                 "Pair positive values with a thicker outline so the letter's face " +
                 "doesn't collapse. Range -1..+1. 0 = font asset default.")]
        [Range(-1f, 1f)] [SerializeField] private float faceDilate = 0f;

        [Header("Editor Preview")]
        [Tooltip("When ON, outline applies in edit mode for live preview. Side " +
                 "effect: TMPs on scene instances pick up a material-instance " +
                 "override you can Revert but not Apply-to-prefab. Leave OFF for " +
                 "clean prefab workflow — values still apply at runtime via " +
                 "OnEnable, and you can preview manually with the context menu.")]
        [SerializeField] private bool livePreviewInEditMode = false;

        private void OnEnable()
        {
            // Always apply at runtime; conditionally in edit mode.
            if (Application.isPlaying || livePreviewInEditMode)
                Apply();
        }

        private void OnValidate()
        {
            // Only respond to inspector edits when the user opted in. Play mode
            // doesn't route through OnValidate for runtime slider drives — call
            // SetOutline / SetFaceDilate instead.
            if (livePreviewInEditMode)
                Apply();
        }

        public void Apply()
        {
            if (this == null) return;
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) return;

            if (target.font == null || target.fontSharedMaterial == null)
            {
                return;
            }

            TMPOutline.Set(target, outlineColor, outlineWidth, outlineSoftness, faceDilate);
        }

        public void SetOutline(Color color, float width, float softness = 0f, float face = 0f)
        {
            outlineColor    = color;
            outlineWidth    = Mathf.Clamp01(width);
            outlineSoftness = Mathf.Clamp01(softness);
            faceDilate      = Mathf.Clamp(face, -1f, 1f);
            Apply();
        }

        public void SetFaceDilate(float dilate)
        {
            faceDilate = Mathf.Clamp(dilate, -1f, 1f);
            Apply();
        }

        public void ClearOutline()
        {
            outlineColor = new Color(0, 0, 0, 0);
            outlineWidth = 0f;
            faceDilate   = 0f;
            Apply();
        }

    }
}
