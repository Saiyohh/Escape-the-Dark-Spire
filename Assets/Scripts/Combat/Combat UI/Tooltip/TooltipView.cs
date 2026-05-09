// TooltipView.cs
// -----------------------------------------------------------------------------
// Drives one pooled tooltip prefab's UI. Owns the root RectTransform, the
// header row (icon + label), and the body (single block OR passive/evoke
// pair). Position is set externally by TooltipSpawnPoint via
// SetScreenPosition — this script only renders content.
//
// Visual structure (authored in the prefab — this script just toggles + fills):
//
//   TooltipPanel (Image — black bg + UIOutline white border; this script
//                 lives here; pooled instance is a child of the combat
//                 canvas's TooltipsParent)
//   ├─ Header (HorizontalLayoutGroup)
//   │   ├─ Icon  (Image — Preserve Aspect)
//   │   └─ Label (TMP_Text — name)
//   ├─ Body  (TMP_Text — single-block path)
//   └─ PassiveEvoke (VerticalLayoutGroup — orb path)
//       ├─ PassiveLabel (TMP_Text)
//       └─ EvokeLabel   (TMP_Text)
// -----------------------------------------------------------------------------
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

        /// <summary>The panel's RectTransform — used by the spawn point to
        /// read the panel's preferred size after layout rebuild.</summary>
        public RectTransform PanelRoot => panelRoot;

        // CanvasGroup driven by PlayFadeIn. Auto-resolved (added if missing)
        // so a prefab without one still works — though authoring it on the
        // prefab is preferable so the user can also use it for raycast
        // blocking config.
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
            // Pooled / hidden — reset state so the next allocation starts
            // from a known place. The fade routine is owned by this component
            // so stopping it on disable is safe.
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Snap alpha to 0 and animate to 1 over <c>fadeInDuration</c>. Called
        /// by TooltipSpawnPoint only on a NEW allocation (not on content
        /// updates of an already-visible tooltip — that would re-fade every
        /// frame the spawn point's Layout re-runs, which looks awful).
        /// </summary>
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
            // Snap to invisible first, then ramp up. Realtime so the fade
            // still plays if the game is paused via Time.timeScale.
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
            // Header row.
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

            // Body — single vs. two-block. Two-block wins if either field set.
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

        /// <summary>
        /// Place the panel's center at <paramref name="screenPoint"/> (in
        /// screen pixels — origin bottom-left). Sets the pivot to centered
        /// for predictable arithmetic and converts the screen point into
        /// the panel's parent-local rect via RectTransformUtility, which
        /// handles every canvas mode uniformly (Overlay / Camera / WorldSpace).
        /// </summary>
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

            // Look up the parent canvas each call — pooled views may be
            // re-parented to different canvases at runtime if a future
            // spawn point routes them elsewhere. Cheap walk.
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
