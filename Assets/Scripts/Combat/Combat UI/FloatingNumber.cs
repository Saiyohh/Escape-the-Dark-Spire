using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class FloatingNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private CanvasGroup canvasGroup;

        private float elapsed;
        private bool initialized;

        private Vector3 startScreenPos;
        private Vector3 apexScreenPos;
        private float footScreenY;
        private float riseTime;
        private float fallTime;
        private float falloffTime;
        private float minScale;
        private float maxScale;
        private float totalDuration;

        private float wobbleAmplitude;
        private float wobbleFrequency;
        private float wobblePhase;

        public void Setup(string value, Color color,
            Vector3 startScreen, Vector3 apexScreen, float footY,
            float rise, float fall, float falloff,
            float scaleMin, float scaleMax,
            float wobbleAmp, float wobbleFreq)
        {
            if (text != null)
            {
                text.text = value;
                text.color = color;
            }

            startScreenPos = startScreen;
            apexScreenPos  = apexScreen;
            footScreenY    = footY;
            riseTime       = Mathf.Max(0.01f, rise);
            fallTime       = Mathf.Max(0.01f, fall);
            falloffTime    = Mathf.Max(0.01f, falloff);
            minScale       = scaleMin;
            maxScale       = scaleMax;
            totalDuration  = riseTime + fallTime + falloffTime;

            wobbleAmplitude = wobbleAmp;
            wobbleFrequency = wobbleFreq;
            wobblePhase     = Random.Range(0f, Mathf.PI * 2f);

            transform.position   = startScreenPos;
            transform.localScale = Vector3.one * minScale;

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;

            elapsed += Time.deltaTime;

            if (elapsed <= riseTime)
            {
                float t = elapsed / riseTime;
                float eased = 1f - (1f - t) * (1f - t);

                transform.position = Vector3.Lerp(startScreenPos, apexScreenPos, eased);
                transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, eased);
            }
            else if (elapsed <= riseTime + fallTime)
            {
                float t = (elapsed - riseTime) / fallTime;
                float eased = t * t;

                Vector3 fallTarget = new Vector3(apexScreenPos.x, footScreenY, 0f);
                transform.position = Vector3.Lerp(apexScreenPos, fallTarget, eased);
                transform.localScale = Vector3.one * Mathf.Lerp(maxScale, minScale, eased);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(1f, 0.4f, eased);
            }
            else
            {
                float t = (elapsed - riseTime - fallTime) / falloffTime;
                Vector3 pos = new Vector3(apexScreenPos.x, footScreenY - t * 200f, 0f);
                transform.position = pos;
                transform.localScale = Vector3.one * Mathf.Lerp(minScale, minScale * 0.8f, t);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0.4f, 0f, t);
            }

            if (wobbleAmplitude > 0f && wobbleFrequency > 0f)
            {
                float apexHeight = Mathf.Max(1f, apexScreenPos.y - footScreenY);
                float heightFactor = Mathf.Clamp01(
                    (transform.position.y - footScreenY) / apexHeight);
                float angle = Mathf.Sin(elapsed * wobbleFrequency * Mathf.PI * 2f + wobblePhase)
                              * wobbleAmplitude * heightFactor;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }

            if (elapsed >= totalDuration)
                Destroy(gameObject);
        }
    }
}
