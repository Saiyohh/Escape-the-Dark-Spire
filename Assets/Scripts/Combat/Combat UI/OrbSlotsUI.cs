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

        private readonly HashSet<int> pendingChannelAnimations = new();
        private int lastFilledCount = 0;

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

            var currentParent = transform.parent;
            if (currentParent == null || currentParent.gameObject == null)
            {
                originalParent = null;
                wasReparented = false;
                return;
            }

            transform.SetParent(originalParent, worldPositionStays: false);
            transform.localPosition = originalLocalPosition;
            originalParent = null;
            wasReparented = false;
        }

        private void HandleOrbsChanged(Unit _)
        {
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
            if (boundUnit != null) OrbManager.ClearAll(boundUnit);
        }

        private void Refresh()
        {
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
                        ApplySpriteAndColor(go, sprite, color);
                    }

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

            if (nextEvokeIndicator != null)
            {
                bool show = filled > 0 && boundUnit.IsAlive;
                nextEvokeIndicator.SetActive(show);
                if (show && rightEndpoint != null)
                    nextEvokeIndicator.transform.localPosition = ToLocal(rightEndpoint);
            }
        }

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

        private System.Collections.IEnumerator ChannelGrow(Transform slotTransform)
        {
            if (slotTransform == null || channelGrowDuration <= 0f) yield break;
            Vector3 fullScale = slotTransform.localScale;
            slotTransform.localScale = fullScale * channelStartScale;

            float t = 0f;
            while (t < channelGrowDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / channelGrowDuration);
                float eased = 1f - (1f - u) * (1f - u); // ease-out quad
                slotTransform.localScale = Vector3.Lerp(fullScale * channelStartScale, fullScale, eased);
                yield return null;
            }
            slotTransform.localScale = fullScale;
        }

        [Header("Passive trigger VFX")]
        [Tooltip("Multiplier applied to a slot's local scale at the peak of its " +
                 "passive-trigger pulse. 1.25 ≈ a noticeable but subtle bounce.")]
        [SerializeField] private float passivePulseScale = 1.25f;
        [Tooltip("Total duration of one pulse (scale up + scale down). Should " +
                 "fit inside CombatManager.orbPassiveInterval.")]
        [SerializeField] private float passivePulseDuration = 0.30f;

        private void HandleOrbPassiveTriggered(Unit bearer, OrbInstance orb)
        {
            if (bearer != boundUnit || orb == null) return;
            if (boundUnit.orbs == null) return;

            int idx = boundUnit.orbs.IndexOf(orb);
            if (idx < 0 || idx >= spawnedSlots.Count) return;
            var go = spawnedSlots[idx];
            if (go == null || !go.activeInHierarchy) return;

            var view = idx < spawnedSlotViews.Count ? spawnedSlotViews[idx] : null;
            Transform pulseTarget = (view != null) ? view.VisualTransform : go.transform;
            StartCoroutine(PulseSlot(pulseTarget));
        }

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
            if (anchor.parent == transform) return anchor.localPosition;
            return transform.InverseTransformPoint(anchor.position);
        }

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

    }
}
