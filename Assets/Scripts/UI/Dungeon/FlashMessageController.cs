using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class FlashMessageController : MonoBehaviour
    {
        public static FlashMessageController Instance { get; private set; }

        [Header("Display")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;

        [Header("Timing")]
        [SerializeField] private float displaySeconds = 1.6f;
        [SerializeField] private float fadeSeconds = 0.2f;

        private readonly Queue<string> queue = new();
        private string lastEnqueued;
        private Coroutine pump;

        // ─── Public API ──────────────────────────────────────────────────────

        public static FlashMessageController GetOrCreate()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<GameObject>("FlashMessages");
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<FlashMessageController>();
        }

        public void Enqueue(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // De-dupe consecutive identical entries (prevents Alert "!" spam
            // when multiple monsters trip Alert on the same frame).
            if (text == lastEnqueued) return;
            lastEnqueued = text;
            queue.Enqueue(text);
            if (pump == null) pump = StartCoroutine(Pump());
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            DungeonEvents.OnFlashMessage += Enqueue;
        }

        private void OnDisable()
        {
            DungeonEvents.OnFlashMessage -= Enqueue;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Pump ────────────────────────────────────────────────────────────

        private IEnumerator Pump()
        {
            while (queue.Count > 0)
            {
                string text = queue.Dequeue();
                if (label != null) label.text = text;

                // Fade in.
                yield return Fade(0f, 1f, fadeSeconds);

                // Hold.
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, displaySeconds));

                // Fade out.
                yield return Fade(1f, 0f, fadeSeconds);
            }

            // Reset de-dupe gate once the queue drains so the same string can
            // be shown again later if the situation repeats.
            lastEnqueued = null;
            pump = null;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (group == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            group.alpha = to;
        }

        // ─── Runtime fallback ────────────────────────────────────────────────

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("FlashMessages");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 750; // above pickup toasts (700), below pause (800)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Flash root — bottom-center, fixed width.
            var flashGO = new GameObject("Flash");
            flashGO.transform.SetParent(go.transform, false);
            var rt = flashGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 220f);
            rt.sizeDelta = new Vector2(700f, 60f);

            var bg = flashGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var hlg = flashGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 8, 8);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var fitter = flashGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(flashGO.transform, false);
            labelGO.AddComponent<RectTransform>();
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            var cg = flashGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var manager = go.AddComponent<FlashMessageController>();
            manager.group = cg;
            manager.label = tmp;
            return go;
        }
    }
}
