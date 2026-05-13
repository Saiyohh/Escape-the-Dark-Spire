using System.Collections;
using UnityEngine;

namespace DarkSpire
{
    public abstract class TooltipTrigger : MonoBehaviour
    {
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

        protected abstract bool BuildContent(out TooltipContent content);

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

            float delay = ResolveDelay();
            if (spawnPoint.HasAnyVisible) delay = 0f;

            if (verbose)
                Debug.Log($"[Tooltip] ShowTooltip on '{name}' → spawn point '{spawnPoint.name}' " +
                          $"(delay {delay:F2}s)", this);

            if (delay > 0f) pendingShow = StartCoroutine(ShowAfterDelay(content, delay));
            else spawnPoint.ShowFor(this, content);
        }

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
            CancelPendingShow();
            CancelPendingHide();
            if (spawnPoint != null) spawnPoint.HideFor(this);
        }
    }
}
