using System.Collections;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class PhaseBannerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("CanvasGroup on the black bar Image. Faded in first.")]
        [SerializeField] private CanvasGroup bgGroup;
        [Tooltip("CanvasGroup wrapping both text labels. Faded in after the bar.")]
        [SerializeField] private CanvasGroup textGroup;
        [Tooltip("Larger top label — reads e.g. 'Round 1'.")]
        [SerializeField] private TMP_Text headerLabel;
        [Tooltip("Smaller bottom label — reads e.g. 'Player Phase'.")]
        [SerializeField] private TMP_Text phaseLabel;

        [Header("Timing")]
        [Tooltip("Seconds to fade the black bar from 0 → 1.")]
        [SerializeField] private float bgFadeDuration = 0.18f;
        [Tooltip("Seconds to fade the header text from 0 → 1 after the bar lands.")]
        [SerializeField] private float textFadeDuration = 0.18f;
        [Tooltip("Seconds to hold the fully-visible banner before fading out.")]
        [SerializeField] private float holdDuration = 0.7f;
        [Tooltip("Seconds for everything to fade back to 0.")]
        [SerializeField] private float fadeOutDuration = 0.25f;

        private void Awake()
        {
            // Snap hidden so the prefab can be authored visible for layout checks
            // without leaking through at runtime.
            if (bgGroup != null)
            {
                bgGroup.alpha = 0f;
                bgGroup.blocksRaycasts = false;
                bgGroup.interactable = false;
            }
            if (textGroup != null)
            {
                textGroup.alpha = 0f;
                textGroup.blocksRaycasts = false;
                textGroup.interactable = false;
            }
        }

        public IEnumerator Show(string header, string phase)
        {
            if (headerLabel != null) headerLabel.text = header ?? string.Empty;
            if (phaseLabel  != null) phaseLabel.text  = phase  ?? string.Empty;

            yield return Fade(bgGroup, 1f, bgFadeDuration);

            yield return Fade(textGroup, 1f, textFadeDuration);

            float t = 0f;
            while (t < holdDuration) { t += Time.deltaTime; yield return null; }

            yield return FadeBoth(0f, fadeOutDuration);
        }

        public float TotalDuration =>
            Mathf.Max(0f, bgFadeDuration) +
            Mathf.Max(0f, textFadeDuration) +
            Mathf.Max(0f, holdDuration) +
            Mathf.Max(0f, fadeOutDuration);

        // ── Internals ───────────────────────────────────────────────────────

        private static IEnumerator Fade(CanvasGroup cg, float to, float duration)
        {
            if (cg == null) yield break;
            float dur = Mathf.Max(0.0001f, duration);
            float from = cg.alpha;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = to;
        }

        private IEnumerator FadeBoth(float to, float duration)
        {
            float dur = Mathf.Max(0.0001f, duration);
            float fromBg   = bgGroup   != null ? bgGroup.alpha   : 0f;
            float fromText = textGroup != null ? textGroup.alpha : 0f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (bgGroup   != null) bgGroup.alpha   = Mathf.Lerp(fromBg,   to, k);
                if (textGroup != null) textGroup.alpha = Mathf.Lerp(fromText, to, k);
                yield return null;
            }
            if (bgGroup   != null) bgGroup.alpha   = to;
            if (textGroup != null) textGroup.alpha = to;
        }
    }
}
