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

        private List<Unit> picksCollected = new();
        private int picksRequired = 1;
        public int PicksRequired => picksRequired;
        public int PicksSoFar => picksCollected.Count;
        public event Action<int, int> OnMultiPickProgress;

        public Unit Caster { get; private set; }
        public int CurrentRangeMin { get; private set; } = 0;
        public int CurrentRangeMax { get; private set; } = int.MaxValue;

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

        private bool InRange(Unit caster, Unit target)
        {
            if (CurrentRangeMax >= int.MaxValue && CurrentRangeMin <= 0) return true;
            int d = RankHelper.Distance(caster, target);
            return d >= CurrentRangeMin && d <= CurrentRangeMax;
        }

        public void SelectTarget(Unit target)
        {
            if (!IsTargeting) return;
            if (!validTargets.Contains(target)) return;

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

            if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }
        }
    }
}
