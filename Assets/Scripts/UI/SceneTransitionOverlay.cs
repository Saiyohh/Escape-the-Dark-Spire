using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DarkSpire
{
    public class SceneTransitionOverlay : MonoBehaviour
    {
        public static SceneTransitionOverlay Instance { get; private set; }

        [Header("Panel")]
        [Tooltip("Full-screen black panel. Authored position should fully cover the canvas; " +
                 "the script computes the off-screen position from this rect's height.")]
        [SerializeField] private RectTransform blackPanel;

        [Header("Anchored Positions")]
        [Tooltip("Panel anchoredPosition while COVERING the canvas. (0, 0) for " +
                 "an anchor-stretched full-screen panel authored at the canvas center.")]
        [SerializeField] private Vector2 coveredAnchoredPos = Vector2.zero;

        [Tooltip("Panel anchoredPosition while UNCOVERED (off-screen below). " +
                 "For a 1080-tall canvas that's typically (0, -1080).")]
        [SerializeField] private Vector2 uncoveredAnchoredPos = new Vector2(0f, -1080f);

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.4f;
        [SerializeField] private AnimationCurve curve =
            new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Optional brief hold while covered before loading the scene. Lets the " +
                 "cover animation read clearly even on fast machines.")]
        [SerializeField] private float coverHoldSeconds = 0.05f;

        private bool inFlight;
        private bool waitingForLoad;

        // ─── Public API ──────────────────────────────────────────────────

        public static SceneTransitionOverlay GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>("SceneTransitionOverlay");
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<SceneTransitionOverlay>();
        }

        public Coroutine LoadSceneTransition(string sceneName)
        {
            if (inFlight)
            {
                Debug.LogWarning($"[SceneTransitionOverlay] Already mid-transition; dropping request for '{sceneName}'.");
                return null;
            }
            return StartCoroutine(TransitionRoutine(sceneName));
        }

        public Coroutine FadeOut() => StartCoroutine(SlideTo(coveredAnchoredPos));
        public Coroutine FadeIn()  => StartCoroutine(SlideTo(uncoveredAnchoredPos));

        // ─── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            // Snap to off-screen at boot. Both target positions are authored
            // in the inspector, so this works regardless of layout timing.
            if (blackPanel != null) blackPanel.anchoredPosition = uncoveredAnchoredPos;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // (Both positions live as serialized fields above — no live computation.)

        // ─── Transition flow ─────────────────────────────────────────────

        private IEnumerator TransitionRoutine(string sceneName)
        {
            inFlight = true;
            yield return SlideTo(coveredAnchoredPos);

            if (coverHoldSeconds > 0f)
            {
                float t = 0f;
                while (t < coverHoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneTransitionOverlay] Scene '{sceneName}' is not in Build Settings.");
                yield return SlideTo(uncoveredAnchoredPos);
                inFlight = false;
                yield break;
            }

            // Mark that we're expecting an OnSceneLoaded callback to drive
            // the slide-down. The SceneFlow swap is synchronous from the
            // caller's perspective; sceneLoaded fires next frame.
            waitingForLoad = true;
            SceneManager.LoadScene(sceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!waitingForLoad) return;
            waitingForLoad = false;
            StartCoroutine(SlideDownThenClear());
        }

        private IEnumerator SlideDownThenClear()
        {
            yield return SlideTo(uncoveredAnchoredPos);
            inFlight = false;
        }

        // Hard cap on per-frame timer advance during the slide. The first frame
        // after SceneManager.LoadScene completes can have a massive deltaTime
        // (the engine just finished a synchronous load + asset rebind on that
        // frame). Without this cap, t jumps deep into the curve in a single
        // 1/30s = ~33ms ≈ a slow frame — generous, but still smooth.
        private const float MaxStepPerFrame = 1f / 30f;

        private IEnumerator SlideTo(Vector2 to)
        {
            if (blackPanel == null) yield break;
            Vector2 from = blackPanel.anchoredPosition;

            // Yield one frame BEFORE sampling deltaTime — lets the post-load
            // catch-up frame settle so the first real animation step starts
            // with a normal frame interval.
            yield return null;

            float t = 0f;
            while (t < slideDuration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxStepPerFrame);
                float k = curve.Evaluate(Mathf.Clamp01(t / slideDuration));
                blackPanel.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            blackPanel.anchoredPosition = to;
        }

        // ─── Runtime fallback (no prefab authored) ───────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("SceneTransitionOverlay");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;            // above every other UI

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();   // catches input while covered

            var panelGO = new GameObject("BlackPanel");
            panelGO.transform.SetParent(go.transform, false);
            var rt = panelGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = panelGO.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;              // block input while covered

            var overlay = go.AddComponent<SceneTransitionOverlay>();
            overlay.blackPanel = rt;
            return go;
        }
    }
}
