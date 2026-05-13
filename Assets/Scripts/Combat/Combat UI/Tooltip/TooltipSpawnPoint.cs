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

        public void ShowFor(object owner, TooltipContent content)
        {
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
            view.PlayFadeIn();
        }

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
            HideAll();
        }

        private void LateUpdate()
        {
            if (stack.Count > 0) Layout();
        }

        private void Layout()
        {
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

                LayoutRebuilder.ForceRebuildLayoutImmediate(view.PanelRoot);
                Vector2 size = view.PanelRoot.rect.size;

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
            if (Anchor is RectTransform rt)
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                Camera cam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera;
                return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            }

            var worldCam = worldAnchorCamera != null ? worldAnchorCamera : Camera.main;
            if (worldCam == null) return Vector2.zero;
            return worldCam.WorldToScreenPoint(Anchor.position);
        }

    }
}
