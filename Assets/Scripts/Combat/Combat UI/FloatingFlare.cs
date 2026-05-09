// FloatingFlare.cs
// -----------------------------------------------------------------------------
// Self-animating non-arc combat label. Used for condition apply / wear-off and
// the standalone status words MISS / DODGE / RESIST / CRIT! / +X SHLD.
//
// Lifecycle:
//   1. Fade-in: alpha 0 → 1 while position holds at startScreen.
//   2. Drift: ease-out toward startScreen + (0, ±driftDistance, 0). Up for
//      positive valence (gained buffs, wears-off, miss/crit/shields/etc.) and
//      Down for negative-condition-gained.
//   3. Fade-out: alpha 1 → 0 while drift continues at the eased rate.
//   4. Self-destroys.
//
// One component drives both prefab variants:
//   - Text-only flare (gained / miss / crit / dodge / resist / shields):
//     leaves `icon` and `subtitle` unwired on the prefab.
//   - Wears-off flare: wires `icon`, `subtitle`, and `label` for the
//     icon + name + "Wears Off" treatment.
//
// All animation is in SCREEN space (pixel coordinates) under the shared
// Combat (Screen Space - Overlay) canvas — no per-flare Canvas component.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public enum FlareDirection { Up, Down }

    public class FloatingFlare : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional. Wired only on the wears-off prefab variant.")]
        [SerializeField] private TMP_Text subtitle;
        [Tooltip("Optional. Wired only on the wears-off prefab variant.")]
        [SerializeField] private Image icon;
        [SerializeField] private CanvasGroup canvasGroup;

        private float elapsed;
        private bool initialized;

        private Vector3 startScreenPos;
        private Vector3 endScreenPos;
        private float fadeInTime;
        private float driftTime;
        private float fadeOutTime;
        private float totalDuration;

        public void Setup(string name, Color color, Vector3 startScreen,
            FlareDirection direction, Sprite iconSprite, string subtitleText,
            float driftDistance, float fadeIn, float drift, float fadeOut)
        {
            if (label != null)
            {
                label.text = name;
                label.color = color;
            }

            if (subtitle != null)
                subtitle.text = string.IsNullOrEmpty(subtitleText) ? string.Empty : subtitleText;

            if (icon != null)
            {
                if (iconSprite != null)
                {
                    icon.sprite = iconSprite;
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }

            startScreenPos = startScreen;
            float dy = direction == FlareDirection.Up ? driftDistance : -driftDistance;
            endScreenPos = startScreen + new Vector3(0f, dy, 0f);

            fadeInTime    = Mathf.Max(0.01f, fadeIn);
            driftTime     = Mathf.Max(0.01f, drift);
            fadeOutTime   = Mathf.Max(0.01f, fadeOut);
            totalDuration = fadeInTime + driftTime + fadeOutTime;

            transform.position   = startScreenPos;
            transform.localScale = Vector3.one;

            if (canvasGroup != null) canvasGroup.alpha = 0f;

            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;

            elapsed += Time.deltaTime;

            // Position: ease-out drift across the full lifetime (post fade-in
            // through fade-out), so motion continues smoothly while fading.
            float driftElapsed = Mathf.Clamp(elapsed - fadeInTime, 0f, driftTime + fadeOutTime);
            float driftT = Mathf.Clamp01(driftElapsed / (driftTime + fadeOutTime));
            float driftEased = 1f - (1f - driftT) * (1f - driftT);
            transform.position = Vector3.Lerp(startScreenPos, endScreenPos, driftEased);

            // Alpha: fade-in → hold → fade-out.
            if (canvasGroup != null)
            {
                if (elapsed <= fadeInTime)
                {
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInTime);
                }
                else if (elapsed <= fadeInTime + driftTime)
                {
                    canvasGroup.alpha = 1f;
                }
                else
                {
                    float t = (elapsed - fadeInTime - driftTime) / fadeOutTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t));
                }
            }

            if (elapsed >= totalDuration)
                Destroy(gameObject);
        }
    }
}
