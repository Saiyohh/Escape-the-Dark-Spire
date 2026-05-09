// TurnIndicator.cs
// -----------------------------------------------------------------------------
// Active-turn aura that sits at the active unit's feet and pulses softly to
// signal whose turn it is. Replaces the old above-the-head bobbing sprite —
// the previous design fought above-the-head UI like the orb tray; the aura
// approach moves the indicator down to the unit's pivot where it doesn't
// compete with anything authored above the head.
//
// Lifecycle (parent-swap, like the prior version):
//   • Lives in the scene as a child of CombatUI (or whatever parent it was
//     authored under). That parent is captured in Awake as the "stable root."
//   • OnUnitTurnStart → reparent under the active UnitDisplay, snap to the
//     unit's pivot (localPosition = pulseOffset), start pulsing.
//   • OnUnitTurnEnd  → reparent back to the stable root and hide.
//   • If the bound unit dies mid-turn, detach immediately so the indicator
//     survives the unit's destruction.
//
// Visual:
//   • Bottom-center-pivoted sprite. The unit's transform pivot is also bottom-
//     center, so localPosition = pulseOffset (default zero) places the aura
//     under the unit's feet without further math.
//   • Alpha oscillates between minAlpha and maxAlpha at pulseFrequency Hz
//     using a sin curve. RGB stays at the configured tint (default white).
//   • Sorting order defaults to ABOVE the unit sprite but BELOW the HUD
//     (HP bars / conditions / orb tray). Tweak per scene if needed.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [DisallowMultipleComponent]
    public class TurnIndicator : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("SpriteRenderer for the aura. Pivot should be bottom-center so " +
                 "the aura sits at the unit's feet when localPosition is zero. " +
                 "Auto-found in children if left blank.")]
        [SerializeField] private SpriteRenderer indicatorRenderer;

        [Tooltip("Tint applied to the aura's RGB. Alpha is driven by the pulse.")]
        [SerializeField] private Color tint = Color.white;

        [Header("Pulse")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.40f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.65f;
        [Tooltip("Pulse cycles per second. 0.6 ≈ a slow breath.")]
        [SerializeField] private float pulseFrequency = 0.6f;

        [Header("Anchor")]
        [Tooltip("Local offset from the unit's pivot. The unit's pivot is " +
                 "bottom-center already, so zero = at the feet. Nudge Y up " +
                 "slightly if the aura art has empty space at the top.")]
        [SerializeField] private Vector3 pulseOffset = Vector3.zero;

        [Header("Sorting")]
        [Tooltip("Sorting layer for the aura. Project convention: 'Unit Overlays' " +
                 "is the dedicated stratum for everything that renders on top of " +
                 "the unit sprite — turn indicator, HP/SP bars, conditions, etc. " +
                 "Order 0 puts the aura at the BACK of that stratum so HUD " +
                 "elements at higher orders sit above it.")]
        [SerializeField] private string sortingLayer = "Unit Overlays";

        [Tooltip("Sorting order within the Unit Overlays layer. 0 places the " +
                 "aura at the back of the stratum; HP bars / conditions / orb " +
                 "tray live at higher orders and render in front of it.")]
        [SerializeField] private int sortingOrder = 0;

        [Header("Fade In")]
        [Tooltip("Seconds to fade up from 0 to the live pulse range when a " +
                 "new turn starts. 0 = pop instantly.")]
        [SerializeField] private float fadeInDuration = 0.15f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Transform stableRoot;
        private Unit boundUnit;
        private float fadeT;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            stableRoot = transform.parent;

            // Drop any stale WorldFollow component left on older prefabs.
            var stale = GetComponent<WorldFollow>();
            if (stale != null) Destroy(stale);

            EnsureRenderer();
            ApplySortingToRenderer();

            // Subscribe in Awake (not OnEnable) so the disabled-during-Awake
            // GameObject doesn't drop the subscription.
            CombatEvents.OnUnitTurnStart += HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd   += HandleUnitTurnEnd;

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            CombatEvents.OnUnitTurnStart -= HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd   -= HandleUnitTurnEnd;
            UnbindUnit();
        }

        private void EnsureRenderer()
        {
            if (indicatorRenderer == null)
                indicatorRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (indicatorRenderer == null)
            {
                // Runtime fallback so the aura is visible even before art lands.
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                var sprite = Sprite.Create(
                    tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), pixelsPerUnit: 1f);
                sprite.name = "RuntimeTurnAura";

                var spriteGO = new GameObject("Aura");
                spriteGO.transform.SetParent(transform, worldPositionStays: false);
                spriteGO.transform.localScale = new Vector3(1.5f, 0.4f, 1f);
                indicatorRenderer = spriteGO.AddComponent<SpriteRenderer>();
                indicatorRenderer.sprite = sprite;
                indicatorRenderer.color = tint;
            }
        }

        private void ApplySortingToRenderer()
        {
            if (indicatorRenderer == null) return;
            indicatorRenderer.sortingLayerName = sortingLayer;
            indicatorRenderer.sortingOrder = sortingOrder;
        }

        // ─── Turn events ─────────────────────────────────────────────────────

        private void HandleUnitTurnStart(Unit unit)
        {
            if (unit == null || !unit.IsAlive) return;
            var display = UnitDisplay.GetDisplay(unit);
            if (display == null) return;

            // Reparent so the aura rides along with rank shifts and any
            // unit-display motion (lunge, knockback). The unit's pivot is
            // bottom-center, so localPosition = pulseOffset places the
            // aura at the feet by default.
            transform.SetParent(display.transform, worldPositionStays: false);
            transform.localPosition = pulseOffset;
            transform.localRotation = Quaternion.identity;

            BindUnit(unit);

            fadeT = 0f;
            ApplyAlpha(minAlpha); // start at the bottom of the range, then breathe up

            gameObject.SetActive(true);
        }

        private void HandleUnitTurnEnd(Unit unit)
        {
            DetachToStableRoot();
            gameObject.SetActive(false);
        }

        private void HandleBoundUnitDeath()
        {
            DetachToStableRoot();
            gameObject.SetActive(false);
        }

        private void BindUnit(Unit unit)
        {
            if (boundUnit == unit) return;
            UnbindUnit();
            boundUnit = unit;
            if (boundUnit != null) boundUnit.OnDeath += HandleBoundUnitDeath;
        }

        private void UnbindUnit()
        {
            if (boundUnit != null) boundUnit.OnDeath -= HandleBoundUnitDeath;
            boundUnit = null;
        }

        private void DetachToStableRoot()
        {
            UnbindUnit();
            if (stableRoot == null) return;

            // Mirror the OrbSlotsUI fix — bail if the current parent is gone
            // (scene unload, play-mode exit). Unity refuses SetParent during
            // a parent's deactivation.
            var currentParent = transform.parent;
            if (currentParent == null || currentParent.gameObject == null) return;

            transform.SetParent(stableRoot, worldPositionStays: false);
        }

        // ─── Alpha pulse ─────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (indicatorRenderer == null) return;

            // 0..1 sine ramp. Multiply through min/max range.
            float wave = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float pulseAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

            // Fade-in scales the pulse alpha up from 0 over fadeInDuration.
            float effective = pulseAlpha;
            if (fadeT < 1f && fadeInDuration > 0f)
            {
                fadeT = Mathf.Clamp01(fadeT + Time.deltaTime / fadeInDuration);
                effective = pulseAlpha * fadeT;
            }

            ApplyAlpha(effective);
        }

        private void ApplyAlpha(float a)
        {
            var c = tint;
            c.a = a;
            indicatorRenderer.color = c;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minAlpha > maxAlpha) (minAlpha, maxAlpha) = (maxAlpha, minAlpha);
            ApplySortingToRenderer();
        }
#endif
    }
}
