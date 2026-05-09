// PhaseBannerUI.cs
// -----------------------------------------------------------------------------
// Thin black opaque banner with a header label, used to announce each phase
// transition during combat ("Round 1: Player Phase"). CombatManager calls
// Show(...) at every phase change and yields on the returned coroutine so the
// banner finishes its in / hold / out cycle before the next phase starts.
//
// Animation contract (default tunables, configurable via inspector):
//   1. Fade in the black bar  (bgFadeDuration)
//   2. Fade in the text label (textFadeDuration)
//   3. Hold visible           (holdDuration)
//   4. Fade everything out    (fadeOutDuration)
// Total ≈ bgFade + textFade + hold + fadeOut.
//
// Prefab setup:
//   PhaseBanner (GameObject — child of CombatUIManager.combatCanvas)
//     RectTransform anchored full-width, fixed thin height (e.g. 120 px)
//     CanvasGroup (root — fades the whole strip in/out)
//     ├─ Bg (Image, opaque black) — its own CanvasGroup so the bar can fade
//     │                              in BEFORE the text
//     └─ TextRoot (CanvasGroup — fades both labels together AFTER the bar)
//        ├─ HeaderLabel (TMP_Text, larger — "Round 1")
//        └─ PhaseLabel  (TMP_Text, smaller — "Player Phase")
// Wire bgGroup, textGroup, headerLabel, and phaseLabel below.
// -----------------------------------------------------------------------------
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

        /// <summary>
        /// Run the in/hold/out animation with the given header + phase text.
        /// Yields for the full animation duration; CombatManager.CombatLoop
        /// calls `yield return banner.Show(...)` at every phase change.
        /// </summary>
        public IEnumerator Show(string header, string phase)
        {
            if (headerLabel != null) headerLabel.text = header ?? string.Empty;
            if (phaseLabel  != null) phaseLabel.text  = phase  ?? string.Empty;

            // 1. Fade in the black bar.
            yield return Fade(bgGroup, 1f, bgFadeDuration);

            // 2. Fade in the text.
            yield return Fade(textGroup, 1f, textFadeDuration);

            // 3. Hold.
            float t = 0f;
            while (t < holdDuration) { t += Time.deltaTime; yield return null; }

            // 4. Fade everything out together.
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
