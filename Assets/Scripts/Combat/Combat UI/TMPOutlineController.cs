// TMPOutlineController.cs
// -----------------------------------------------------------------------------
// Drop on a GameObject with a TMP_Text component (or assign target manually)
// to expose outline Color / Width / Softness / Face Dilate as inspector
// fields, driven through TMPOutline.Set so writes stay per-instance.
//
// Tension to understand:
//   • TMP's fontMaterial getter PROMOTES the text's material to a scene-
//     local instance on first access. That instance is serialized on the
//     TMP component and shows as a prefab override you CAN'T push (the
//     material itself isn't a project asset).
//   • But writing the outline values is what makes them visible.
//
// This controller lets you pick the trade-off:
//   livePreviewInEditMode = false (default): values serialize on this
//     component and apply at runtime (OnEnable in play mode) or on demand
//     via the "Preview Outline" context menu. No edit-mode override
//     pollution. Nested-prefab friendly.
//   livePreviewInEditMode = true: applies in OnValidate / OnEnable during
//     edit mode. You see the outline live but scene-instance TMPs pick up
//     a non-pushable material override. Revert the TMP to clear.
//
// Recommended workflow:
//   Keep livePreviewInEditMode = false. Edit the prefab asset directly
//   (double-click to enter Prefab Stage) when you want to tune values
//   against the visible result. Use the context menu for one-off previews
//   in scenes.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        /// <summary>
        /// Push the current slider values to the TMP's per-instance material.
        /// Safe to call from any callback timing.
        /// </summary>
        public void Apply()
        {
            if (this == null) return;
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) return;

            if (target.font == null || target.fontSharedMaterial == null)
            {
#if UNITY_EDITOR
                EditorApplication.delayCall += DeferredApply;
#endif
                return;
            }

            TMPOutline.Set(target, outlineColor, outlineWidth, outlineSoftness, faceDilate);
        }

#if UNITY_EDITOR
        private void DeferredApply()
        {
            if (this == null) return;
            if (target == null) return;
            Apply();
        }
#endif

        /// <summary>
        /// Drive the outline from code. Applies immediately regardless of the
        /// livePreviewInEditMode flag — use for transient runtime effects
        /// (flash red on hit, pulse color, etc.).
        /// </summary>
        public void SetOutline(Color color, float width, float softness = 0f, float face = 0f)
        {
            outlineColor    = color;
            outlineWidth    = Mathf.Clamp01(width);
            outlineSoftness = Mathf.Clamp01(softness);
            faceDilate      = Mathf.Clamp(face, -1f, 1f);
            Apply();
        }

        /// <summary>Drive only the face dilate; leave outline values untouched.</summary>
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

#if UNITY_EDITOR
        /// <summary>
        /// Force-apply the outline once in edit mode, regardless of the
        /// livePreviewInEditMode flag. Leaves a material-instance override
        /// on the TMP until you Revert it — useful for a quick visual check.
        /// </summary>
        [ContextMenu("Preview Outline (one-shot)")]
        private void PreviewOutlineOnce()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) return;
            TMPOutline.Set(target, outlineColor, outlineWidth, outlineSoftness, faceDilate);
        }

        /// <summary>
        /// Drop the TMP's per-instance material override and re-point it at
        /// the font asset's shared material.
        /// </summary>
        [ContextMenu("Reset TMP Material to Font")]
        private void ResetTMPMaterialToFont()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            TMPOutline.ResetToShared(target);
        }
#endif
    }
}
