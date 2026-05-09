// EnemyWorldHUD.cs
// -----------------------------------------------------------------------------
// World-space enemy HUD. Two regions:
//   BELOW the sprite: HP bar + text, optional DEF badge
//   ABOVE the sprite: intent strip (icons for what the enemy will do this turn)
//                    + a ChanceBox (hover-only; shows hit/afflict %)
//
// UnitDisplay spawns this from the enemyWorldHUDPrefab during Initialize, and
// positions the whole prefab at the enemy's feet (enemyWorldHUDOffset). The
// intent panel offsets itself UP by intentRelativeY so it sits above the head
// even though the canvas anchor is at the feet.
//
// Prefab setup:
//   HUD_Enemy (GameObject)
//     Canvas (World Space, Scale 0.01)
//     + GraphicRaycaster
//     RectTransform
//     ├─ BelowRegion (anchored at bottom)
//     │    ├─ HpBar (Slider)
//     │    │    └─ HpText (TMP)
//     │    └─ DefBadge (Image + TMP) — only shown when defense > 0
//     └─ AboveRegion (anchored at top; local Y = intentRelativeY)
//          ├─ IntentContainer (HorizontalLayoutGroup)
//          │    └─ IntentIconUI children spawn at runtime
//          └─ ChanceBox (RectTransform anchored top-right of unit's hitbox;
//                         child of this HUD but positioned to hitbox corner
//                         by ChanceBox.Initialize using UnitDisplay.HitboxSize)
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class EnemyWorldHUD : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Slider hpBar;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("Seconds the HP fill takes to lerp to the new value. Set to 0 to snap.")]
        [SerializeField] private float hpTweenDuration = 0.3f;
        private Coroutine hpTweenCo;

        [Header("Defense")]
        [SerializeField] private GameObject defBadge;
        [SerializeField] private TMP_Text defText;

        [Header("Intent")]
        [Tooltip("Anchors the above-the-head region. Moved up by intentRelativeY in Initialize.")]
        [SerializeField] private RectTransform aboveRegion;
        [Tooltip("RectTransform with a HorizontalLayoutGroup.")]
        [SerializeField] private RectTransform intentContainer;
        [Tooltip("Prefab for each intent icon (needs an IntentIconUI component).")]
        [SerializeField] private GameObject intentIconPrefab;

        [Header("Conditions")]
        [Tooltip("RectTransform with a HorizontalLayoutGroup — condition icons spawn " +
                 "as children when the enemy receives a condition.")]
        [SerializeField] private RectTransform conditionsContainer;
        [Tooltip("Prefab for each condition icon (needs a ConditionIconUI component). " +
                 "Usually the same prefab used by UnitWorldHUD.")]
        [SerializeField] private GameObject conditionIconPrefab;

        [Header("Chance Box (hover-only)")]
        [SerializeField] private ChanceBox chanceBox;

        [Header("Intent Pulse")]
        [SerializeField] private float pulseScale = 1.25f;
        [SerializeField] private float pulseDuration = 0.25f;

        [Header("Death Fade")]
        [Tooltip("Seconds to fade the HP bar / intent strip / conditions to 0 alpha " +
                 "when the linked unit dies. Quick by design — the sprite's longer " +
                 "drop animation owns the rest of the death beat.")]
        [SerializeField] private float deathFadeOutDuration = 0.25f;

        [Header("Tier Badge (Elite / Boss)")]
        [Tooltip("Wrapper GameObject for the elite/boss tag — hidden when " +
                 "EnemyData.enemyType == Normal, shown otherwise.")]
        [SerializeField] private GameObject tierBadge;
        [Tooltip("Text label inside tierBadge. Set to ELITE / BOSS at Initialize.")]
        [SerializeField] private TMP_Text tierLabel;
        [Tooltip("Optional background image inside tierBadge. Recolored to silver " +
                 "(Elite) or crimson (Boss) at Initialize.")]
        [SerializeField] private Image tierBadgeBg;

        [Header("Hover Name Overlay")]
        [Tooltip("Wrap HP / DEF (and any other 'stats' visuals you want to fade) " +
                 "under one CanvasGroup so they cross-fade with the name label on " +
                 "hover. Leave null to disable the fade.")]
        [SerializeField] private CanvasGroup statsGroup;
        [Tooltip("CanvasGroup containing the name label + drop-shadow image. Fades " +
                 "in while hovered, out otherwise.")]
        [SerializeField] private CanvasGroup nameGroup;
        [Tooltip("Optional text label inside nameGroup. Auto-populated from " +
                 "linkedUnit.unitName at Initialize.")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Seconds for the hover fade between bars and name overlay.")]
        [SerializeField] private float hoverFadeDuration = 0.18f;
        private Coroutine hoverFadeCo;
        private bool isHovered;

        private Unit linkedUnit;
        private readonly List<IntentIconUI> intentIcons = new();
        private readonly Dictionary<ConditionID, ConditionIconUI> conditionIcons = new();
        private EnemyMove currentMove;

        private Action<int> onDamageTakenHandler;
        private Action<int> onHealReceivedHandler;
        private Action<int> onDefenseChangedHandler;
        private Action onStatsChangedHandler;
        private Action onDeathHandler;
        private Action<ConditionID, int> onConditionAppliedHandler;
        private Action<ConditionID> onConditionRemovedHandler;
        private Action<ConditionID, int> onConditionChangedHandler;

        public void Initialize(Unit unit, float intentRelativeY)
        {
            linkedUnit = unit;
            if (unit == null) return;

            if (aboveRegion != null)
            {
                var pos = aboveRegion.anchoredPosition;
                // intentRelativeY is in WORLD units (e.g. 2.0 = 2 units above
                // feet). Anchored position is in UGUI pixels relative to the
                // hosting Canvas. Convert by dividing by the canvas scale —
                // typically 0.01, giving 100 UGUI px per world unit.
                //
                // Pull the canvas scale from the SHARED CombatUIManager.WorldCanvas
                // first (the HUD is now its child); fall back to this transform's
                // local scale for legacy HUD prefabs that still carry their own
                // Canvas component.
                float canvasScale = 1f;
                if (CombatUIManager.WorldCanvas != null)
                    canvasScale = CombatUIManager.WorldCanvas.transform.localScale.x;
                else if (transform.localScale.x > 0.0001f)
                    canvasScale = transform.localScale.x;

                pos.y = intentRelativeY / Mathf.Max(0.0001f, canvasScale);
                aboveRegion.anchoredPosition = pos;
            }

            // Initial paint
            RefreshHP();
            RefreshDefense();
            RebuildConditions();
            InitializeHoverOverlay(unit);
            InitializeTierBadge(unit);

            // Event wiring — every refresh path routes through IsSuppressed
            // so HP / DEF / condition updates wait for the attacker's
            // animation to finish before they paint to the screen.
            onDamageTakenHandler       = _ => { if (!IsSuppressed) RefreshHP(); };
            onHealReceivedHandler      = _ => { if (!IsSuppressed) RefreshHP(); };
            onDefenseChangedHandler    = v => { if (!IsSuppressed) RefreshDefense(v); };
            onStatsChangedHandler      = () => { if (!IsSuppressed) { RefreshHP(); RefreshDefense(); } };
            onDeathHandler             = FadeOutAndHide;
            onConditionAppliedHandler  = HandleConditionApplied;
            onConditionRemovedHandler  = HandleConditionRemoved;
            onConditionChangedHandler  = HandleConditionChanged;

            unit.OnDamageTaken     += onDamageTakenHandler;
            unit.OnHealReceived    += onHealReceivedHandler;
            unit.OnDefenseChanged  += onDefenseChangedHandler;
            unit.OnStatsChanged    += onStatsChangedHandler;
            unit.OnDeath           += onDeathHandler;

            unit.conditions.OnConditionApplied += onConditionAppliedHandler;
            unit.conditions.OnConditionRemoved += onConditionRemovedHandler;
            unit.conditions.OnConditionChanged += onConditionChangedHandler;

            // Catch-up refresh when the attached UnitDisplay lifts its
            // suppression. HP/defense/intent ride OnUISuppressionLifted (after
            // the lunge); condition icons ride OnConditionUISuppressionLifted
            // (later, after damage feedback) so the icon paints with the
            // condition floater rather than with the HP drop.
            var display = UnitDisplay.GetDisplay(unit);
            if (display != null)
            {
                display.OnUISuppressionLifted          += HandleSuppressionLifted;
                display.OnConditionUISuppressionLifted += HandleConditionUISuppressionLifted;
            }

            // Intent bus
            CombatEvents.OnEnemyMoveSet += HandleMoveSet;

            // Chance Box bootstrap
            if (chanceBox != null)
                chanceBox.Initialize(unit);
        }

        public void Hide() => gameObject.SetActive(false);
        public void Show() => gameObject.SetActive(true);

        /// <summary>
        /// Fade the HUD's CanvasGroup to zero alpha over deathFadeOutDuration,
        /// then deactivate. Adds a CanvasGroup at runtime if the prefab doesn't
        /// have one. Snaps to hidden on duration <= 0.
        /// </summary>
        private void FadeOutAndHide()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            StartCoroutine(FadeOutAndHideCo());
        }

        private System.Collections.IEnumerator FadeOutAndHideCo()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            // Block raycasts immediately so a fading-out HUD can't be hovered.
            cg.blocksRaycasts = false;
            cg.interactable = false;

            float t = 0f;
            float dur = Mathf.Max(0.0001f, deathFadeOutDuration);
            float startAlpha = cg.alpha;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Short grow/shrink pulse on the intent strip — called by CombatManager
        /// right before the enemy executes, to telegraph the incoming action.
        /// </summary>
        public void PlayIntentPulse()
        {
            if (aboveRegion == null) return;
            StopAllCoroutines();
            StartCoroutine(PulseCoroutine(aboveRegion));
        }

        private System.Collections.IEnumerator PulseCoroutine(RectTransform rt)
        {
            var original = rt.localScale;
            var peak = original * pulseScale;

            float half = pulseDuration * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.Lerp(original, peak, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.Lerp(peak, original, t / half);
                yield return null;
            }
            rt.localScale = original;
        }

        // ─── Refreshers ─────────────────────────────────────────────────────

        private void RefreshHP()
        {
            if (linkedUnit == null) return;
            if (hpBar != null)
            {
                hpBar.maxValue = Mathf.Max(1, linkedUnit.maxHP);
                float target = linkedUnit.currentHP;
                if (hpTweenDuration <= 0f || !gameObject.activeInHierarchy)
                {
                    if (hpTweenCo != null) { StopCoroutine(hpTweenCo); hpTweenCo = null; }
                    hpBar.value = target;
                }
                else
                {
                    if (hpTweenCo != null) StopCoroutine(hpTweenCo);
                    hpTweenCo = StartCoroutine(TweenHpBar(hpBar.value, target, hpTweenDuration));
                }
            }
            if (hpText != null)
                hpText.text = $"{linkedUnit.currentHP}";
        }

        private System.Collections.IEnumerator TweenHpBar(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (hpBar != null) hpBar.value = Mathf.Lerp(from, to, k);
                yield return null;
            }
            if (hpBar != null) hpBar.value = to;
            hpTweenCo = null;
        }

        private void RefreshDefense(int _ = 0)
        {
            // Show the unit's static defense rating (the to-hit target). This
            // is always > 0 for a normal unit, so the badge stays visible.
            // Shields (transient damage absorption) are now rendered in the
            // conditions strip as a condition icon — separate concern.
            if (linkedUnit == null) return;
            int def = linkedUnit.EffectiveDEF;
            if (defBadge != null) defBadge.SetActive(def > 0);
            if (defText != null) defText.text = def > 0 ? def.ToString() : "";
        }

        // ─── Intent handling ────────────────────────────────────────────────

        private void HandleMoveSet(Unit enemy, EnemyMove move)
        {
            if (enemy != linkedUnit) return;
            currentMove = move;
            var intents = move != null ? move.intents : System.Array.Empty<EnemyIntent>();
            RebuildIntent(intents);
            if (chanceBox != null) chanceBox.Refresh(intents);
        }

        private void RebuildIntent(IReadOnlyList<EnemyIntent> intents)
        {
            if (intentContainer == null) return;

            // Clear existing icons
            for (int i = intentContainer.childCount - 1; i >= 0; i--)
                Destroy(intentContainer.GetChild(i).gameObject);
            intentIcons.Clear();

            if (intents == null || intentIconPrefab == null) return;

            for (int i = 0; i < intents.Count; i++)
            {
                var go = Instantiate(intentIconPrefab, intentContainer);
                var ui = go.GetComponent<IntentIconUI>();
                if (ui != null)
                {
                    // Pass the linked unit so the icon can preview post-modifier
                    // damage (Weak, Strength) when rendering the label.
                    ui.Bind(intents[i], linkedUnit);
                    intentIcons.Add(ui);
                }
            }
        }

        /// <summary>Re-render every intent icon's label without rebuilding
        /// the strip. Cheap — call on condition apply/change/remove so the
        /// previewed damage tracks Weak / Strength stacks landing mid-turn.</summary>
        private void RefreshIntentLabels()
        {
            for (int i = 0; i < intentIcons.Count; i++)
                if (intentIcons[i] != null) intentIcons[i].Refresh();
        }

        // ─── Condition strip ────────────────────────────────────────────────

        private void HandleConditionApplied(ConditionID id, int stacks)
        {
            if (IsConditionUISuppressed) return; // catch-up via HandleConditionUISuppressionLifted
            if (!conditionIcons.TryGetValue(id, out var icon))
            {
                icon = SpawnConditionIcon(id);
                if (icon != null) conditionIcons[id] = icon;
            }
            if (icon != null) icon.SetStacks(stacks);
            // Conditions like Weak / Strength change outgoing damage — refresh
            // the intent labels so the displayed damage reflects the new value.
            RefreshIntentLabels();
        }

        private void HandleConditionChanged(ConditionID id, int stacks)
        {
            if (IsConditionUISuppressed) return;
            if (conditionIcons.TryGetValue(id, out var icon))
                icon.SetStacks(stacks);
            RefreshIntentLabels();
        }

        private void HandleConditionRemoved(ConditionID id)
        {
            if (IsConditionUISuppressed) return;
            if (conditionIcons.TryGetValue(id, out var icon) && icon != null)
            {
                Destroy(icon.gameObject);
                conditionIcons.Remove(id);
            }
            RefreshIntentLabels();
        }

        // ── Suppression gating ────────────────────────────────────────────
        private bool IsSuppressed
        {
            get
            {
                if (linkedUnit == null) return false;
                var d = UnitDisplay.GetDisplay(linkedUnit);
                return d != null && d.SuppressUIUpdates;
            }
        }

        private bool IsConditionUISuppressed
        {
            get
            {
                if (linkedUnit == null) return false;
                var d = UnitDisplay.GetDisplay(linkedUnit);
                return d != null && d.SuppressConditionUI;
            }
        }

        private void HandleSuppressionLifted()
        {
            if (linkedUnit == null) return;
            // HP / defense / intent catch-up. Condition icons are handled
            // separately via HandleConditionUISuppressionLifted so the icon
            // paints with the condition floater, not the HP drop.
            RefreshHP();
            RefreshDefense();
            RefreshIntentLabels();
        }

        private void HandleConditionUISuppressionLifted()
        {
            if (linkedUnit == null) return;
            RebuildConditions();
            // Damage-modifying conditions (Weak/Strength) just resolved —
            // intent labels need a re-read.
            RefreshIntentLabels();
        }

        /// <summary>Full rebuild — used on Initialize (enemy may spawn pre-afflicted by a debuff).</summary>
        private void RebuildConditions()
        {
            if (conditionsContainer == null) return;

            for (int i = conditionsContainer.childCount - 1; i >= 0; i--)
                Destroy(conditionsContainer.GetChild(i).gameObject);
            conditionIcons.Clear();

            if (linkedUnit == null) return;
            var all = linkedUnit.conditions.GetAllConditions();
            for (int i = 0; i < all.Count; i++)
            {
                var inst = all[i];
                var icon = SpawnConditionIcon(inst.data.conditionID);
                if (icon == null) continue;
                icon.SetStacks(inst.stacks);
                conditionIcons[inst.data.conditionID] = icon;
            }
        }

        private ConditionIconUI SpawnConditionIcon(ConditionID id)
        {
            if (conditionIconPrefab == null || conditionsContainer == null) return null;

            var data = ConditionLibrary.Instance != null
                ? ConditionLibrary.Instance.Get(id)
                : null;
            if (data == null) return null;

            var go = Instantiate(conditionIconPrefab, conditionsContainer);
            var ui = go.GetComponent<ConditionIconUI>();
            if (ui != null) ui.Bind(data);
            return ui;
        }

        private void OnDestroy()
        {
            CombatEvents.OnEnemyMoveSet -= HandleMoveSet;

            if (linkedUnit == null) return;
            if (onDamageTakenHandler != null)     linkedUnit.OnDamageTaken    -= onDamageTakenHandler;
            if (onHealReceivedHandler != null)    linkedUnit.OnHealReceived   -= onHealReceivedHandler;
            if (onDefenseChangedHandler != null)  linkedUnit.OnDefenseChanged -= onDefenseChangedHandler;
            if (onStatsChangedHandler != null)    linkedUnit.OnStatsChanged   -= onStatsChangedHandler;
            if (onDeathHandler != null)           linkedUnit.OnDeath          -= onDeathHandler;
            if (linkedUnit.conditions != null)
            {
                if (onConditionAppliedHandler != null) linkedUnit.conditions.OnConditionApplied -= onConditionAppliedHandler;
                if (onConditionRemovedHandler != null) linkedUnit.conditions.OnConditionRemoved -= onConditionRemovedHandler;
                if (onConditionChangedHandler != null) linkedUnit.conditions.OnConditionChanged -= onConditionChangedHandler;
            }
        }

        // ─── Combat-start intro fade ────────────────────────────────────────

        private CanvasGroup rootCanvasGroup;
        private Coroutine introFadeCo;

        private CanvasGroup GetOrAddRootCanvasGroup()
        {
            if (rootCanvasGroup != null) return rootCanvasGroup;
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            return rootCanvasGroup;
        }

        public void PrepareForIntro()
        {
            var cg = GetOrAddRootCanvasGroup();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        public Coroutine FadeIn(float duration, float startDelay = 0f)
        {
            if (introFadeCo != null) StopCoroutine(introFadeCo);
            introFadeCo = StartCoroutine(IntroFadeInCo(duration, startDelay));
            return introFadeCo;
        }

        private System.Collections.IEnumerator IntroFadeInCo(float duration, float startDelay)
        {
            var cg = GetOrAddRootCanvasGroup();
            if (startDelay > 0f)
            {
                float d = 0f;
                while (d < startDelay) { d += Time.deltaTime; yield return null; }
            }
            float dur = Mathf.Max(0.0001f, duration);
            float from = cg.alpha;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
            introFadeCo = null;
        }

        // ─── Tier badge (Elite / Boss) ──────────────────────────────────────

        // Tier-badge palette. Mirrors the editor summary chips.
        private static readonly Color EliteTint = new(0.72f, 0.74f, 0.78f); // silver
        private static readonly Color BossTint  = new(0.78f, 0.25f, 0.30f); // crimson

        private void InitializeTierBadge(Unit unit)
        {
            if (tierBadge == null) return;
            var data = unit != null ? unit.enemyData : null;
            if (data == null || data.enemyType == EnemyType.Normal)
            {
                tierBadge.SetActive(false);
                return;
            }

            tierBadge.SetActive(true);
            if (tierLabel != null)
                tierLabel.text = data.enemyType == EnemyType.Boss ? "BOSS" : "ELITE";
            if (tierBadgeBg != null)
                tierBadgeBg.color = data.enemyType == EnemyType.Boss ? BossTint : EliteTint;
        }

        // ─── Hover name overlay ─────────────────────────────────────────────

        private void InitializeHoverOverlay(Unit unit)
        {
            if (nameLabel != null && unit != null)
                nameLabel.text = unit.unitName;
            if (statsGroup != null) statsGroup.alpha = 1f;
            if (nameGroup  != null) nameGroup.alpha  = 0f;
            isHovered = false;
        }

        public void OnUnitHoverEnter()
        {
            if (isHovered) return;
            isHovered = true;
            StartHoverFade(targetBars: 0f, targetName: 1f);
        }

        public void OnUnitHoverExit()
        {
            if (!isHovered) return;
            isHovered = false;
            StartHoverFade(targetBars: 1f, targetName: 0f);
        }

        private void StartHoverFade(float targetBars, float targetName)
        {
            if (statsGroup == null && nameGroup == null) return;
            if (hoverFadeCo != null) StopCoroutine(hoverFadeCo);
            if (!gameObject.activeInHierarchy)
            {
                if (statsGroup != null) statsGroup.alpha = targetBars;
                if (nameGroup  != null) nameGroup.alpha  = targetName;
                return;
            }
            hoverFadeCo = StartCoroutine(HoverFadeCo(targetBars, targetName));
        }

        private System.Collections.IEnumerator HoverFadeCo(float targetBars, float targetName)
        {
            float fromBars = statsGroup != null ? statsGroup.alpha : 0f;
            float fromName = nameGroup  != null ? nameGroup.alpha  : 0f;
            float dur = Mathf.Max(0.0001f, hoverFadeDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (statsGroup != null) statsGroup.alpha = Mathf.Lerp(fromBars, targetBars, k);
                if (nameGroup  != null) nameGroup.alpha  = Mathf.Lerp(fromName, targetName, k);
                yield return null;
            }
            if (statsGroup != null) statsGroup.alpha = targetBars;
            if (nameGroup  != null) nameGroup.alpha  = targetName;
            hoverFadeCo = null;
        }
    }
}
