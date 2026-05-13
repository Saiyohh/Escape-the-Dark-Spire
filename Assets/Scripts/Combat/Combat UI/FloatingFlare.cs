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

            float driftElapsed = Mathf.Clamp(elapsed - fadeInTime, 0f, driftTime + fadeOutTime);
            float driftT = Mathf.Clamp01(driftElapsed / (driftTime + fadeOutTime));
            float driftEased = 1f - (1f - driftT) * (1f - driftT);
            transform.position = Vector3.Lerp(startScreenPos, endScreenPos, driftEased);

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
