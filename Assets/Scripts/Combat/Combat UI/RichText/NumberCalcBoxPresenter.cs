using System.Collections;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class NumberCalcBoxPresenter : MonoBehaviour
    {
        public static NumberCalcBoxPresenter Instance { get; private set; }

        [Tooltip("Prefab whose root GameObject has a NumberCalcBoxView component. " +
                 "The presenter instantiates one instance under boxParent at Awake.")]
        [SerializeField] private GameObject viewPrefab;

        [Tooltip("Parent (RectTransform GameObject) for the spawned view. " +
                 "Should be under the combat canvas, last sibling so it draws on top. " +
                 "Falls back to this transform if null.")]
        [SerializeField] private GameObject boxParent;

        [Tooltip("Pixels of vertical gap between the link's top edge and the calc box's bottom edge.")]
        [SerializeField] private float pixelGap = 6f;

        [Tooltip("Seconds to hold over a number before the calc box appears. " +
                 "Default 0.05 = 'nearly instant' (one or two frames). " +
                 "Bump higher if the popup feels twitchy under fast cursor sweeps.")]
        [SerializeField] private float showDelay = 0.05f;
        [SerializeField] private float hideGrace = 0.05f;

        private NumberCalcBoxView view;
        private DescriptionLinkHoverDispatcher activeOwner;
        private int activeKey = -1;
        private bool isVisible;

        private Coroutine pendingShow;
        private Coroutine pendingHide;
        private TMP_Text pendingText;
        private int pendingLinkIdx;
        private NumberEvaluator.Result pendingResult;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            EnsureView();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureView()
        {
            if (view != null) return;
            if (viewPrefab == null) return;

            Transform parent = boxParent != null ? boxParent.transform : transform;
            var instance = Instantiate(viewPrefab, parent);
            view = instance.GetComponent<NumberCalcBoxView>();
            if (view == null)
            {
                Debug.LogError("[NumberCalcBox] viewPrefab root has no NumberCalcBoxView component.", this);
                Destroy(instance);
                return;
            }
            view.SetVisible(false);
        }

        public void Show(
            DescriptionLinkHoverDispatcher owner,
            int key,
            NumberEvaluator.Result result,
            TMP_Text anchorText,
            int linkIdx,
            bool isRefresh)
        {
            EnsureView();
            if (view == null || anchorText == null) return;

            // Same target — refresh content + reposition without restarting delay.
            if (activeOwner == owner && activeKey == key)
            {
                if (isVisible)
                {
                    view.SetBreakdown(result.Breakdown);
                    Position(anchorText, linkIdx);
                }
                else
                {
                    // Pending show is already mid-flight for this exact target.
                    // Just refresh the cached payload (target may have hovered
                    // changing damage values) — don't restart the delay timer,
                    // or every LateUpdate would reset it and the popup would
                    // never materialize.
                    pendingResult = result;
                    pendingText = anchorText;
                    pendingLinkIdx = linkIdx;
                }
                CancelPendingHide();
                return;
            }

            CancelPendingHide();

            // Already visible from any source — swap instantly.
            if (isVisible)
            {
                CancelPendingShow();
                activeOwner = owner;
                activeKey = key;
                view.SetBreakdown(result.Breakdown);
                Position(anchorText, linkIdx);
                return;
            }

            // Schedule a delayed show.
            CancelPendingShow();
            pendingText = anchorText;
            pendingLinkIdx = linkIdx;
            pendingResult = result;
            activeOwner = owner;
            activeKey = key;

            if (showDelay <= 0f) DoShow();
            else pendingShow = StartCoroutine(ShowAfterDelay(showDelay));
        }

        public void HideIfOwnedBy(DescriptionLinkHoverDispatcher owner, int key)
        {
            if (activeOwner != owner || activeKey != key) return;

            CancelPendingShow();

            if (!isVisible)
            {
                activeOwner = null;
                activeKey = -1;
                return;
            }

            if (pendingHide != null) return;
            if (hideGrace <= 0f) DoHide();
            else pendingHide = StartCoroutine(HideAfterGrace(hideGrace));
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
            if (view == null) { ResetState(); return; }
            if (pendingText == null) { ResetState(); return; }

            view.SetBreakdown(pendingResult.Breakdown);
            view.SetVisible(true);
            isVisible = true;
            Position(pendingText, pendingLinkIdx);

            pendingText = null;
        }

        private void DoHide()
        {
            if (view != null) view.SetVisible(false);
            isVisible = false;
            ResetState();
        }

        private void ResetState()
        {
            activeOwner = null;
            activeKey = -1;
            pendingText = null;
            pendingResult = default;
        }

        private void CancelPendingShow()
        {
            if (pendingShow != null) { StopCoroutine(pendingShow); pendingShow = null; }
            pendingText = null;
            pendingResult = default;
        }

        private void CancelPendingHide()
        {
            if (pendingHide != null) { StopCoroutine(pendingHide); pendingHide = null; }
        }

        private void Position(TMP_Text text, int linkIdx)
        {
            if (view == null || text == null) return;
            if (!LinkRectMath.TryGetLinkScreenMidTop(text, linkIdx, out Vector2 screenMidTop)) return;

            Canvas.ForceUpdateCanvases();
            float halfH = view.PanelRoot != null ? view.PanelRoot.rect.height * 0.5f : 24f;
            view.SetScreenPosition(new Vector2(screenMidTop.x, screenMidTop.y + halfH + pixelGap));
        }
    }
}
