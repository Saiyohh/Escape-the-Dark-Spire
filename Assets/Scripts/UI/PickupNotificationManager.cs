using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class PickupNotificationManager : MonoBehaviour
    {
        public static PickupNotificationManager Instance { get; private set; }

        [Header("Stacking")]
        [Tooltip("Container the toast rows are parented under — usually a " +
                 "VerticalLayoutGroup. If null, the runtime fallback creates one.")]
        [SerializeField] private RectTransform stackContainer;

        [Header("Toast row prefab")]
        [Tooltip("Optional. If null, ShowPickup builds a row at runtime.")]
        [SerializeField] private GameObject toastPrefab;

        [Header("Behavior")]
        [SerializeField] private float lifeSeconds = 2.6f;
        [SerializeField] private float fadeOutSeconds = 0.5f;

        public static PickupNotificationManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<GameObject>("PickupNotifications");
            GameObject go = prefab != null ? Instantiate(prefab) : BuildRuntimeFallback();
            return go.GetComponent<PickupNotificationManager>();
        }

        public void ShowPickup(string text, Sprite icon = null)
        {
            if (stackContainer == null) return;
            var row = BuildRow(text, icon);
            StartCoroutine(LifecycleAndDestroy(row));
        }

        public void ShowMulti(IEnumerable<(string text, Sprite icon)> lines)
        {
            if (stackContainer == null || lines == null) return;
            foreach (var (text, icon) in lines)
                ShowPickup(text, icon);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private GameObject BuildRow(string text, Sprite icon)
        {
            GameObject row;
            if (toastPrefab != null)
            {
                row = Instantiate(toastPrefab, stackContainer);
                var img = row.GetComponentInChildren<Image>(true);
                var tmp = row.GetComponentInChildren<TMP_Text>(true);
                if (img != null) img.sprite = icon;
                if (img != null) img.gameObject.SetActive(icon != null);
                if (tmp != null) tmp.text = text;
            }
            else
            {
                row = BuildRuntimeRow(stackContainer, text, icon);
            }
            return row;
        }

        private IEnumerator LifecycleAndDestroy(GameObject row)
        {
            float hold = Mathf.Max(0.05f, lifeSeconds - fadeOutSeconds);
            yield return new WaitForSeconds(hold);

            var cg = row.GetComponent<CanvasGroup>();
            if (cg == null) cg = row.AddComponent<CanvasGroup>();

            float t = 0f;
            while (t < fadeOutSeconds)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
                yield return null;
            }
            Destroy(row);
        }

        private static GameObject BuildRuntimeFallback()
        {
            var go = new GameObject("PickupNotifications");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var stack = new GameObject("Stack");
            stack.transform.SetParent(go.transform, false);
            var stackRT = stack.AddComponent<RectTransform>();
            stackRT.anchorMin = new Vector2(1f, 1f);
            stackRT.anchorMax = new Vector2(1f, 1f);
            stackRT.pivot     = new Vector2(1f, 1f);
            stackRT.anchoredPosition = new Vector2(-32f, -32f);
            stackRT.sizeDelta = new Vector2(420f, 0f);

            var vlg = stack.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperRight;
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = stack.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var manager = go.AddComponent<PickupNotificationManager>();
            manager.stackContainer = stackRT;
            return go;
        }

        private static GameObject BuildRuntimeRow(RectTransform parent, string text, Sprite icon)
        {
            var row = new GameObject("Toast");
            row.transform.SetParent(parent, false);

            var rowRT = row.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, 48f);

            var bg = row.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 14, 6, 6);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            if (icon != null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(row.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.sizeDelta = new Vector2(36f, 36f);
                var img = iconGO.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                var le = iconGO.AddComponent<LayoutElement>();
                le.preferredWidth = 36f;
                le.preferredHeight = 36f;
            }

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            labelGO.AddComponent<RectTransform>();
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;

            return row;
        }
    }
}
