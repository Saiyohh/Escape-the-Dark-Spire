// OrbSlotsUI.cs
// -----------------------------------------------------------------------------
// HUD widget that displays the orb queue for the orb-bearer (Defect) in the
// player party. Subscribes to CombatEvents.OnCombatStart to locate the unit
// flagged hasOrbSystem, then listens to that Unit.OnOrbsChanged for live
// updates as orbs are channeled and evoked.
//
// Layout — parametric "camel arc" between two endpoints:
//   • rightEndpoint anchors slot 0 (the evoke chamber — always Evoked first).
//   • leftEndpoint anchors slot (orbSlotMax - 1).
//   • Intermediate slots distribute evenly along the line, with vertical arc
//     offset = arcHeight * 4t(1-t) so endpoints sit on the line and the
//     apex (t=0.5) is the highest point.
//   • For 3 slots: slot 0 = far right, slot 1 = apex of arc, slot 2 = far left.
//
// Fill semantics (driven by OrbManager, surfaced here visually):
//   • Channels go to the rightmost EMPTY slot — when the tray is empty the
//     first orb lands at slot 0 (right); each subsequent channel marches
//     leftward through slot 1, slot 2, etc.
//   • When slot 0 Evokes, the surviving orbs shift one slot to the right;
//     the freed slot is always on the LEFT side of the tray.
//   • Empty slots therefore always sit on the LEFT of the filled section.
//
// Slot widgets are spawned from `slotPrefab` at runtime and reused — when
// orbSlotMax shrinks (theoretically; not yet wired), excess widgets are
// disabled rather than destroyed. Empty slots show `emptySlotSprite` tinted
// with `emptySlotColor`; filled slots show the OrbDataSO.icon tinted with
// the orb's vfxColor.
//
// Layout space: this component reads endpoint Transform.localPosition values,
// so the script works equally well anchored to the Defect's UnitDisplay
// (world-space, sits above the head) or under a screen-space HUD canvas
// (RectTransforms work via .localPosition the same way). The slotPrefab is
// instantiated as a child of this component's transform; author its visual
// component (Image, SpriteRenderer, etc.) to match the parent canvas.
//
// World-space follow:
//   When `anchorToBearerDisplay = true`, the OrbSlotsUI reparents itself
//   under the bearer's UnitDisplay transform at combat start (with a local
//   offset). Rank shifts already lerp the UnitDisplay between rank-position
//   transforms, and the orb tray rides along automatically as a child.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class OrbSlotsUI : MonoBehaviour
    {
        [Header("Slot prefab")]
        [Tooltip("One slot's visual. Should have a UnityEngine.UI.Image OR a " +
                 "SpriteRenderer at the root or in a child — both are supported. " +
                 "Instantiated under this component's transform; layout uses " +
                 "Transform.localPosition.")]
        [SerializeField] private GameObject slotPrefab;

        [Header("Arc endpoints")]
        [Tooltip("Slot 0 (oldest, next to Evoke) sits here.")]
        [SerializeField] private Transform rightEndpoint;
        [Tooltip("Slot (orbSlotMax - 1) (newest) sits here. Intermediate slots " +
                 "distribute evenly between the two endpoints.")]
        [SerializeField] private Transform leftEndpoint;

        [Tooltip("Apex height of the camel arc above the straight line between " +
                 "endpoints. The arc passes through both endpoints with vertical " +
                 "offset 0, peaks at this value at the midpoint.")]
        [SerializeField] private float arcHeight = 0.5f;

        [Tooltip("Direction (in this transform's local space) the arc bulges " +
                 "toward. Default = up. Normalized internally.")]
        [SerializeField] private Vector3 arcUpDirection = Vector3.up;

        [Header("Empty slot visuals")]
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("Optional 'next to evoke' indicator")]
        [Tooltip("If set, this GameObject is repositioned to slot 0's location " +
                 "and shown only when slot 0 holds an orb.")]
        [SerializeField] private GameObject nextEvokeIndicator;

        [Header("World-space follow (Defect's tray above the head)")]
        [Tooltip("When true, the OrbTray reparents itself under the orb-bearer's " +
                 "UnitDisplay transform at combat start so it rides along with " +
                 "rank shifts. Leave false for a fixed screen-space HUD tray.")]
        [SerializeField] private bool anchorToBearerDisplay = true;

        [Tooltip("Local offset applied after reparenting under the bearer. " +
                 "Y > 0 puts the tray above the head; Z = 0 keeps it on the " +
                 "same plane as the unit sprite.")]
        [SerializeField] private Vector3 bearerAnchorOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Channel-in animation")]
        [Tooltip("Scale a slot starts at when an orb is freshly channeled. " +
                 "Coroutine grows it back to full size over channelGrowDuration.")]
        [Range(0.05f, 1f)] [SerializeField] private float channelStartScale = 0.2f;
        [SerializeField] private float channelGrowDuration = 0.18f;

        private Unit boundUnit;
        private Transform originalParent; // remember where we came from so OnCombatEnd can restore
        private Vector3 originalLocalPosition;
        private bool wasReparented;
        private readonly List<GameObject> spawnedSlots = new();
        private readonly List<OrbSlotView> spawnedSlotViews = new(); // 1:1 with spawnedSlots; entries may be null for legacy prefabs

        // Set of slot indices that should play the channel-in scale animation
        // on the next Refresh. Filled in HandleOrbsChanged when the filled
        // count grows; consumed in Refresh when applying transforms.
        private readonly HashSet<int> pendingChannelAnimations = new();
        private int lastFilledCount = 0;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            CombatEvents.OnCombatStart        += HandleCombatStart;
            CombatEvents.OnCombatEnd          += HandleCombatEnd;
            CombatEvents.OnOrbPassiveTriggered += HandleOrbPassiveTriggered;
            HandleCombatStart();
        }

        private void OnDisable()
        {
            CombatEvents.OnCombatStart         -= HandleCombatStart;
            CombatEvents.OnCombatEnd           -= HandleCombatEnd;
            CombatEvents.OnOrbPassiveTriggered -= HandleOrbPassiveTriggered;

            // Unhook the bound unit's events but DO NOT reparent the transform —
            // OnDisable can fire as part of scene unload / play-mode exit while
            // the bearer's UnitDisplay is also tearing down, and SetParent
            // during a parent's deactivation throws. Reparent restoration is
            // owned by HandleCombatEnd / re-bind, both of which run mid-runtime
            // when the hierarchy is stable.
            if (boundUnit != null)
            {
                boundUnit.OnOrbsChanged -= HandleOrbsChanged;
                boundUnit.OnDeath       -= HandleDeath;
                boundUnit = null;
            }
        }

        private void HandleCombatStart()
        {
            UnbindUnit();
            BindUnit(FindOrbBearer());
            Refresh();
        }

        private void HandleCombatEnd(bool _)
        {
            UnbindUnit();
            Refresh();
        }

        private static Unit FindOrbBearer()
        {
            var mgr = CombatManager.Instance;
            if (mgr == null) return null;
            foreach (var u in mgr.PlayerUnits)
            {
                if (u != null && u.characterData != null
                    && u.characterData.hasOrbSystem)
                    return u;
            }
            return null;
        }

        private void BindUnit(Unit u)
        {
            boundUnit = u;
            if (boundUnit != null)
            {
                boundUnit.OnOrbsChanged += HandleOrbsChanged;
                boundUnit.OnDeath       += HandleDeath;

                // Refresh number labels when Focus stacks change mid-turn.
                if (boundUnit.conditions != null)
                {
                    boundUnit.conditions.OnConditionApplied += HandleBearerConditionApplied;
                    boundUnit.conditions.OnConditionChanged += HandleBearerConditionChanged;
                    boundUnit.conditions.OnConditionRemoved += HandleBearerConditionRemoved;
                }

                if (anchorToBearerDisplay)
                    AttachToBearerDisplay();
            }
        }

        private void UnbindUnit()
        {
            if (boundUnit != null)
            {
                boundUnit.OnOrbsChanged -= HandleOrbsChanged;
                boundUnit.OnDeath       -= HandleDeath;
                if (boundUnit.conditions != null)
                {
                    boundUnit.conditions.OnConditionApplied -= HandleBearerConditionApplied;
                    boundUnit.conditions.OnConditionChanged -= HandleBearerConditionChanged;
                    boundUnit.conditions.OnConditionRemoved -= HandleBearerConditionRemoved;
                }
            }
            boundUnit = null;
            DetachFromBearerDisplay();
        }

        // Refresh only on Focus changes — every other condition change is
        // irrelevant to orb numbers and we don't want to repaint each tick.
        private void HandleBearerConditionApplied(ConditionID id, int _)
        {
            if (id == ConditionID.Focus) Refresh();
        }
        private void HandleBearerConditionChanged(ConditionID id, int _)
        {
            if (id == ConditionID.Focus) Refresh();
        }
        private void HandleBearerConditionRemoved(ConditionID id)
        {
            if (id == ConditionID.Focus) Refresh();
        }

        /// <summary>
        /// Reparent under the bearer's UnitDisplay so the tray follows the
        /// Defect when ranks shift. The UnitDisplay lerps between rank-position
        /// transforms; as a child of it, the tray inherits the motion.
        /// </summary>
        private void AttachToBearerDisplay()
        {
            if (boundUnit == null || wasReparented) return;
            var display = UnitDisplay.GetDisplay(boundUnit);
            if (display == null) return;

            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;

            transform.SetParent(display.transform, worldPositionStays: false);
            transform.localPosition = bearerAnchorOffset;
            transform.localRotation = Quaternion.identity;
            wasReparented = true;
        }

        private void DetachFromBearerDisplay()
        {
            if (!wasReparented) return;

            // If the current parent's GameObject is gone (destroyed in a
            // teardown we didn't see), Unity will refuse the SetParent. Just
            // clear our flags and let the engine clean up the hierarchy.
            var currentParent = transform.parent;
            if (currentParent == null || currentParent.gameObject == null)
            {
                originalParent = null;
                wasReparented = false;
                return;
            }

            // Restore the original parent so re-binding for a new combat
            // starts from a clean slate. worldPositionStays = false so we
            // pop back to the authored layout position.
            transform.SetParent(originalParent, worldPositionStays: false);
            transform.localPosition = originalLocalPosition;
            originalParent = null;
            wasReparented = false;
        }

        private void HandleOrbsChanged(Unit _)
        {
            // Detect newly-channeled orbs by comparing the filled count to
            // the last refresh. Any slot indices in [lastFilledCount, current)
            // are freshly added — flag them so Refresh plays the channel-in
            // grow animation on those specific slots.
            int currentFilled = (boundUnit != null && boundUnit.orbs != null)
                ? boundUnit.orbs.Count : 0;
            if (currentFilled > lastFilledCount)
            {
                for (int i = lastFilledCount; i < currentFilled; i++)
                    pendingChannelAnimations.Add(i);
            }
            lastFilledCount = currentFilled;
            Refresh();
        }

        private void HandleDeath()
        {
            // Drop the queue when the bearer dies — orbs don't tick on a dead
            // unit and they shouldn't linger in the HUD. ClearAll fires
            // OnOrbsChanged which lands us back in Refresh.
            if (boundUnit != null) OrbManager.ClearAll(boundUnit);
        }

        // ─── Layout + state refresh ──────────────────────────────────────────

        private void Refresh()
        {
            // No bearer (out of combat or non-Defect party): hide everything.
            if (boundUnit == null)
            {
                foreach (var go in spawnedSlots)
                    if (go != null) go.SetActive(false);
                if (nextEvokeIndicator != null) nextEvokeIndicator.SetActive(false);
                return;
            }

            int max = Mathf.Max(0, boundUnit.orbSlotMax);
            EnsureSlotCount(max);

            int filled = boundUnit.orbs != null ? boundUnit.orbs.Count : 0;

            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                var go = spawnedSlots[i];
                if (go == null) continue;

                bool inUse = i < max;
                go.SetActive(inUse);
                if (!inUse) continue;

                // Layout: visual position 0 = far right (slot 0), N-1 = far left.
                go.transform.localPosition = CurvePoint(i, max);

                OrbInstance orb = (i < filled) ? boundUnit.orbs[i] : null;
                var view = i < spawnedSlotViews.Count ? spawnedSlotViews[i] : null;

                if (orb != null && orb.data != null)
                {
                    Sprite sprite = orb.data.icon != null ? orb.data.icon : emptySlotSprite;
                    Color color = orb.data.vfxColor;

                    if (view != null)
                    {
                        int passive = OrbManager.GetPassiveDisplayValue(orb, boundUnit);
                        int active  = OrbManager.GetActiveDisplayValue(orb, boundUnit);
                        bool showActive = OrbManager.ShouldShowActiveOnIcon(orb);
                        view.RenderFilled(orb, sprite, color, passive, active, showActive);
                    }
                    else
                    {
                        // Legacy prefab without OrbSlotView — fall back to bare sprite/color.
                        ApplySpriteAndColor(go, sprite, color);
                    }

                    // Channel-in animation: when this slot was newly added in
                    // the most recent OnOrbsChanged, kick off a quick scale
                    // pop from channelStartScale → 1 so the orb visibly
                    // "comes in" rather than just appearing.
                    //
                    // Scale the slot's VISUAL child (sprite renderer) — not
                    // the slot root — so the BoxCollider2D used by
                    // OrbTooltipTrigger keeps its authored size during the
                    // animation. Falls back to the slot root if the prefab
                    // doesn't carve out a visual child.
                    if (pendingChannelAnimations.Remove(i))
                    {
                        Transform animTarget = (view != null) ? view.VisualTransform : go.transform;
                        StartCoroutine(ChannelGrow(animTarget));
                    }
                }
                else
                {
                    if (view != null)
                        view.RenderEmpty(emptySlotSprite, emptySlotColor);
                    else
                        ApplySpriteAndColor(go, emptySlotSprite, emptySlotColor);
                }
            }

            // "Next to evoke" indicator pinned to slot 0 (right endpoint), only
            // when slot 0 actually holds an orb.
            if (nextEvokeIndicator != null)
            {
                bool show = filled > 0 && boundUnit.IsAlive;
                nextEvokeIndicator.SetActive(show);
                if (show && rightEndpoint != null)
                    nextEvokeIndicator.transform.localPosition = ToLocal(rightEndpoint);
            }
        }

        /// <summary>
        /// Spawn additional slot GameObjects from the prefab until we have at
        /// least `count` of them. Existing slots are reused. Excess slots
        /// beyond `count` are deactivated by Refresh, not destroyed, so the
        /// pool is stable across orbSlotMax changes (e.g. a future aspect
        /// granting +1 slot mid-combat).
        ///
        /// Also caches the per-slot OrbSlotView component (or null if the
        /// prefab predates that authoring shape) for the number-display path.
        /// </summary>
        private void EnsureSlotCount(int count)
        {
            if (slotPrefab == null) return;
            while (spawnedSlots.Count < count)
            {
                var go = Instantiate(slotPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                spawnedSlots.Add(go);
                spawnedSlotViews.Add(go.GetComponentInChildren<OrbSlotView>(true));
            }
        }

        /// <summary>
        /// Parametric position along the camel arc.
        /// Slot 0 → right endpoint (t=0). Slot (n-1) → left endpoint (t=1).
        /// Intermediate slots distribute evenly. The vertical arc offset is
        /// arcHeight * 4t(1-t), so endpoints sit on the line and the apex
        /// sits halfway between them.
        ///
        /// Single-slot edge case: place at the midpoint with full arc apex.
        /// </summary>
        private Vector3 CurvePoint(int slotIndex, int slotCount)
        {
            Vector3 right = ToLocal(rightEndpoint);
            Vector3 left  = ToLocal(leftEndpoint);

            float t;
            if (slotCount <= 1)
                t = 0.5f; // single slot — place at apex
            else
                t = (float)slotIndex / (slotCount - 1);

            Vector3 onLine = Vector3.Lerp(right, left, t);
            Vector3 up = arcUpDirection.sqrMagnitude > 0.0001f ? arcUpDirection.normalized : Vector3.up;
            float arcOffset = arcHeight * 4f * t * (1f - t);
            return onLine + up * arcOffset;
        }

        // ─── Channel-in spawn animation ──────────────────────────────────────

        /// <summary>
        /// Quick scale pop from channelStartScale to the slot's authored
        /// localScale. Used to sell "an orb just got channeled" without
        /// needing a per-orb particle effect. Caller should ensure the slot
        /// has its final scale on its transform — we read it as the target
        /// and snap-set the start scale before lerping back.
        /// </summary>
        private System.Collections.IEnumerator ChannelGrow(Transform slotTransform)
        {
            if (slotTransform == null || channelGrowDuration <= 0f) yield break;
            Vector3 fullScale = slotTransform.localScale;
            // Author scale could be non-1; multiply by a fraction rather than
            // hard-setting to (channelStartScale,...) so non-uniform scales survive.
            slotTransform.localScale = fullScale * channelStartScale;

            float t = 0f;
            while (t < channelGrowDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / channelGrowDuration);
                // Slight overshoot for a "pop" feel — eases out past 1, settles.
                float eased = 1f - (1f - u) * (1f - u); // ease-out quad
                slotTransform.localScale = Vector3.Lerp(fullScale * channelStartScale, fullScale, eased);
                yield return null;
            }
            slotTransform.localScale = fullScale;
        }

        // ─── Per-orb passive VFX ─────────────────────────────────────────────

        [Header("Passive trigger VFX")]
        [Tooltip("Multiplier applied to a slot's local scale at the peak of its " +
                 "passive-trigger pulse. 1.25 ≈ a noticeable but subtle bounce.")]
        [SerializeField] private float passivePulseScale = 1.25f;
        [Tooltip("Total duration of one pulse (scale up + scale down). Should " +
                 "fit inside CombatManager.orbPassiveInterval.")]
        [SerializeField] private float passivePulseDuration = 0.30f;

        private void HandleOrbPassiveTriggered(Unit bearer, OrbInstance orb)
        {
            // Only react to the orb-bearer we're bound to.
            if (bearer != boundUnit || orb == null) return;
            if (boundUnit.orbs == null) return;

            int idx = boundUnit.orbs.IndexOf(orb);
            if (idx < 0 || idx >= spawnedSlots.Count) return;
            var go = spawnedSlots[idx];
            if (go == null || !go.activeInHierarchy) return;

            // Pulse the visual child rather than the slot root so the
            // collider used by OrbTooltipTrigger keeps its authored size
            // and the hit area doesn't pulse along with the sprite.
            var view = idx < spawnedSlotViews.Count ? spawnedSlotViews[idx] : null;
            Transform pulseTarget = (view != null) ? view.VisualTransform : go.transform;
            StartCoroutine(PulseSlot(pulseTarget));
        }

        /// <summary>
        /// Quick scale-up-then-down on the slot's transform. Placeholder
        /// "orb hits" feedback until per-orb VFX prefabs land. Coroutine is
        /// fire-and-forget; multiple overlapping pulses on the same slot
        /// just last-write-wins on the scale, which looks fine for the
        /// pacing we control via CombatManager.orbPassiveInterval.
        /// </summary>
        private System.Collections.IEnumerator PulseSlot(Transform slotTransform)
        {
            if (slotTransform == null || passivePulseDuration <= 0f) yield break;
            Vector3 baseScale = slotTransform.localScale;
            Vector3 peakScale = baseScale * passivePulseScale;

            float half = passivePulseDuration * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                slotTransform.localScale = Vector3.Lerp(baseScale, peakScale, u);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                slotTransform.localScale = Vector3.Lerp(peakScale, baseScale, u);
                yield return null;
            }
            slotTransform.localScale = baseScale;
        }

        private Vector3 ToLocal(Transform anchor)
        {
            if (anchor == null) return Vector3.zero;
            // Anchors are siblings/children of this transform — read their
            // localPosition directly. If an authoring setup ever uses a
            // distant anchor, fall back to InverseTransformPoint.
            if (anchor.parent == transform) return anchor.localPosition;
            return transform.InverseTransformPoint(anchor.position);
        }

        /// <summary>
        /// Set the slot's sprite + color through whichever renderer it has —
        /// UI Image (canvas) or SpriteRenderer (world-space). Looks at the
        /// root and one level of children.
        /// </summary>
        private static void ApplySpriteAndColor(GameObject slot, Sprite sprite, Color color)
        {
            var img = slot.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                img.sprite = sprite;
                img.color  = color;
                return;
            }
            var sr = slot.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                sr.sprite = sprite;
                sr.color  = color;
            }
        }

#if UNITY_EDITOR
        // Visualize the arc in the editor so authoring the endpoints is easy.
        private void OnDrawGizmosSelected()
        {
            if (rightEndpoint == null || leftEndpoint == null) return;
            Vector3 right = rightEndpoint.position;
            Vector3 left  = leftEndpoint.position;
            Vector3 up = transform.TransformDirection(
                arcUpDirection.sqrMagnitude > 0.0001f ? arcUpDirection.normalized : Vector3.up);

            Gizmos.color = Color.cyan;
            const int segments = 24;
            Vector3 prev = right;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 onLine = Vector3.Lerp(right, left, t);
                Vector3 p = onLine + up * (arcHeight * 4f * t * (1f - t));
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(right, 0.05f);
            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(left, 0.05f);
        }
#endif
    }
}
