using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class FloatingNumberManager : MonoBehaviour
    {
        [Header("Prefabs + Canvas")]
        [Tooltip("Damage / crit-damage / heal arc number. Big, scales up to " +
                 "apex then shrinks + fades on the fall.")]
        [SerializeField] private GameObject floatingNumberPrefab;

        [Tooltip("Text-only flare for condition gained, MISS, DODGE, RESIST, " +
                 "CRIT! label, and shields. Drifts up or down with fade.")]
        [SerializeField] private GameObject conditionFlarePrefab;

        [Tooltip("Icon + name + 'Wears Off' subtitle. Always rises. Falls " +
                 "back to conditionFlarePrefab if unset.")]
        [SerializeField] private GameObject wearsOffFlarePrefab;

        [Tooltip("Screen Space - Overlay canvas that the overlays parent under. " +
                 "Usually the Combat canvas on the same root as this manager.")]
        [SerializeField] private Canvas combatCanvas;

        // Colors come from ColorLibrary at lookup time. Local readonly fallbacks
        // are used when the library is missing or the key isn't authored — keeps
        // popups visible even before the library is set up.
        private static readonly Color FallbackDamage  = new Color(0.95f, 0.30f, 0.28f);
        private static readonly Color FallbackCrit    = new Color(1.00f, 0.84f, 0.20f);
        private static readonly Color FallbackHeal    = new Color(0.40f, 0.85f, 0.45f);
        private static readonly Color FallbackShields = new Color(0.45f, 0.75f, 0.95f);
        private static readonly Color FallbackMiss    = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color FallbackBuff    = new Color(0.40f, 0.85f, 0.55f);
        private static readonly Color FallbackDebuff  = new Color(0.80f, 0.30f, 0.80f);

        private static Color DamageColor  => ColorLibrary.Get("DamageNumbers", "Damage",  FallbackDamage);
        private static Color CritColor    => ColorLibrary.Get("DamageNumbers", "Crit",    FallbackCrit);
        private static Color HealColor    => ColorLibrary.Get("DamageNumbers", "Heal",    FallbackHeal);
        private static Color ShieldsColor => ColorLibrary.Get("DamageNumbers", "Shields", FallbackShields);
        private static Color MissColor    => ColorLibrary.Get("DamageNumbers", "Miss",    FallbackMiss);
        private static Color BuffColor    => ColorLibrary.Get("Condition",     "BuffPill",   FallbackBuff);
        private static Color DebuffColor  => ColorLibrary.Get("Condition",     "DebuffPill", FallbackDebuff);

        [Header("Arc Timing")]
        [Tooltip("Seconds from foot to apex.")]
        [SerializeField] private float riseTime = 0.61f;

        [Tooltip("Seconds from apex back down to foot Y.")]
        [SerializeField] private float fallTime = 0.44f;

        [Tooltip("Seconds falling off-screen past the foot.")]
        [SerializeField] private float falloffTime = 0.53f;

        [Header("Arc Scale")]
        [Tooltip("Scale at spawn (foot) and end of falloff. Number scales up " +
                 "to maxScale at apex and shrinks back through this on the fall.")]
        [SerializeField] private float minScale = 0.65f;
        [SerializeField] private float maxScale = 2.1f;

        [Header("Arc Wobble")]
        [Tooltip("Max Z-axis rotation in degrees. The number rocks left-right " +
                 "like a knob, with amplitude scaled by current height above " +
                 "foot — strongest near apex, zero at foot level.")]
        [SerializeField] private float wobbleAmplitude = 6f;

        [Tooltip("Wobble cycles per second.")]
        [SerializeField] private float wobbleFrequency = 2.4f;

        [Header("Flare Timing")]
        [Tooltip("Seconds for alpha 0 → 1 at spawn.")]
        [SerializeField] private float flareFadeIn = 0.10f;

        [Tooltip("Seconds the flare drifts at full opacity (ease-out).")]
        [SerializeField] private float flareDrift = 0.55f;

        [Tooltip("Seconds for alpha 1 → 0 at end.")]
        [SerializeField] private float flareFadeOut = 0.30f;

        [Tooltip("Pixels the flare drifts vertically across its lifetime.")]
        [SerializeField] private float flareDriftDistance = 60f;

        [Tooltip("Pixel offset below the pivot where positive-gained flares " +
                 "spawn before rising through the pivot.")]
        [SerializeField] private float positiveSpawnBelowPivot = 24f;

        [Header("Offset Tracking")]
        [Tooltip("Minimum horizontal offset from unit center (world units). " +
                 "Prevents numbers stacking on dead-center.")]
        [SerializeField] private float minOffset = 0.15f;

        [Tooltip("Incremental nudge for consecutive spawns on the same unit (world units). " +
                 "After hitting the hitbox edge, direction flips and resets to minOffset.")]
        [SerializeField] private float offsetNudge = 0.10f;

        [Tooltip("Time gap (seconds) before the per-unit offset memory resets.")]
        [SerializeField] private float offsetResetTime = 1.5f;

        public static FloatingNumberManager Instance { get; private set; }

        // Per-unit nudge tracking
        private readonly Dictionary<Unit, float> lastOffsets = new();
        private readonly Dictionary<Unit, int>   lastOffsetDir = new();
        private readonly Dictionary<Unit, float> lastSpawnTime = new();

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            CombatEvents.OnActionResolved   += HandleActionResolved;
            CombatEvents.OnConditionApplied += HandleConditionApplied;
            CombatEvents.OnConditionRemoved += HandleConditionRemoved;
        }

        private void OnDisable()
        {
            CombatEvents.OnActionResolved   -= HandleActionResolved;
            CombatEvents.OnConditionApplied -= HandleConditionApplied;
            CombatEvents.OnConditionRemoved -= HandleConditionRemoved;
            if (Instance == this) Instance = null;
        }

        // ─── Condition feedback ──────────────────────────────────────────────

        private void HandleConditionApplied(Unit target, ConditionID id, int stacks)
        {
            // Action-driven applies (during a skill cast) suppress this auto
            // floater; CombatManager replays it via SpawnConditionApplied at
            // the right moment in its per-effect playback. Autonomous applies
            // (DoT ticks, start-of-turn self-stacks) leave the flag false and
            // floater-spawn through this path immediately.
            var d = UnitDisplay.GetDisplay(target);
            if (d != null && d.SuppressConditionUI) return;
            SpawnConditionAppliedInternal(target, id, stacks);
        }

        public void SpawnConditionApplied(Unit target, ConditionID id, int stacks)
            => SpawnConditionAppliedInternal(target, id, stacks);

        private void SpawnConditionAppliedInternal(Unit target, ConditionID id, int stacks)
        {
            var data = ConditionLibrary.Instance != null
                ? ConditionLibrary.Instance.Get(id)
                : null;
            bool isDebuff = data != null && data.isDebuff;
            string displayName = data != null && !string.IsNullOrEmpty(data.displayName)
                ? data.displayName
                : id.ToString();
            string txt = stacks > 1 ? $"+{stacks} {displayName}" : displayName;

            Color c = isDebuff ? DebuffColor : BuffColor;
            FlareDirection dir = isDebuff ? FlareDirection.Down : FlareDirection.Up;
            // Negative gained spawns AT pivot (drifts down across the unit);
            // positive gained spawns just below pivot and rises through it.
            float pivotYOffset = isDebuff ? 0f : -positiveSpawnBelowPivot;

            SpawnFlareAtPivot(target, txt, c, dir, null, null, pivotYOffset, conditionFlarePrefab);
        }

        private void HandleConditionRemoved(Unit target, ConditionID id)
        {
            var data = ConditionLibrary.Instance != null
                ? ConditionLibrary.Instance.Get(id)
                : null;
            string displayName = data != null && !string.IsNullOrEmpty(data.displayName)
                ? data.displayName
                : id.ToString();
            Sprite icon = data != null ? data.icon : null;

            var prefab = wearsOffFlarePrefab != null ? wearsOffFlarePrefab : conditionFlarePrefab;
            // White text — outline is configured on the prefab's TMP material.
            SpawnFlareAtPivot(target, displayName, Color.white, FlareDirection.Up,
                              icon, "Wears Off", 0f, prefab);
        }

        // ─── Result → spawn translation ──────────────────────────────────────

        private void HandleActionResolved(CombatActionResult result)
        {
            if (result == null) return;

            // Damage / miss / dodge / crit
            if (result.didRoll)
            {
                if (result.didHit && result.damageDealt > 0)
                {
                    SpawnDamage(result.target, result.damageDealt, result.wasCrit);
                    if (result.wasCrit)
                        SpawnFlareAtPivot(result.target, "CRIT!", CritColor,
                                          FlareDirection.Up, null, null, 0f,
                                          conditionFlarePrefab);
                }
                else if (result.wasDodged)
                {
                    SpawnFlareAtPivot(result.target, "DODGE", MissColor,
                                      FlareDirection.Up, null, null, 0f,
                                      conditionFlarePrefab);
                }
                else if (!result.didHit)
                {
                    SpawnFlareAtPivot(result.target, "MISS", MissColor,
                                      FlareDirection.Up, null, null, 0f,
                                      conditionFlarePrefab);
                }
            }
            else if (result.damageDealt > 0)
            {
                SpawnDamage(result.target, result.damageDealt, false);
            }

            if (result.healingDone > 0)
                SpawnHeal(result.target, result.healingDone);

            if (result.defenseGained > 0)
                SpawnFlareAtPivot(result.source, $"+{result.defenseGained} SHLD",
                                  ShieldsColor, FlareDirection.Up, null, null, 0f,
                                  conditionFlarePrefab);

            if (result.didSave && result.saveSucceeded)
                SpawnFlareAtPivot(result.target, "RESIST", MissColor,
                                  FlareDirection.Up, null, null, 0f,
                                  conditionFlarePrefab);

            // Conditions applied are handled by HandleConditionApplied via the
            // dedicated CombatEvents.OnConditionApplied bus.
        }

        // ─── Public arc API ──────────────────────────────────────────────────

        public void SpawnDamage(Unit unit, int amount, bool isCrit)
            => SpawnArc(unit, amount.ToString(), isCrit ? CritColor : DamageColor);

        public void SpawnHeal(Unit unit, int amount)
            => SpawnArc(unit, $"+{amount}", HealColor);

        // ─── Arc spawn ───────────────────────────────────────────────────────

        private void SpawnArc(Unit unit, string text, Color color)
        {
            if (floatingNumberPrefab == null || unit == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var display = UnitDisplay.GetDisplay(unit);
            if (display == null) return;

            float halfWidth = display.HitboxSize.x * 0.5f;
            float offset = ComputeOffset(unit, halfWidth);

            Vector3 footWorld      = display.transform.position;
            Vector3 spawnWorld     = footWorld + new Vector3(offset, 0f, 0f);
            Vector3 indicatorWorld = display.FloaterAnchorWorld;

            Vector3 startScreen = cam.WorldToScreenPoint(spawnWorld);
            Vector3 apexScreen  = cam.WorldToScreenPoint(indicatorWorld);
            float footScreenY   = cam.WorldToScreenPoint(footWorld).y;

            var parent = combatCanvas != null ? combatCanvas.transform : transform;
            var go = Instantiate(floatingNumberPrefab, parent);

            var floater = go.GetComponent<FloatingNumber>();
            if (floater != null)
            {
                floater.Setup(text, color,
                    startScreen, apexScreen, footScreenY,
                    riseTime, fallTime, falloffTime,
                    minScale, maxScale,
                    wobbleAmplitude, wobbleFrequency);
            }
        }

        // ─── Flare spawn ─────────────────────────────────────────────────────

        private void SpawnFlareAtPivot(Unit unit, string text, Color color,
            FlareDirection direction, Sprite icon, string subtitle,
            float pivotYOffsetPixels, GameObject prefab)
        {
            if (prefab == null || unit == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var display = UnitDisplay.GetDisplay(unit);
            if (display == null) return;

            Vector3 pivotScreen = cam.WorldToScreenPoint(display.FloaterAnchorWorld);
            pivotScreen.y += pivotYOffsetPixels;

            var parent = combatCanvas != null ? combatCanvas.transform : transform;
            var go = Instantiate(prefab, parent);

            var flare = go.GetComponent<FloatingFlare>();
            if (flare != null)
            {
                flare.Setup(text, color, pivotScreen, direction,
                    icon, subtitle,
                    flareDriftDistance, flareFadeIn, flareDrift, flareFadeOut);
            }
        }

        private float ComputeOffset(Unit unit, float halfWidth)
        {
            float now = Time.time;

            if (lastSpawnTime.TryGetValue(unit, out float prevTime)
                && now - prevTime > offsetResetTime)
            {
                lastOffsets.Remove(unit);
                lastOffsetDir.Remove(unit);
            }
            lastSpawnTime[unit] = now;

            if (!lastOffsets.TryGetValue(unit, out float prevOffset))
            {
                int dir = Random.value > 0.5f ? 1 : -1;
                float offset = minOffset * dir;
                lastOffsets[unit] = Mathf.Abs(offset);
                lastOffsetDir[unit] = dir;
                return Mathf.Clamp(offset, -halfWidth, halfWidth);
            }
            else
            {
                int dir = lastOffsetDir.GetValueOrDefault(unit, 1);
                float newMag = prevOffset + offsetNudge;

                if (newMag > halfWidth)
                {
                    dir = -dir;
                    newMag = minOffset;
                    lastOffsetDir[unit] = dir;
                }

                lastOffsets[unit] = newMag;
                return newMag * dir;
            }
        }
    }
}
