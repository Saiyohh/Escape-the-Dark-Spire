using System;

namespace DarkSpire
{
    public static class CombatEvents
    {
        public static event Action OnCombatStart;
        public static event Action<int> OnTurnStart;
        public static event Action<int> OnTurnEnd;
        public static event Action<int> OnRoundStart;         // fires before any turn of round N
        public static event Action<int> OnRoundEnd;           // fires after cleanup of round N
        public static event Action OnPlayerPhaseStart;
        public static event Action OnPlayerPhaseEnd;
        public static event Action OnEnemyPhaseStart;
        public static event Action OnEnemyPhaseEnd;
        public static event Action<CombatPhase> OnPhaseChanged;
        public static event Action<Unit> OnUnitTurnStart;
        public static event Action<Unit> OnUnitTurnEnd;
        public static event Action<CombatActionResult> OnActionResolved;
        public static event Action<CombatActionResult> OnPlayerAttack;
        public static event Action<CombatActionResult> OnPlayerSkillUsed;
        public static event Action<Unit, int> OnPlayerHit;
        public static event Action<Unit> OnEnemyDeath;
        public static event Action<bool> OnCombatEnd; // true = victory
        public static event Action<Unit, int, int, bool, bool> OnDiceRolled; // unit, raw, total, hit, crit
        public static event Action<Unit, EnemyMove> OnEnemyMoveSet;
        public static event Action<Unit> OnActionStateChanged; // fired after action/bonus consumed
        public static event Action<Unit> OnSkillTargetingCancelled; // fired when right-click cancels skill targeting
        public static event Action<Unit, OrbInstance> OnOrbPassiveTriggered; // (bearer, orb) — VFX/audio hook for the per-orb passive tick at phase end
        public static event Action<Unit, ConditionID, int> OnConditionApplied; // (target, condition, stacks)
        public static event Action<Unit, ConditionID> OnConditionRemoved;       // (target, condition)

        public static void InvokeCombatStart() => OnCombatStart?.Invoke();
        public static void InvokeTurnStart(int turn) => OnTurnStart?.Invoke(turn);
        public static void InvokeTurnEnd(int turn) => OnTurnEnd?.Invoke(turn);
        public static void InvokeRoundStart(int round) => OnRoundStart?.Invoke(round);
        public static void InvokeRoundEnd(int round) => OnRoundEnd?.Invoke(round);
        public static void InvokePlayerPhaseStart() => OnPlayerPhaseStart?.Invoke();
        public static void InvokePlayerPhaseEnd() => OnPlayerPhaseEnd?.Invoke();
        public static void InvokeEnemyPhaseStart() => OnEnemyPhaseStart?.Invoke();
        public static void InvokeEnemyPhaseEnd() => OnEnemyPhaseEnd?.Invoke();
        public static void InvokePhaseChanged(CombatPhase phase) => OnPhaseChanged?.Invoke(phase);
        public static void InvokeUnitTurnStart(Unit unit) => OnUnitTurnStart?.Invoke(unit);
        public static void InvokeUnitTurnEnd(Unit unit) => OnUnitTurnEnd?.Invoke(unit);
        public static void InvokeActionResolved(CombatActionResult result) => OnActionResolved?.Invoke(result);
        public static void InvokePlayerAttack(CombatActionResult result) => OnPlayerAttack?.Invoke(result);
        public static void InvokePlayerSkillUsed(CombatActionResult result) => OnPlayerSkillUsed?.Invoke(result);
        public static void InvokePlayerHit(Unit unit, int damage) => OnPlayerHit?.Invoke(unit, damage);
        public static void InvokeEnemyDeath(Unit enemy) => OnEnemyDeath?.Invoke(enemy);
        public static void InvokeCombatEnd(bool victory) => OnCombatEnd?.Invoke(victory);
        public static void InvokeDiceRolled(Unit unit, int raw, int total, bool hit, bool crit) =>
            OnDiceRolled?.Invoke(unit, raw, total, hit, crit);
        public static void InvokeEnemyMoveSet(Unit enemy, EnemyMove move) =>
            OnEnemyMoveSet?.Invoke(enemy, move);
        public static void InvokeActionStateChanged(Unit unit) => OnActionStateChanged?.Invoke(unit);
        public static void InvokeSkillTargetingCancelled(Unit unit) => OnSkillTargetingCancelled?.Invoke(unit);
        public static void InvokeOrbPassiveTriggered(Unit bearer, OrbInstance orb) =>
            OnOrbPassiveTriggered?.Invoke(bearer, orb);
        public static void InvokeConditionApplied(Unit target, ConditionID id, int stacks) =>
            OnConditionApplied?.Invoke(target, id, stacks);
        public static void InvokeConditionRemoved(Unit target, ConditionID id) =>
            OnConditionRemoved?.Invoke(target, id);

        public static void ClearAll()
        {
            OnCombatStart = null;
            OnTurnStart = null;
            OnTurnEnd = null;
            OnRoundStart = null;
            OnRoundEnd = null;
            OnPlayerPhaseStart = null;
            OnPlayerPhaseEnd = null;
            OnEnemyPhaseStart = null;
            OnEnemyPhaseEnd = null;
            OnPhaseChanged = null;
            OnUnitTurnStart = null;
            OnUnitTurnEnd = null;
            OnActionResolved = null;
            OnPlayerAttack = null;
            OnPlayerSkillUsed = null;
            OnPlayerHit = null;
            OnEnemyDeath = null;
            OnCombatEnd = null;
            OnDiceRolled = null;
            OnEnemyMoveSet = null;
            OnActionStateChanged = null;
            OnSkillTargetingCancelled = null;
            OnOrbPassiveTriggered = null;
            OnConditionApplied = null;
            OnConditionRemoved = null;
        }
    }
}
