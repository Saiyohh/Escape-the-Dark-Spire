using System.Collections;
using UnityEngine;

namespace DarkSpire
{
    public class CombatBarsAnimator : MonoBehaviour
    {
        [Header("Bars")]
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.35f;
        [SerializeField] private AnimationCurve curve =
            new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Brief pause before slide-in starts so the scene transition has time to clear.")]
        [SerializeField] private float slideInDelay = 0.1f;

        [Tooltip("Extra pixels added to the off-screen offset so partial visibility never " +
                 "peeks during the slide.")]
        [SerializeField] private float offscreenBuffer = 50f;

        [Tooltip("If true, bars are placed in the hidden state on Awake and only become " +
                 "visible when OnCombatStart fires. Disable for editor-test scenes that " +
                 "want the bars visible immediately.")]
        [SerializeField] private bool startHidden = true;

        [Tooltip("If true, the bars slide off-screen the instant CombatEvents.OnCombatEnd " +
                 "fires. Default false — we want the bars to stay on screen through the " +
                 "killing-blow death animation, floating numbers, and the post-combat " +
                 "settle delay. The scene transition overlay slides up over the whole " +
                 "scene at the end of that delay and covers the bars along with everything " +
                 "else, so an explicit slide-out is redundant and just makes the UI " +
                 "vanish too early.")]
        [SerializeField] private bool slideOutOnCombatEnd = false;

        private Vector2 topShown, topHidden;
        private Vector2 bottomShown, bottomHidden;
        private bool captured;

        private void Awake()
        {
            Capture();
            if (startHidden) HideInstant();
        }

        private void OnEnable()
        {
            CombatEvents.OnCombatStart += HandleCombatStart;
            CombatEvents.OnCombatEnd   += HandleCombatEnd;
        }

        private void OnDisable()
        {
            CombatEvents.OnCombatStart -= HandleCombatStart;
            CombatEvents.OnCombatEnd   -= HandleCombatEnd;
        }

        private void Capture()
        {
            if (captured) return;
            if (topBar != null)
            {
                topShown = topBar.anchoredPosition;
                topHidden = topShown + new Vector2(0f, topBar.rect.height + offscreenBuffer);
            }
            if (bottomBar != null)
            {
                bottomShown = bottomBar.anchoredPosition;
                bottomHidden = bottomShown - new Vector2(0f, bottomBar.rect.height + offscreenBuffer);
            }
            captured = true;
        }

        private void HandleCombatStart() => SlideIn();
        private void HandleCombatEnd(bool _)
        {
            // Default: do nothing on combat end — the scene transition overlay
            // covers the whole scene a few seconds later (CombatBootstrap.
            // returnDelay), and an early slide-out would yank the action bar /
            // top bar off screen mid-death-animation. Editor-test scenes (no
            // scene swap) can opt into the slide via slideOutOnCombatEnd.
            if (slideOutOnCombatEnd) SlideOut();
        }

        public void SlideIn()
        {
            Capture();
            StopAllCoroutines();
            if (topBar != null)
                StartCoroutine(SlideRect(topBar, topBar.anchoredPosition, topShown, slideInDelay));
            if (bottomBar != null)
                StartCoroutine(SlideRect(bottomBar, bottomBar.anchoredPosition, bottomShown, slideInDelay));
        }

        public void SlideOut()
        {
            Capture();
            StopAllCoroutines();
            if (topBar != null)
                StartCoroutine(SlideRect(topBar, topBar.anchoredPosition, topHidden, 0f));
            if (bottomBar != null)
                StartCoroutine(SlideRect(bottomBar, bottomBar.anchoredPosition, bottomHidden, 0f));
        }

        public void HideInstant()
        {
            Capture();
            if (topBar != null) topBar.anchoredPosition = topHidden;
            if (bottomBar != null) bottomBar.anchoredPosition = bottomHidden;
        }

        public void ShowInstant()
        {
            Capture();
            if (topBar != null) topBar.anchoredPosition = topShown;
            if (bottomBar != null) bottomBar.anchoredPosition = bottomShown;
        }

        private IEnumerator SlideRect(RectTransform rt, Vector2 from, Vector2 to, float delay)
        {
            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay) { d += Time.unscaledDeltaTime; yield return null; }
            }
            float t = 0f;
            rt.anchoredPosition = from;
            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = curve.Evaluate(Mathf.Clamp01(t / slideDuration));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            rt.anchoredPosition = to;
        }
    }
}
