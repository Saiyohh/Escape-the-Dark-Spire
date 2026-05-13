// DangerAura.cs
// -----------------------------------------------------------------------------
// Per-targeted-player visual spawned by DangerPreviewController while the user
// hovers an enemy intent that threatens this player. Mirrors TurnIndicator's
// pattern exactly:
//   • Pure world-space SpriteRenderer for the aura (no Canvas).
//   • World-space TextMeshPro (MeshRenderer-based) for the % label.
//   • Reparents under the target's UnitDisplay so the aura rides along with
//     rank shifts, knockbacks, lunges, etc. (Same trick TurnIndicator uses.)
//   • Alpha pulse driven directly on renderer.color.a + label color.a, no
//     CanvasGroup. Sorting layer matches "Unit Overlays" stratum.
//
// Lifecycle is pool-owned by DangerPreviewController. Bind() refreshes
// content + parents under the player's display; Release() detaches back to
// the stable pool root and hides.
//
// Prefab structure (hand-authored):
//   DangerAura  (Transform, DangerAura)
//     ├─ Glow   (SpriteRenderer — aura sprite, pivot bottom-center; sorting
//                 layer 'Unit Overlays')
//     └─ Label  (TextMeshPro — world-space text mesh; same sorting layer,
//                 higher order so it sits in front of the glow; supports
//                 two lines)
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    [DisallowMultipleComponent]
    public class DangerAura : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("SpriteRenderer for the aura. Auto-found in children if blank.")]
        [SerializeField] private SpriteRenderer glow;
        [Tooltip("World-space TextMeshPro for the % label (use TextMeshPro, NOT TextMeshProUGUI).")]
        [SerializeField] private TextMeshPro label;

        [Header("Tints")]
        [Tooltip("Used when any attack-roll effect targets this player. Wins over afflict tint when both apply.")]
        [SerializeField] private Color attackTint  = new Color(0.85f, 0.25f, 0.25f, 1f);
        [Tooltip("Used when only afflict (save-based) effects target this player.")]
        [SerializeField] private Color afflictTint = new Color(0.65f, 0.30f, 0.85f, 1f);

        [Header("Pulse")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.50f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.95f;
        [SerializeField] private float pulseFrequency = 0.9f;

        [Header("Anchor")]
        [Tooltip("Local offset under the player's UnitDisplay. Unit pivot is bottom-center, so zero = feet.")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        [Header("Sorting")]
        [Tooltip("Sorting layer for both glow and label. Matches TurnIndicator convention.")]
        [SerializeField] private string sortingLayer = "Unit Overlays";
        [Tooltip("Sorting order for the glow (background of the stratum).")]
        [SerializeField] private int glowSortingOrder = 0;
        [Tooltip("Sorting order for the label (in front of the glow).")]
        [SerializeField] private int labelSortingOrder = 5;

        // Stable parent the aura returns to when not in use. Captured on Awake
        // so SetParent has somewhere safe to go on Release.
        private Transform stableRoot;

        // Current tint chosen at Bind (cached so the pulse can recolor without
        // re-resolving attack vs afflict tinting every frame).
        private Color currentTint;

        private void Reset()
        {
            if (glow == null)  glow  = GetComponentInChildren<SpriteRenderer>(true);
            if (label == null) label = GetComponentInChildren<TextMeshPro>(true);
        }

        private void Awake()
        {
            stableRoot = transform.parent;
            if (glow == null)  glow  = GetComponentInChildren<SpriteRenderer>(true);
            if (label == null) label = GetComponentInChildren<TextMeshPro>(true);

            ApplySorting();
            gameObject.SetActive(false);
        }

        private void ApplySorting()
        {
            if (glow != null)
            {
                glow.sortingLayerName = sortingLayer;
                glow.sortingOrder = glowSortingOrder;
            }
            if (label != null)
            {
                var mr = label.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sortingLayerName = sortingLayer;
                    mr.sortingOrder = labelSortingOrder;
                }
            }
        }

        // ─── Bind / Release ─────────────────────────────────────────────────

        /// <summary>Reset state and attach to the given player's UnitDisplay at the feet.</summary>
        public void Bind(in IntentTargetEntry entry, UnitDisplay display)
        {
            currentTint = entry.hasAttack
                ? attackTint
                : (entry.hasAfflict ? afflictTint : attackTint);

            if (glow  != null) glow.color  = currentTint;
            if (label != null)
            {
                label.text  = FormatLabel(in entry);
                label.color = new Color(currentTint.r, currentTint.g, currentTint.b, 1f);
            }

            if (display != null)
            {
                transform.SetParent(display.transform, worldPositionStays: false);
                transform.localPosition = localOffset;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            gameObject.SetActive(true);
        }

        /// <summary>Hide and return to the stable parent so the pool can re-issue it later.</summary>
        public void Release()
        {
            gameObject.SetActive(false);

            // Mirror TurnIndicator's defensive detach — Unity refuses
            // SetParent during a parent's deactivation, and the player
            // we were riding may be mid-destroy on a scene exit.
            if (stableRoot == null) return;
            var p = transform.parent;
            if (p == null || p.gameObject == null) return;
            transform.SetParent(stableRoot, worldPositionStays: false);
        }

        // ─── Pulse ──────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            float wave = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float a = Mathf.Lerp(minAlpha, maxAlpha, wave);

            if (glow != null)
            {
                var c = currentTint;
                c.a = a;
                glow.color = c;
            }
            // Label alpha tracks the same pulse but stays a touch brighter
            // so the text doesn't ghost as the aura breathes out.
            if (label != null)
            {
                var c = label.color;
                c.a = Mathf.Lerp(0.85f, 1f, wave);
                label.color = c;
            }
        }

        // ─── Label ──────────────────────────────────────────────────────────

        private static string FormatLabel(in IntentTargetEntry entry)
        {
            string attackLine = entry.hasAttack
                ? $"Hit {Mathf.RoundToInt(entry.hitPct * 100f)}%"
                : null;
            string afflictLine = entry.hasAfflict
                ? $"{AfflictName(entry.afflictCondition)} {Mathf.RoundToInt(entry.afflictPct * 100f)}%"
                : null;

            if (attackLine != null && afflictLine != null)
                return $"{attackLine}\n{afflictLine}";
            return attackLine ?? afflictLine ?? "";
        }

        private static string AfflictName(ConditionID id)
        {
            var lib = ConditionLibrary.Instance;
            var data = lib != null ? lib.Get(id) : null;
            if (data != null && !string.IsNullOrEmpty(data.displayName))
                return data.displayName;
            return "Afflict";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minAlpha > maxAlpha) (minAlpha, maxAlpha) = (maxAlpha, minAlpha);
            ApplySorting();
        }
#endif
    }
}
