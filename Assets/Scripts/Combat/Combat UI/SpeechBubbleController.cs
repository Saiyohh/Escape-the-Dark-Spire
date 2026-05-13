using System.Collections;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SpeechBubbleController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;

        [Header("Timing")]
        [Tooltip("Fade-in duration for the bubble background. Quick.")]
        [SerializeField] private float fadeInDuration  = 0.12f;

        [Tooltip("Fade-out duration. Slightly slower than fade-in for a softer exit.")]
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Tooltip("Delay between fade-in starting and the typewriter beginning. " +
                 "Lets the bubble pop in before text starts revealing.")]
        [SerializeField] private float typewriterDelay = 0.08f;

        [Tooltip("Typewriter speed in characters per second. Higher = faster.")]
        [SerializeField] private float typewriterCps   = 60f;

        [Tooltip("Seconds to hold on the fully-typed message before fading out.")]
        [SerializeField] private float holdDuration    = 2.0f;

        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Show(string message)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            StopAllCoroutines();
            StartCoroutine(ShowRoutine(message ?? string.Empty));
        }

        private IEnumerator ShowRoutine(string message)
        {
            // Reset state
            canvasGroup.alpha = 0f;
            if (label != null)
            {
                label.text = message;
                label.maxVisibleCharacters = 0;
            }

            // Fade in (kick off the typewriter mid-fade for a soft, layered feel)
            float t = 0f;
            float typewriterStartT = Mathf.Min(typewriterDelay, fadeInDuration);
            bool typewriterStarted = false;
            Coroutine typewriterCo = null;

            while (t < fadeInDuration)
            {
                canvasGroup.alpha = fadeCurve.Evaluate(Mathf.Clamp01(t / fadeInDuration));
                if (!typewriterStarted && t >= typewriterStartT)
                {
                    typewriterStarted = true;
                    typewriterCo = StartCoroutine(TypewriterRoutine(message));
                }
                t += Time.deltaTime;
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Edge case: very short fade-in finished before the typewriter started.
            if (!typewriterStarted)
                typewriterCo = StartCoroutine(TypewriterRoutine(message));

            // Wait for typewriter to finish, then hold.
            if (typewriterCo != null)
                yield return typewriterCo;

            yield return new WaitForSeconds(holdDuration);

            // Fade out — bubble + text fade together since both share the CanvasGroup.
            t = 0f;
            while (t < fadeOutDuration)
            {
                canvasGroup.alpha = 1f - fadeCurve.Evaluate(Mathf.Clamp01(t / fadeOutDuration));
                t += Time.deltaTime;
                yield return null;
            }
            canvasGroup.alpha = 0f;
            Destroy(gameObject);
        }

        private IEnumerator TypewriterRoutine(string message)
        {
            if (label == null || string.IsNullOrEmpty(message)) yield break;

            int total = message.Length;
            float charDuration = 1f / Mathf.Max(1f, typewriterCps);

            for (int i = 1; i <= total; i++)
            {
                label.maxVisibleCharacters = i;
                yield return new WaitForSeconds(charDuration);
            }
            label.maxVisibleCharacters = total;
        }
    }
}
