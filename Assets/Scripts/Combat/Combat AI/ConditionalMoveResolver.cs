using UnityEngine;

namespace DarkSpire
{
    public static class ConditionalMoveResolver
    {
        public static EnemyMove PickOverride(Unit enemy)
        {
            if (enemy == null || enemy.enemyData == null) return null;
            var list = enemy.enemyData.conditionalMoves;
            if (list == null || list.Length == 0) return null;

            for (int i = 0; i < list.Length; i++)
            {
                var entry = list[i];
                if (entry == null || entry.move == null) continue;

                // Once-per-combat triggers latch after their first fire so a
                // sustained condition (e.g. HP < 50% the rest of the fight)
                // doesn't replace every subsequent move.
                if (entry.oncePerCombat && enemy.firedConditionalIndices.Contains(i))
                    continue;

                if (!TriggerFires(enemy, entry)) continue;

                if (entry.oncePerCombat) enemy.firedConditionalIndices.Add(i);
                return entry.move;
            }

            return null;
        }

        private static bool TriggerFires(Unit enemy, EnemyConditionalMove entry)
        {
            switch (entry.trigger)
            {
                case EnemyConditionalTrigger.HPBelowPercent:
                {
                    if (enemy.maxHP <= 0) return false;
                    float pct = (float)enemy.currentHP / enemy.maxHP;
                    return pct < Mathf.Clamp01(entry.percent);
                }

                case EnemyConditionalTrigger.HPAbovePercent:
                {
                    if (enemy.maxHP <= 0) return false;
                    float pct = (float)enemy.currentHP / enemy.maxHP;
                    return pct > Mathf.Clamp01(entry.percent);
                }

                case EnemyConditionalTrigger.OnTurnNumber:
                {
                    var mgr = CombatManager.Instance;
                    if (mgr == null) return false;
                    return mgr.TurnNumber == entry.turnNumber;
                }

                case EnemyConditionalTrigger.OnConditionApplied:
                    return enemy.conditionsAppliedSinceLastMove != null
                        && enemy.conditionsAppliedSinceLastMove.Contains(entry.conditionId);

                case EnemyConditionalTrigger.OnConditionRemoved:
                    return enemy.conditionsRemovedSinceLastMove != null
                        && enemy.conditionsRemovedSinceLastMove.Contains(entry.conditionId);

                case EnemyConditionalTrigger.OnConditionAtStacks:
                    return enemy.conditions != null
                        && enemy.conditions.GetStacks(entry.conditionId) >= Mathf.Max(1, entry.stacks);

                case EnemyConditionalTrigger.AnyTargetLacksCondition:
                {
                    var mgr = CombatManager.Instance;
                    if (mgr == null) return false;
                    var party = mgr.GetAlivePlayerUnits();
                    if (party == null) return false;
                    for (int i = 0; i < party.Count; i++)
                    {
                        var p = party[i];
                        if (p?.conditions == null) continue;
                        if (p.conditions.GetStacks(entry.conditionId) <= 0) return true;
                    }
                    return false;
                }

                default:
                    return false;
            }
        }
    }
}
