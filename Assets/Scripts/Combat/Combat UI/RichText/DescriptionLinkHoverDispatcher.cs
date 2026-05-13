using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class DescriptionLinkHoverDispatcher : MonoBehaviour
    {
        [Tooltip("Logs every link-hover transition to the console. Flip on while " +
                 "diagnosing why a description's hovers aren't firing; flip off when done.")]
        [SerializeField] private bool verboseLogging;

        [Tooltip("Hitbox inflation for ComputedNumber tokens only. 0.5 = 50% larger total " +
                 "(numbers are usually 1-2 characters wide and need help being grabbable). " +
                 "Set to 0 to disable padding entirely.")]
        [Range(0f, 2f)]
        [SerializeField] private float numberHitboxInflation = 0.5f;

        private TMP_Text text;
        private DescriptionRenderer descriptionRenderer;
        private Canvas parentCanvas;
        private Camera uiCamera;

        // Currently active link's token index (-1 = none).
        private int activeTokenIndex = -1;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            descriptionRenderer = GetComponent<DescriptionRenderer>();
            parentCanvas = GetComponentInParent<Canvas>();
            ResolveUiCamera();
        }

        private void OnEnable()
        {
            // Re-resolve canvas/camera when this component re-enables (parent may have changed).
            parentCanvas = GetComponentInParent<Canvas>();
            ResolveUiCamera();
        }

        private void OnDisable()
        {
            ExitActive();
        }

        private void ResolveUiCamera()
        {
            uiCamera = null;
            if (parentCanvas == null) return;

            // Walk to the root canvas — sub-canvases inherit render mode but
            // may have a null worldCamera, which would make FindIntersectingLink
            // silently miss every hover under a Camera-mode setup.
            var root = parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas : parentCanvas;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = root.worldCamera;
        }

        public void NotifyRendered()
        {
            // Recompute link rects on next LateUpdate; nothing else to do here.
            // We don't synthesize an enter — if the cursor is still over the
            // same link, the LateUpdate transition logic will see it's the
            // same activeTokenIndex and do nothing (keeping content in place
            // without flicker).
        }

        private void LateUpdate()
        {
            if (text == null || !isActiveAndEnabled) return;
            if (Mouse.current == null) { ExitActive(); return; }

            Vector2 mouseScreen = Mouse.current.position.ReadValue();

            // First-pass: precise hit-test (matches what TMP samples use).
            int linkIdx = TMP_TextUtilities.FindIntersectingLink(text, mouseScreen, uiCamera);

            // Fallback: inflated hit-test, but only against ComputedNumber tokens.
            // Numbers are 1-2 characters and hard to land precisely; keywords are
            // already wide enough to grab without help.
            if (linkIdx < 0 && numberHitboxInflation > 0f)
            {
                linkIdx = LinkRectMath.FindIntersectingLinkPadded(
                    text, mouseScreen, uiCamera,
                    numberHitboxInflation,
                    IsNumberLink);
            }

            if (linkIdx < 0)
            {
                if (verboseLogging && activeTokenIndex >= 0)
                    Debug.Log($"[RichText] hover exit (mouse {mouseScreen}, no link) on '{name}'");
                ExitActive();
                return;
            }

            var info = text.textInfo.linkInfo[linkIdx];
            string idStr = info.GetLinkID();
            if (!DescriptionRenderer.TryParseLinkId(idStr, out int tokenIndex))
            {
                if (verboseLogging)
                    Debug.LogWarning($"[RichText] link '{idStr}' on '{name}' didn't parse as a description token id");
                ExitActive();
                return;
            }

            if (verboseLogging && tokenIndex != activeTokenIndex)
                Debug.Log($"[RichText] hover '{idStr}' (token {tokenIndex}) on '{name}' " +
                          $"camera={(uiCamera != null ? uiCamera.name : "<null>")} " +
                          $"renderMode={(parentCanvas != null ? parentCanvas.renderMode.ToString() : "<no canvas>")}");

            if (tokenIndex == activeTokenIndex)
            {
                // Still on the same link — refresh content in case the renderer
                // re-rendered with a new value.
                ShowFor(tokenIndex, linkIdx, isRefresh: true);
                return;
            }

            // Different (or first) link — exit old, enter new.
            ExitActive();
            activeTokenIndex = tokenIndex;
            ShowFor(tokenIndex, linkIdx, isRefresh: false);
        }

        // Predicate handed to FindIntersectingLinkPadded so only number tokens
        // get the inflated hitbox treatment. Resolves the link's tokenIndex
        // via the same id-parsing the dispatcher uses for routing.
        private bool IsNumberLink(int linkIdx)
        {
            if (descriptionRenderer == null) return false;
            if (linkIdx < 0 || linkIdx >= text.textInfo.linkInfo.Length) return false;
            var info = text.textInfo.linkInfo[linkIdx];
            if (!DescriptionRenderer.TryParseLinkId(info.GetLinkID(), out int tokenIndex)) return false;
            if (!descriptionRenderer.TryGetToken(tokenIndex, out var t)) return false;
            return t.Kind == DescriptionTokenKind.ComputedNumber;
        }

        private void ExitActive()
        {
            if (activeTokenIndex < 0) return;

            var keyword = KeywordTooltipPresenter.Instance;
            if (keyword != null) keyword.HideIfOwnedBy(this, activeTokenIndex);

            var calc = NumberCalcBoxPresenter.Instance;
            if (calc != null) calc.HideIfOwnedBy(this, activeTokenIndex);

            activeTokenIndex = -1;
        }

        private void ShowFor(int tokenIndex, int linkIdx, bool isRefresh)
        {
            if (descriptionRenderer == null) return;
            if (!descriptionRenderer.TryGetToken(tokenIndex, out var t)) return;

            switch (t.Kind)
            {
                case DescriptionTokenKind.Keyword:
                    if (descriptionRenderer.TryGetKeywordEntry(tokenIndex, out var entry) && entry != null)
                    {
                        var presenter = KeywordTooltipPresenter.Instance;
                        if (presenter != null)
                            presenter.Show(this, tokenIndex, entry, text, linkIdx, isRefresh);
                    }
                    break;

                case DescriptionTokenKind.ComputedNumber:
                    if (descriptionRenderer.TryGetNumberResult(tokenIndex, out var result))
                    {
                        var presenter = NumberCalcBoxPresenter.Instance;
                        if (presenter != null)
                            presenter.Show(this, tokenIndex, result, text, linkIdx, isRefresh);
                    }
                    break;
            }
        }
    }
}
