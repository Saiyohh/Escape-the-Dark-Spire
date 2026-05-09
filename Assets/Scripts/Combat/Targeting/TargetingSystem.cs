// TargetingSystem.cs
// -----------------------------------------------------------------------------
// Singleton that mediates target selection. Actions call BeginTargeting(mode,
// caster, onConfirmed, onCancelled) and either:
//   • Auto-resolve (Self, AllEnemies, AllAllies, RandomEnemy) → invoke immediately
//   • Manual mode (SingleEnemy, SingleAlly) → raise OnTargetingStateChanged,
//     let UnitDisplay dispatch clicks, fire onConfirmed on SelectTarget
//
// Escape + right-click cancel targeting.
// TargetingArrow subscribes to the state/hover events for the bezier-arrow VFX.
//
// Range validation (Pass 2.B): BeginTargeting accepts rangeMin/rangeMax that
// filter valid targets by RankHelper.Distance from the caster. Out-of-range
// units are excluded from validTargets — clicks on them are ignored, and the
// IsValidTarget query returns false (so TargetingArrow won't tint-green/red
// on hover). Pass 0/99 or call without range args to skip the filter.
//
// Pass 2 TODO: add RandomAlly mode.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    public class TargetingSystem : MonoBehaviour
    {
        public static TargetingSystem Instance { get; private set; }

        public bool IsTargeting { get; private set; }
        public TargetMode CurrentMode { get; private set; }

        private Action<List<Unit>> onTargetsConfirmed;
        private Action onTargetingCancelled;

        private List<Unit> validTargets = new();
        private List<Unit> allPlayerUnits;
        private List<Unit> allEnemyUnits;

        // Multi-pick state: collects clicks into picksCollected until we hit
        // picksRequired, then fires onTargetsConfirmed with the full list.
        // pickCount == 1 keeps the legacy one-click-confirms flow.
        private List<Unit> picksCollected = new();
        private int picksRequired = 1;
        public int PicksRequired => picksRequired;
        public int PicksSoFar => picksCollected.Count;
        /// <summary>Fired each time a pick is added (picks so far, required). UI can read this for "pick 2 of 3" hints.</summary>
        public event Action<int, int> OnMultiPickProgress;

        // Active range window during targeting (Pass 2.B). Set by BeginTargeting,
        // read back by TargetingArrow / UnitDisplay to tint out-of-range hovers
        // differently from the valid pool.
        public Unit Caster { get; private set; }
        public int CurrentRangeMin { get; private set; } = 0;
        public int CurrentRangeMax { get; private set; } = int.MaxValue;

        /// <summary>
        /// Optional world-space origin for the targeting arrow.
        /// Set by the action that initiates targeting (card, button, potion sprite).
        /// If null, TargetingArrow falls back to the active unit's display position.
        /// </summary>
        public Transform ArrowOriginOverride { get; private set; }

        public event Action<Unit> OnTargetHovered;
        public event Action OnTargetUnhovered;
        public event Action<bool> OnTargetingStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetUnitLists(List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            allPlayerUnits = playerUnits;
            allEnemyUnits = enemyUnits;
        }

        /// <summary>Begin targeting mode. Calls callback with resolved targets when confirmed.</summary>
        /// <param name="arrowOrigin">Optional Transform the targeting arrow should originate from.
        /// Pass null to fall back to the active unit's display position.</param>
        /// <param name="rangeMin">Minimum distance this target must be from caster. 0 = no min.</param>
        /// <param name="rangeMax">Maximum distance. Leave default (int.MaxValue) for unrestricted.</param>
        /// <param name="pickCount">How many individual targets the player must pick. 1 = legacy
        /// one-click-confirms. >1 = each click adds to a collected list; confirm fires when the
        /// list reaches pickCount. Applies only to SingleEnemy / SingleAlly modes.</param>
        public void BeginTargeting(TargetMode mode, Unit caster,
            Action<List<Unit>> onConfirmed, Action onCancelled = null,
            Transform arrowOrigin = null,
            int rangeMin = 0, int rangeMax = int.MaxValue,
            int pickCount = 1)
        {
            CurrentMode = mode;
            ArrowOriginOverride = arrowOrigin;
            onTargetsConfirmed = onConfirmed;
            onTargetingCancelled = onCancelled;
            Caster = caster;
            CurrentRangeMin = rangeMin;
            CurrentRangeMax = rangeMax;
            picksCollected.Clear();
            picksRequired = Mathf.Max(1, pickCount);

            switch (mode)
            {
                case TargetMode.Self:
                    // Auto-resolve immediately
                    onConfirmed?.Invoke(new List<Unit> { caster });
                    return;

                case TargetMode.AllEnemies:
                    var allAlive = new List<Unit>();
                    var enemies = caster.isPlayerControlled ? allEnemyUnits : allPlayerUnits;
                    foreach (var e in enemies)
                        if (e.IsAlive && InRange(caster, e)) allAlive.Add(e);
                    onConfirmed?.Invoke(allAlive);
                    return;

                case TargetMode.AllAllies:
                    var allAllies = new List<Unit>();
                    var allies = caster.isPlayerControlled ? allPlayerUnits : allEnemyUnits;
                    foreach (var a in allies)
                        if (a.IsAlive && InRange(caster, a)) allAllies.Add(a);
                    onConfirmed?.Invoke(allAllies);
                    return;

                case TargetMode.RandomEnemy:
                    var pool = caster.isPlayerControlled ? allEnemyUnits : allPlayerUnits;
                    var alive = new List<Unit>();
                    foreach (var e in pool)
                        if (e.IsAlive && InRange(caster, e)) alive.Add(e);
                    if (alive.Count > 0)
                        onConfirmed?.Invoke(new List<Unit> { alive[UnityEngine.Random.Range(0, alive.Count)] });
                    return;

                case TargetMode.SingleEnemy:
                    validTargets.Clear();
                    var enemyPool = caster.isPlayerControlled ? allEnemyUnits : allPlayerUnits;
                    foreach (var e in enemyPool)
                        if (e.IsAlive && InRange(caster, e)) validTargets.Add(e);

                    // Always enter targeting mode — even with 1 target the player
                    // should draw the arrow and click to confirm. Feels deliberate.
                    IsTargeting = true;
                    OnTargetingStateChanged?.Invoke(true);
                    break;

                case TargetMode.SingleAlly:
                    validTargets.Clear();
                    var allyPool = caster.isPlayerControlled ? allPlayerUnits : allEnemyUnits;
                    foreach (var a in allyPool)
                        if (a.IsAlive && InRange(caster, a)) validTargets.Add(a);

                    IsTargeting = true;
                    OnTargetingStateChanged?.Invoke(true);
                    break;
            }
        }

        /// <summary>True if `target` is inside the currently-active range window.</summary>
        private bool InRange(Unit caster, Unit target)
        {
            if (CurrentRangeMax >= int.MaxValue && CurrentRangeMin <= 0) return true;
            int d = RankHelper.Distance(caster, target);
            return d >= CurrentRangeMin && d <= CurrentRangeMax;
        }

        /// <summary>Called by UnitDisplay when player clicks a unit during targeting.</summary>
        public void SelectTarget(Unit target)
        {
            if (!IsTargeting) return;
            if (!validTargets.Contains(target)) return;

            // Multi-pick: collect, keep targeting mode alive until we hit the
            // required count. Disallow picking the same unit twice unless the
            // skill specifically allows it (not modeled yet — assume distinct).
            if (picksRequired > 1)
            {
                if (picksCollected.Contains(target)) return;
                picksCollected.Add(target);
                OnMultiPickProgress?.Invoke(picksCollected.Count, picksRequired);

                if (picksCollected.Count < picksRequired) return;
            }

            IsTargeting = false;
            ArrowOriginOverride = null;
            ClearRangeState();
            OnTargetingStateChanged?.Invoke(false);

            var confirmedList = picksRequired > 1
                ? new List<Unit>(picksCollected)
                : new List<Unit> { target };
            picksCollected.Clear();
            picksRequired = 1;

            onTargetsConfirmed?.Invoke(confirmedList);
        }

        public void CancelTargeting()
        {
            if (!IsTargeting) return;
            IsTargeting = false;
            ArrowOriginOverride = null;
            ClearRangeState();
            picksCollected.Clear();
            picksRequired = 1;
            OnTargetingStateChanged?.Invoke(false);
            onTargetingCancelled?.Invoke();
        }

        private void ClearRangeState()
        {
            Caster = null;
            CurrentRangeMin = 0;
            CurrentRangeMax = int.MaxValue;
        }

        public bool IsValidTarget(Unit unit) => IsTargeting && validTargets.Contains(unit);

        public void NotifyTargetHovered(Unit unit) => OnTargetHovered?.Invoke(unit);
        public void NotifyTargetUnhovered() => OnTargetUnhovered?.Invoke();

        private void Update()
        {
            if (!IsTargeting) return;

            // Escape key cancels targeting
            if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }

            // Right mouse button cancels targeting
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }
        }
    }
}
