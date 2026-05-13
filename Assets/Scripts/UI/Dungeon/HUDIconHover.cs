using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(LayoutElement))]
    public class HUDIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Scale pop")]
        [Tooltip("Target scale multiplier when hovered. 1.15 = 15% larger.")]
        [SerializeField] private float hoverScale = 1.15f;

        [Tooltip("Seconds to ease from current scale to the target. " +
                 "Small values feel snappy; larger values feel floaty.")]
        [SerializeField] private float scaleLerpSeconds = 0.10f;

        [Header("Rotation shake")]
        [Tooltip("Total duration of the one-shot wobble after the cursor enters.")]
        [SerializeField] private float shakeDuration = 0.4f;

        [Tooltip("Peak rotation in degrees. The shake decays linearly from this " +
                 "amplitude down to 0 over shakeDuration.")]
        [SerializeField] private float shakeAmplitudeDeg = 8f;

        [Tooltip("Oscillation frequency in Hz. Higher = faster wiggle.")]
        [SerializeField] private float shakeFrequencyHz = 18f;

        private RectTransform rt;
        private Vector3 baseScale;
        private float currentScaleMul = 1f;
        private float targetScaleMul = 1f;
        private float shakeT = -1f; // negative = no active shake

        private void Awake()
        {
            rt = (RectTransform)transform;
            baseScale = rt.localScale;
        }

        private void OnEnable()
        {
            currentScaleMul = 1f;
            targetScaleMul = 1f;
            shakeT = -1f;
            ApplyScale();
            ApplyRotation(0f);
        }

        private void OnDisable()
        {
            currentScaleMul = 1f;
            targetScaleMul = 1f;
            shakeT = -1f;
            if (rt != null)
            {
                rt.localScale = baseScale;
                rt.localRotation = Quaternion.identity;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScaleMul = hoverScale;
            shakeT = 0f; // (re)trigger the one-shot shake
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScaleMul = 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (scaleLerpSeconds > 0.0001f)
                currentScaleMul = Mathf.Lerp(currentScaleMul, targetScaleMul,
                                             1f - Mathf.Exp(-dt / scaleLerpSeconds));
            else
                currentScaleMul = targetScaleMul;
            ApplyScale();

            if (shakeT >= 0f)
            {
                shakeT += dt;
                if (shakeT >= shakeDuration)
                {
                    shakeT = -1f;
                    ApplyRotation(0f);
                }
                else
                {
                    float decay = 1f - Mathf.Clamp01(shakeT / shakeDuration);
                    float angle = shakeAmplitudeDeg * decay *
                                  Mathf.Sin(shakeT * shakeFrequencyHz * Mathf.PI * 2f);
                    ApplyRotation(angle);
                }
            }
        }

        private void ApplyScale()
        {
            if (rt == null) return;
            rt.localScale = baseScale * currentScaleMul;
        }

        private void ApplyRotation(float degrees)
        {
            if (rt == null) return;
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
