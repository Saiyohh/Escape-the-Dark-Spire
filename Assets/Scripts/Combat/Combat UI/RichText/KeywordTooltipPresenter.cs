using System.Collections;
using UnityEngine;
using TMPro;

namespace DarkSpire
{
    public class KeywordTooltipPresenter : MonoBehaviour
    {
        public static KeywordTooltipPresenter Instance { get; private set; }

        [Tooltip("Pixels of vertical gap between the link's top edge and the tooltip's bottom edge.")]
        [SerializeField] private float pixelGap = 8f;

        [Tooltip("Show delay; falls back to TooltipController.defaultShowDelay when set to a negative value. " +
                 "0.05 = 'nearly instant' for description hovers; the global tooltip default is 0.3s for icons " +
                 "where the player needs more reading time.")]
        [SerializeField] private float showDelayOverride = 0.05f;

        [Tooltip("Hide grace; falls back to TooltipController.defaultHideGrace when set to a negative value.")]
        [SerializeField] private float hideGraceOverride = -1f;

        [Tooltip("Logs every Show / DoShow / DoHide step. Flip on to diagnose why a keyword " +
                 "tooltip isn't appearing. Pairs well with TooltipController.verboseLogging.")]
        [SerializeField] private bool verboseLogging;

        private TooltipView activeView;
        private DescriptionLinkHoverDispatcher activeOwner;
        private int activeKey = -1;

        private Coroutine pendingShow;
        private Coroutine pendingHide;
        private TMP_Text pendingText;
        private int pendingLinkIdx;
        private KeywordGlossary.Entry pendingEntry;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private float ShowDelay =>
            showDelayOverride >= 0f
                ? showDelayOverride
                : (TooltipController.Instance != null ? TooltipController.Instance.defaultShowDelay : 0.3f);

        private float HideGrace =>
            hideGraceOverride >= 0f
                ? hideGraceOverride
                : (TooltipController.Instance != null ? TooltipController.Instance.defaultHideGrace : 0.05f);

        public void Show(
            DescriptionLinkHoverDispatcher owner,
            int key,
            KeywordGlossary.Entry entry,
            TMP_Text anchorText,
            int linkIdx,
            bool isRefresh)
        {
            if (entry == null || anchorText == null)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[KeywordTooltip] Show bailed — entry={(entry == null ? "null" : entry.key)} " +
                                     $"anchorText={(anchorText == null ? "null" : anchorText.name)}", this);
                return;
            }
            if (verboseLogging)
                Debug.Log($"[KeywordTooltip] Show key='{entry.key}' isRefresh={isRefresh} " +
                          $"hasActiveView={(activeView != null)} sameOwnerKey={(activeOwner == owner && activeKey == key)}", this);

            // Same owner + same key: refresh content + reposition without
            // restarting the delay. Crucial — LateUpdate calls Show() every
            // frame the cursor stays on the same link, and resetting the
            // pending-show coroutine each frame would prevent the tooltip from
            // ever materializing.
            if (activeOwner == owner && activeKey == key)
            {
                if (activeView != null)
                {
                    ApplyContent(activeView, entry);
                    PositionAboveLink(activeView, anchorText, linkIdx);
                }
                else
                {
                    pendingEntry = entry;
                    pendingText = anchorText;
                    pendingLinkIdx = linkIdx;
                }
                CancelPendingHide();
                return;
            }

            // New target: cancel any pending hide; cancel any pending show that
            // was for a different anchor.
            CancelPendingHide();

            // If a popup is already visible, swap content + position instantly.
            if (activeView != null)
            {
                CancelPendingShow();
                activeOwner = owner;
                activeKey = key;
                ApplyContent(activeView, entry);
                PositionAboveLink(activeView, anchorText, linkIdx);
                return;
            }

            // Nothing visible yet — schedule a delayed show, or instant if delay is 0.
            float delay = ShowDelay;
            CancelPendingShow();
            pendingText = anchorText;
            pendingLinkIdx = linkIdx;
            pendingEntry = entry;
            activeOwner = owner;
            activeKey = key;

