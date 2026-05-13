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

        private Transform stableRoot;

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

        public void Release()
        {
            gameObject.SetActive(false);

            if (stableRoot == null) return;
            var p = transform.parent;
            if (p == null || p.gameObject == null) return;
            transform.SetParent(stableRoot, worldPositionStays: false);
        }

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
            if (label != null)
            {
                var c = label.color;
                c.a = Mathf.Lerp(0.85f, 1f, wave);
                label.color = c;
            }
        }

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

    }
}
