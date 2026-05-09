// TooltipSpawnPoint.cs
// -----------------------------------------------------------------------------
// A single anchor + stack-direction policy for tooltips. Drop on any
// container that hosts tooltip-triggers (the orb tray, a party member's
// condition row, the skill info panel, etc.). Triggers within the
// container's hierarchy resolve to this spawn point automatically via
// GetComponentInParent.
//
// All tooltips fired from triggers under this spawn point appear at the
// same anchor and stack in a configured direction:
//
//   • growDirection      — Up or Down. Successive tooltips stack along
//                          this axis.
//   • horizontalAlign    — Right (anchor = LEFT edge of each tooltip) or
//                          Left (anchor = RIGHT edge). Matches the
//                          author's mental model of "tooltips extend
//                          rightward/leftward from this point."
//   • gap                — Pixels between the anchor and the first
//                          tooltip, and between adjacent tooltips.
//
// Stack semantics:
//   • Each call to ShowFor(owner, content) either adds a new tooltip
//     (if owner is new) or updates an existing one (same owner). Layout
//     re-runs after each change.
//   • HideFor(owner) removes that owner's tooltip and recompacts.
//   • The spawn point re-layouts each frame in LateUpdate if anything is
//     visible, so tooltips track an anchor that moves at runtime (e.g.
//     the orb tray reparents under the bearer's UnitDisplay, which slides
//     between rank positions).
//
// Pool ownership: the spawn point allocates tooltip views from
// TooltipController.Instance and releases them back when removed.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public enum TooltipGrowDirection { Up, Down }
    public enum TooltipHorizontalAlign { Right, Left }

    [AddComponentMenu("DarkSpire/Tooltip/Tooltip Spawn Point")]
    public class TooltipSpawnPoint : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("Optional explicit anchor transform. If null, uses this " +
                 "GameObject's transform as the anchor.")]
        [SerializeField] private Transform anchorOverride;

        [Header("Stacking")]
        [Tooltip("Direction the stack grows from the anchor. Down = first " +
                 "tooltip below the anchor, subsequent tooltips stack further " +
                 "downward. Up = above. Pick whichever direction has empty " +
                 "screen space — orb tray sits at the top so use Down; skill " +
                 "info panel sits at the bottom so use Up.")]
        public TooltipGrowDirection growDirection = TooltipGrowDirection.Down;

        [Tooltip("Whether tooltips extend rightward or leftward from the " +
                 "anchor's X. Right: the anchor is at the LEFT edge of each " +
                 "tooltip's panel. Left: the anchor is at the RIGHT edge.")]
        public TooltipHorizontalAlign horizontalAlign = TooltipHorizontalAlign.Right;

        [Tooltip("Pixel gap between the anchor and the first tooltip, and " +
                 "between adjacent tooltips in the stack.")]
        [Min(0f)] public float gap = 6f;

        [Header("World-space anchor")]
        [Tooltip("Camera used to project a non-RectTransform (world-space) " +
                 "anchor to screen pixels. If null, falls back to Camera.main. " +
                 "Ignored when the anchor is a RectTransform.")]
        [SerializeField] private Camera worldAnchorCamera;

        // Insertion-ordered stack of (owner, view) pairs. Owners are arbitrary
        // objects — typically TooltipTrigger instances. Layout walks this in
        // order to position each tooltip.
        private readonly List<Entry> stack = new();

        private struct Entry
        {
            public object owner;
            public TooltipView view;
        }

        public Transform Anchor => anchorOverride != null ? anchorOverride : transform;

        public bool HasAnyVisible => stack.Count > 0;

        public bool IsShowingFor(object owner)
        {
            for (int i = 0; i < stack.Count; i++)
                if (ReferenceEquals(stack[i].owner, owner)) return true;
            return false;
        }

        /// <summary>Show or update a tooltip owned by <paramref name="owner"/>.
        /// If the owner already has a tooltip in this stack, content is
        /// updated in place; otherwise a new tooltip is appended.</summary>
        public void ShowFor(object owner, TooltipContent content)
        {
            // Update existing.
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i].owner, owner))
                {
                    if (TooltipController.Verbose)
                        Debug.Log($"[Tooltip] SpawnPoint '{name}' update existing for owner {owner}", this);
                    stack[i].view.SetContent(content);
                    Layout();
                    return;
                }
            }

            // Allocate a fresh view.
            var ctrl = TooltipController.Instance;
            if (ctrl == null)
            {
                Debug.LogWarning($"[Tooltip] SpawnPoint '{name}' aborted ShowFor — TooltipController.Instance is null. " +
                                 "Drop a TooltipController in the scene.", this);
                return;
            }
            var view = ctrl.Allocate();
            if (view == null)
            {
                if (TooltipController.Verbose)
                    Debug.Log($"[Tooltip] SpawnPoint '{name}' aborted ShowFor — Allocate returned null (see prior warning)", this);
                return;
            }
            view.SetContent(content);
            stack.Add(new Entry { owner = owner, view = view });
            if (TooltipController.Verbose)
                Debug.Log($"[Tooltip] SpawnPoint '{name}' added view (stack size now {stack.Count})", this);
            Layout();
            // Fade-in happens AFTER Layout so the panel's first visible
            // frame is already at the correct screen position. Only fires
            // on a fresh add — content updates on existing entries (the
            // top-of-method early return) don't replay the fade, and
            // LateUpdate's per-frame Layout calls don't either.
            view.PlayFadeIn();
        }

        /// <summary>Remove the tooltip owned by <paramref name="owner"/> and
        /// recompact the stack.</summary>
        public void HideFor(object owner)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i].owner, owner))
                {
                    var ctrl = TooltipController.Instance;
                    if (ctrl != null) ctrl.Release(stack[i].view);
                    stack.RemoveAt(i);
                    if (TooltipController.Verbose)
                        Debug.Log($"[Tooltip] SpawnPoint '{name}' removed view (stack size now {stack.Count})", this);
                    Layout();
                    return;
                }
            }
        }

        public void HideAll()
        {
            var ctrl = TooltipController.Instance;
            for (int i = 0; i < stack.Count; i++)
                if (ctrl != null) ctrl.Release(stack[i].view);
            stack.Clear();
        }

        private void OnDisable()
        {
            // Free pooled views — don't leak across scene unloads or
            // container deactivation.
            HideAll();
        }

        private void LateUpdate()
        {
            // Cheap re-layout each frame while visible — handles anchors that
            // move at runtime (orb tray rides along when rank shifts pull the
            // bearer's UnitDisplay between rank positions). When nothing is
            // visible, this is a no-op.
            if (stack.Count > 0) Layout();
        }

        private void Layout()
        {
            // Drop any null views first (released externally / destroyed).
            for (int i = stack.Count - 1; i >= 0; i--)
                if (stack[i].view == null) stack.RemoveAt(i);

            if (stack.Count == 0) return;

            Vector2 anchorScreen = ProjectAnchorToScreen();
            float yDir = (growDirection == TooltipGrowDirection.Up) ? 1f : -1f;
            float yOffset = 0f;

            for (int i = 0; i < stack.Count; i++)
            {
                var view = stack[i].view;
                if (view == null || view.PanelRoot == null) continue;

                // Force layout so we read the current preferred size.
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.PanelRoot);
                Vector2 size = view.PanelRoot.rect.size;

                // Add gap before this tooltip; first iteration uses one gap
                // between anchor and tooltip 0. Then offset by half-height
                // because the panel pivot is centered.
                yOffset += gap;
                float screenY = anchorScreen.y + yDir * (yOffset + size.y * 0.5f);

                float screenX;
                if (horizontalAlign == TooltipHorizontalAlign.Right)
                    screenX = anchorScreen.x + size.x * 0.5f;
                else
                    screenX = anchorScreen.x - size.x * 0.5f;

                view.SetScreenPosition(new Vector2(screenX, screenY));

                if (TooltipController.Verbose)
                    Debug.Log($"[Tooltip] SpawnPoint '{name}' laid out tooltip {i} " +
                              $"at screen ({screenX:F0},{screenY:F0}), size ({size.x:F0}x{size.y:F0})", this);

                yOffset += size.y;
            }
        }

        private Vector2 ProjectAnchorToScreen()
        {
            // RectTransform path — find its canvas + camera.
            if (Anchor is RectTransform rt)
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                Camera cam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera;
                return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            }

            // World-space path — main camera (or override).
            var worldCam = worldAnchorCamera != null ? worldAnchorCamera : Camera.main;
            if (worldCam == null) return Vector2.zero;
            return worldCam.WorldToScreenPoint(Anchor.position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = Anchor.position;
            Gizmos.DrawWireSphere(p, 0.05f);

            // Indicator arrow pointing in the grow direction.
            Vector3 dir = growDirection == TooltipGrowDirection.Up ? Vector3.up : Vector3.down;
            Gizmos.DrawLine(p, p + dir * 0.25f);
            Vector3 hDir = horizontalAlign == TooltipHorizontalAlign.Right ? Vector3.right : Vector3.left;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, p + hDir * 0.15f);
        }
#endif
    }
}