            if (delay <= 0f) DoShow();
            else pendingShow = StartCoroutine(ShowAfterDelay(delay));
        }

        public void HideIfOwnedBy(DescriptionLinkHoverDispatcher owner, int key)
        {
            if (activeOwner != owner || activeKey != key) return;

            CancelPendingShow();

            if (activeView == null)
            {
                // Pending show was cancelled before it materialized.
                activeOwner = null;
                activeKey = -1;
                return;
            }

            float grace = HideGrace;
            if (pendingHide != null) return;
            if (grace <= 0f) DoHide();
            else pendingHide = StartCoroutine(HideAfterGrace(grace));
        }

        private IEnumerator ShowAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            pendingShow = null;
            DoShow();
        }

        private IEnumerator HideAfterGrace(float grace)
        {
            yield return new WaitForSecondsRealtime(grace);
            pendingHide = null;
            DoHide();
        }

        private void DoShow()
        {
            var ctrl = TooltipController.Instance;
            if (ctrl == null)
            {
                if (verboseLogging)
                    Debug.LogWarning("[KeywordTooltip] DoShow bailed — TooltipController.Instance is null. " +
                                     "Add a TooltipController to the combat UI scene.", this);
                ResetState();
                return;
            }
            if (pendingText == null || pendingEntry == null)
            {
                if (verboseLogging)
                    Debug.LogWarning("[KeywordTooltip] DoShow bailed — pending text/entry was cleared.", this);
                ResetState();
                return;
            }

            activeView = ctrl.Allocate();
            if (activeView == null)
            {
                if (verboseLogging)
                    Debug.LogWarning("[KeywordTooltip] DoShow bailed — TooltipController.Allocate returned null. " +
                                     "Check the controller's viewPrefab + tooltipsParent. Enable " +
                                     "TooltipController.verboseLogging for the specific reason.", this);
                ResetState();
                return;
            }

            ApplyContent(activeView, pendingEntry);
            PositionAboveLink(activeView, pendingText, pendingLinkIdx);
            activeView.PlayFadeIn();

            if (verboseLogging)
                Debug.Log($"[KeywordTooltip] DoShow → view '{activeView.name}' " +
                          $"text='{pendingEntry.key}' anchor='{pendingText.name}'", this);

            pendingText = null;
            pendingEntry = null;
        }

        private void DoHide()
        {
            if (activeView != null && TooltipController.Instance != null)
                TooltipController.Instance.Release(activeView);
            ResetState();
        }

        private void ResetState()
        {
            activeView = null;
            activeOwner = null;
            activeKey = -1;
            pendingText = null;
            pendingEntry = null;
        }

        private void CancelPendingShow()
        {
            if (pendingShow != null) { StopCoroutine(pendingShow); pendingShow = null; }
            pendingText = null;
            pendingEntry = null;
        }

        private void CancelPendingHide()
        {
            if (pendingHide != null) { StopCoroutine(pendingHide); pendingHide = null; }
        }

        private void ApplyContent(TooltipView view, KeywordGlossary.Entry entry)
        {
            var glossary = KeywordGlossary.Instance;
            string name = glossary != null ? glossary.GetDisplayName(entry) : entry.displayName ?? entry.key;
            string body = glossary != null ? glossary.GetDescription(entry) : entry.description;
            Sprite icon = glossary != null ? glossary.GetIcon(entry) : entry.icon;

            // Condition-linked entries: resolve {X}/{stacks}/{total} placeholders
            // in the body via the SO data. Skill-description hovers have no live
            // unit context, so stacks defaults to 0 — {X} still substitutes
            // correctly for "Increases DEF by 1 for this round."-style rule text.
            if (entry.isConditionLinked && ConditionLibrary.Instance != null)
            {
                var data = ConditionLibrary.Instance.Get(entry.linkedCondition);
                if (data != null)
                    body = ConditionDescriptionFormatter.Format(data, stacks: 0);
            }

            view.SetContent(TooltipContent.ForCondition(name, icon, body));
        }

        private void PositionAboveLink(TooltipView view, TMP_Text text, int linkIdx)
        {
            if (view == null || text == null) return;
            if (linkIdx < 0 || linkIdx >= text.textInfo.linkInfo.Length) return;

            // Compute the link's screen-space mid-top point.
            if (!LinkRectMath.TryGetLinkScreenMidTop(text, linkIdx, out Vector2 screenMidTop))
                return;

            // Force a layout pass so PanelRoot.rect.height is accurate.
            Canvas.ForceUpdateCanvases();
            float halfH = view.PanelRoot != null ? view.PanelRoot.rect.height * 0.5f : 32f;

            view.SetScreenPosition(new Vector2(screenMidTop.x, screenMidTop.y + halfH + pixelGap));
        }
    }
}
