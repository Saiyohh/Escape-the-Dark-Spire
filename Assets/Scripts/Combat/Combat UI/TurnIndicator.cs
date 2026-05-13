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

        private Transform stableRoot;
        private Unit boundUnit;
        private float fadeT;

        private void Awake()
        {
            stableRoot = transform.parent;

            var stale = GetComponent<WorldFollow>();
            if (stale != null) Destroy(stale);

            EnsureRenderer();
            ApplySortingToRenderer();

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

        private void HandleUnitTurnStart(Unit unit)
        {
            if (unit == null || !unit.IsAlive) return;
            var display = UnitDisplay.GetDisplay(unit);
            if (display == null) return;

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

            var currentParent = transform.parent;
            if (currentParent == null || currentParent.gameObject == null) return;

            transform.SetParent(stableRoot, worldPositionStays: false);
        }

        private void LateUpdate()
        {
            if (indicatorRenderer == null) return;

            float wave = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float pulseAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

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

    }
}
