using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class EnemyAI
    {
        public static EnemyMove DecideMove(Unit enemy)
        {
            if (enemy == null || enemy.enemyData == null) return null;

            if (enemy.enemyData.hasTurn1MoveOverride
                && !enemy.hasActedThisTurn
                && enemy.enemyData.turn1MoveOverride != null)
            {
                return enemy.enemyData.turn1MoveOverride;
            }

            var conditional = ConditionalMoveResolver.PickOverride(enemy);
            if (conditional != null) return conditional;

            return enemy.GetNextEnemyMove();
        }

        public static Unit SelectTargetForIntent(
            Unit enemy, EnemyIntent intent, List<Unit> playerUnits)
        {
            if (playerUnits == null || enemy == null) return null;

            int rangeMin = intent != null ? Mathf.Max(1, intent.rangeMin) : 1;
            int rangeMax = intent != null ? Mathf.Max(rangeMin, intent.rangeMax) : int.MaxValue;

            var inRange = new List<Unit>();
            for (int i = 0; i < playerUnits.Count; i++)
            {
                var p = playerUnits[i];
                if (p == null || !p.IsAlive) continue;
                if (RankHelper.InRange(enemy, p, rangeMin, rangeMax))
                    inRange.Add(p);
            }
            if (inRange.Count == 0) return null;

            var pref = intent != null ? intent.targetPreference : EnemyTargetPreference.Random;
            var condId = intent != null ? intent.preferredCondition : default;
            return PickByPreference(inRange, pref, condId);
        }

        public static Unit SelectTarget(List<Unit> playerUnits)
        {
            if (playerUnits == null) return null;
            var alive = new List<Unit>();
            for (int i = 0; i < playerUnits.Count; i++)
                if (playerUnits[i] != null && playerUnits[i].IsAlive) alive.Add(playerUnits[i]);
            if (alive.Count == 0) return null;
            return alive[Random.Range(0, alive.Count)];
        }

        public static List<CombatActionResult> ExecuteMove(
            Unit enemy, EnemyMove move,
            List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            return SkillResolver.ResolveEnemyMove(enemy, move, playerUnits, enemyUnits);
        }

        private static Unit PickByPreference(
            List<Unit> pool, EnemyTargetPreference pref, ConditionID condId)
        {
            if (pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            if (pref == EnemyTargetPreference.Random)
                return pool[Random.Range(0, pool.Count)];

            if (pref == EnemyTargetPreference.HasCondition
             || pref == EnemyTargetPreference.LacksCondition)
            {
                bool wantHas = pref == EnemyTargetPreference.HasCondition;
                var matched = new List<Unit>();
                for (int i = 0; i < pool.Count; i++)
                {
                    bool has = pool[i].conditions != null
                            && pool[i].conditions.GetStacks(condId) > 0;
                    if (has == wantHas) matched.Add(pool[i]);
                }
                var bucket = matched.Count > 0 ? matched : pool;
                return bucket[Random.Range(0, bucket.Count)];
            }

            int bestScore = int.MaxValue;
            for (int i = 0; i < pool.Count; i++)
                bestScore = Mathf.Min(bestScore, ScoreFor(pool[i], pref));

            var ties = new List<Unit>();
            for (int i = 0; i < pool.Count; i++)
                if (ScoreFor(pool[i], pref) == bestScore) ties.Add(pool[i]);
            return ties[Random.Range(0, ties.Count)];
        }

        private static int ScoreFor(Unit u, EnemyTargetPreference pref) => pref switch
        {
            EnemyTargetPreference.LowestHP     =>  u.currentHP,
            EnemyTargetPreference.HighestHP    => -u.currentHP,
            EnemyTargetPreference.LowestMaxHP  =>  u.maxHP,
            EnemyTargetPreference.HighestMaxHP => -u.maxHP,
            EnemyTargetPreference.LowestWIL    =>  u.EffectiveWIL,
            EnemyTargetPreference.HighestWIL   => -u.EffectiveWIL,
            _ => 0,
        };
    }
}
