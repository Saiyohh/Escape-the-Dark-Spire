using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class TooltipView : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("RectTransform that gets moved into place. Defaults to this " +
                 "GameObject's RectTransform on Awake if left null.")]
        [SerializeField] private RectTransform panelRoot;

        [Header("Header (icon + name)")]
        [Tooltip("The whole header row container. Toggled SetActive(false) " +
                 "for the orb path (no header).")]
        [SerializeField] private GameObject headerRoot;
        [SerializeField] private Image headerIcon;
        [SerializeField] private TMP_Text headerLabel;
        [Tooltip("Wrapper around headerIcon — toggled SetActive(false) when " +
                 "the trigger has a name but no icon. If left unwired, falls " +
                 "back to toggling headerIcon.gameObject directly.")]
        [SerializeField] private GameObject headerIconContainer;

        [Header("Body — single block (conditions, chance, stars)")]
        [SerializeField] private GameObject bodyRoot;
        [SerializeField] private TMP_Text bodyLabel;

        [Header("Body — passive/evoke pair (Defect orbs)")]
        [SerializeField] private GameObject passiveEvokeRoot;
        [SerializeField] private TMP_Text passiveLabel;
        [SerializeField] private TMP_Text evokeLabel;

        [Header("Body prefixes")]
        [Tooltip("Prefix on the first paragraph of the orb body. The text " +
                 "after the prefix is OrbDataSO.passiveDescription.")]
        [SerializeField] private string passivePrefix = "<b>Passive:</b> ";

        [Tooltip("Prefix on the second paragraph of the orb body.")]
        [SerializeField] private string evokePrefix = "<b>Evoke:</b> ";

        [Header("Fade in")]
        [Tooltip("Seconds the panel takes to fade from alpha 0 → 1 when " +
                 "first shown. 0 disables the fade (instant pop). The fade " +
                 "uses CanvasGroup.alpha so all child Graphics ramp together. " +
                 "If no CanvasGroup exists on this GameObject, one is added " +
                 "at Awake.")]
        [Min(0f)] [SerializeField] private float fadeInDuration = 0.15f;

        public RectTransform PanelRoot => panelRoot;

        private CanvasGroup canvasGroup;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void PlayFadeIn()
        {
            if (canvasGroup == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);

            if (fadeInDuration <= 0f)
            {
                canvasGroup.alpha = 1f;
                return;
            }

            fadeRoutine = StartCoroutine(FadeInCoroutine());
        }

        private IEnumerator FadeInCoroutine()
        {
            canvasGroup.alpha = 0f;
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            fadeRoutine = null;
        }

        public void SetContent(TooltipContent content)
        {
            bool showHeader = content.HasHeader;
            if (headerRoot != null) headerRoot.SetActive(showHeader);
            if (showHeader)
            {
                if (headerLabel != null) headerLabel.text = content.HeaderText;

                bool showIcon = content.HeaderIcon != null;
                if (headerIcon != null)
                {
                    headerIcon.sprite = content.HeaderIcon;
                    headerIcon.enabled = showIcon;
                }
                if (headerIconContainer != null) headerIconContainer.SetActive(showIcon);
                else if (headerIcon != null) headerIcon.gameObject.SetActive(showIcon);
            }

            bool twoBlock = content.HasPassiveEvoke;
            if (bodyRoot != null) bodyRoot.SetActive(!twoBlock);
            if (passiveEvokeRoot != null) passiveEvokeRoot.SetActive(twoBlock);

            if (!twoBlock)
            {
                if (bodyLabel != null) bodyLabel.text = content.BodyText ?? string.Empty;
            }
            else
            {
                if (passiveLabel != null)
                {
                    passiveLabel.text = string.IsNullOrEmpty(content.PassiveText)
                        ? string.Empty
                        : passivePrefix + content.PassiveText;
                    passiveLabel.gameObject.SetActive(!string.IsNullOrEmpty(content.PassiveText));
                }
                if (evokeLabel != null)
                {
                    evokeLabel.text = string.IsNullOrEmpty(content.EvokeText)
                        ? string.Empty
                        : evokePrefix + content.EvokeText;
                    evokeLabel.gameObject.SetActive(!string.IsNullOrEmpty(content.EvokeText));
                }
            }
        }

        public void SetScreenPosition(Vector2 screenPoint)
        {
            if (panelRoot == null) return;

            panelRoot.pivot = new Vector2(0.5f, 0.5f);

            var parentRT = panelRoot.parent as RectTransform;
            if (parentRT == null)
            {
                panelRoot.position = screenPoint;
                return;
            }

            Camera uiCamera = null;
            var canvas = parentRT.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRT, screenPoint, uiCamera, out Vector2 localPoint))
            {
                panelRoot.localPosition = localPoint;
            }
        }
    }
}
