// TooltipTrigger.cs
// -----------------------------------------------------------------------------
// Abstract base for any element that wants to fire a tooltip on hover.
// Subclasses do two things:
//   1. Provide the tooltip's payload via BuildContent().
//   2. Detect hover enter/exit through whatever input plumbing fits their
//      element type (UGUI pointer events, world-space collider polling, TMP
//      link hover detection, etc.) and call ShowTooltip() / HideTooltip().
//
// Two concrete bases ship with the system:
//   • UITooltipTrigger      — UGUI IPointerEnter/Exit handlers. Use on any
//                             RectTransform-based UI element.
//   • WorldTooltipTrigger   — Polls Collider2D.OverlapPoint each frame
//                             against the cursor world position. Use on
//                             world-space SpriteRenderer elements (orb
//                             slots) since the new Input System silently
//                             skips OnMouseEnter/IPointerEnterHandler on
//                             world sprites.
//
// Per-trigger ownership of the show delay + hide grace. Each trigger runs
// its own coroutines so multiple triggers can be in flight simultaneously
// without stomping each other's timing. The actual tooltip placement +
// stacking happens in the trigger's resolved TooltipSpawnPoint.
//
// Authoring: triggers expose NO inspector fields by default. The spawn
// point is auto-resolved via GetComponentInParent at Awake; timing comes
// from the global TooltipController defaults; toggle behavior is the
// MonoBehaviour's built-in `enabled` checkbox (disabled component → no
// tooltip). Subclasses may add their own fields for content (e.g.
// StaticTooltipTrigger's inspector-authored header / body text).
// -----------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

namespace DarkSpire
{
    public abstract class TooltipTrigger : MonoBehaviour
    {
        // Spawn point that owns the on-screen anchor + stack policy for this
        // trigger's tooltip. Resolved at Awake via GetComponentInParent — every
        // trigger lives inside the hierarchy of the container that defines
        // its spawn point (e.g. OrbSlotsUI for orb slots, the unit's HUD for
        // condition icons). Not serialized: there's no use case for an
        // override that breaks the hierarchy contract.
        protected TooltipSpawnPoint spawnPoint;

        private Coroutine pendingShow;
        private Coroutine pendingHide;

        protected virtual void Awake()
        {
            spawnPoint = GetComponentInParent<TooltipSpawnPoint>();
            if (TooltipController.Verbose && spawnPoint == null)
                Debug.LogWarning($"[Tooltip] {GetType().Name} on '{name}': no " +
                                 "TooltipSpawnPoint found in parent chain. " +
                                 "This trigger will silently no-op.", this);
        }

        /// <summary>
        /// Build the tooltip payload. Return false to suppress the tooltip on
        /// this hover (e.g. an empty orb slot has nothing to show).
        /// </summary>
        protected abstract bool BuildContent(out TooltipContent content);

        /// <summary>Subclasses call this from their hover-enter detector.</summary>
        protected void ShowTooltip()
        {
            bool verbose = TooltipController.Verbose;

            if (!isActiveAndEnabled)
            {
                if (verbose) Debug.Log($"[Tooltip] ShowTooltip skipped on '{name}' — component disabled", this);
                return;
            }
            if (spawnPoint == null)
            {
                if (verbose) Debug.Log($"[Tooltip] ShowTooltip skipped on '{name}' — no spawn point resolved", this);
                return;
            }
            if (!BuildContent(out var content))
            {
                if (verbose) Debug.Log($"[Tooltip] ShowTooltip skipped on '{name}' — BuildContent returned false (empty payload)", this);
                return;
            }

            // User came back over the trigger before the hide grace expired —
            // cancel that, the tooltip stays.
            CancelPendingHide();

            if (pendingShow != null)
            {
                if (verbose) Debug.Log($"[Tooltip] ShowTooltip on '{name}' — already pending, ignoring", this);
                return;
            }
            if (spawnPoint.IsShowingFor(this))
            {
                if (verbose) Debug.Log($"[Tooltip] ShowTooltip on '{name}' — already shown, ignoring", this);
                return;
            }

            // If the spawn point is already showing something for someone
            // else, skip the delay — the user is in tooltip-reading mode and
            // the new entry should drop straight in beside the existing stack.
            float delay = ResolveDelay();
            if (spawnPoint.HasAnyVisible) delay = 0f;

            if (verbose)
                Debug.Log($"[Tooltip] ShowTooltip on '{name}' → spawn point '{spawnPoint.name}' " +
                          $"(delay {delay:F2}s)", this);

            if (delay > 0f) pendingShow = StartCoroutine(ShowAfterDelay(content, delay));
            else spawnPoint.ShowFor(this, content);
        }

        /// <summary>Subclasses call this from their hover-exit detector.</summary>
        protected void HideTooltip()
        {
            CancelPendingShow();
            if (spawnPoint == null) return;
            if (!spawnPoint.IsShowingFor(this)) return;
            if (pendingHide != null) return;

            float grace = ResolveGrace();
            if (TooltipController.Verbose)
                Debug.Log($"[Tooltip] HideTooltip on '{name}' (grace {grace:F2}s)", this);
            if (grace > 0f) pendingHide = StartCoroutine(HideAfterGrace(grace));
            else spawnPoint.HideFor(this);
        }

        private static float ResolveDelay() =>
            TooltipController.Instance != null
                ? TooltipController.Instance.defaultShowDelay
                : 0.3f;

        private static float ResolveGrace() =>
            TooltipController.Instance != null
                ? TooltipController.Instance.defaultHideGrace
                : 0.05f;

        private IEnumerator ShowAfterDelay(TooltipContent content, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            pendingShow = null;
            if (spawnPoint != null) spawnPoint.ShowFor(this, content);
        }

        private IEnumerator HideAfterGrace(float grace)
        {
            yield return new WaitForSecondsRealtime(grace);
            pendingHide = null;
            if (spawnPoint != null) spawnPoint.HideFor(this);
        }

        private void CancelPendingShow()
        {
            if (pendingShow != null) { StopCoroutine(pendingShow); pendingShow = null; }
        }

        private void CancelPendingHide()
        {
            if (pendingHide != null) { StopCoroutine(pendingHide); pendingHide = null; }
        }

        protected virtual void OnDisable()
        {
            // Pointer-exit doesn't fire on disable; explicitly hide so the
            // tooltip drops when the trigger is hidden mid-hover (combat end,
            // refresh, slot evicted).
            CancelPendingShow();
            CancelPendingHide();
            if (spawnPoint != null) spawnPoint.HideFor(this);
        }
    }
}
