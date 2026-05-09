// FloatingNumber.cs
// -----------------------------------------------------------------------------
// Self-animating floating combat number with parabolic-arc trajectory.
// Ported from Echoes of the Spire's UI/FloatingNumber.cs.
//
// Lifecycle:
//   1. Spawns at unit's foot in screen space (small — minScale), offset
//      horizontally along the hitbox width.
//   2. Rises to the unit's indicator anchor (top) along an ease-out parabola.
//      Scale grows from minScale → maxScale during the rise.
//   3. Falls from apex to foot Y on an ease-in (accelerating). Scale shrinks
//      back from maxScale → minScale, alpha fades to 40%.
//   4. Falloff: continues straight down past foot, scale shrinks further,
//      alpha fades to 0%.
//   5. Self-destroys when totalDuration elapses.
//
// All animation is in SCREEN space (pixel coordinates), so the popup stays
// crisp regardless of camera zoom. Driven by FloatingNumberManager.SpawnArc().
// Parented under the shared Combat (Screen Space - Overlay) canvas — no
// per-popup Canvas component.
// -----------------------------------------------------------------------------
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

        // Arc parameters (set by Setup)
        private Vector3 startScreenPos;
        private Vector3 apexScreenPos;
        private float footScreenY;
        private float riseTime;
        private float fallTime;
        private float falloffTime;
        private float minScale;
        private float maxScale;
        private float totalDuration;

        // Wobble (height-scaled horizontal sway)
        private float wobbleAmplitude;
        private float wobbleFrequency;
        private float wobblePhase;

        /// <summary>
        /// Full arc spawn — rise from foot to apex, fall back down past the
        /// foot. Caller pre-converts world positions to screen space.
        /// </summary>
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
                // Phase 1 — Rise (ease-out quad). Snappy launch, settling at apex.
                float t = elapsed / riseTime;
                float eased = 1f - (1f - t) * (1f - t);

                transform.position = Vector3.Lerp(startScreenPos, apexScreenPos, eased);
                transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, eased);
            }
            else if (elapsed <= riseTime + fallTime)
            {
                // Phase 2 — Fall (ease-in quad). Accelerates back toward foot Y,
                // shrinking maxScale → minScale and fading 1 → 0.4.
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
                // Phase 3 — Falloff. Straight down off-screen, shrinking past
                // minScale and fading to 0.
                float t = (elapsed - riseTime - fallTime) / falloffTime;
                Vector3 pos = new Vector3(apexScreenPos.x, footScreenY - t * 200f, 0f);
                transform.position = pos;
                transform.localScale = Vector3.one * Mathf.Lerp(minScale, minScale * 0.8f, t);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0.4f, 0f, t);
            }

            // Height-scaled wobble: Z-axis rotation that rocks left-right like
            // a knob, amplitude proportional to how far above the foot the
            // popup currently sits. Zero at foot, max near apex, decays on
            // the fall.
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
