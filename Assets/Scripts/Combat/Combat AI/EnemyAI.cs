// EnemyAI.cs
// -----------------------------------------------------------------------------
// Thin decision layer over EnemyData:
//   DecideMove:           pick a move (turn-1 override → conditional override
//                         → weighted-random pattern)
//   SelectTargetForIntent: per-intent target picker honoring rangeMin/rangeMax
//                         and the intent's targetPreference (LowestHP, etc.)
//   ExecuteMove:          resolve every intent in the move via SkillResolver,
//                         returns the per-effect result list for CombatManager
//                         playback.
//
// CombatManager drives this during ExecuteEnemyPhase. The pattern + weights +
// conditional overrides live on EnemyData.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class EnemyAI
    {
        /// <summary>
        /// Pick the move this enemy will use on its next action. Priority
        /// order: turn-1 override > first triggered conditional override >
        /// weighted-random from movePattern.
        /// </summary>
        public static EnemyMove DecideMove(Unit enemy)
        {
            if (enemy == null || enemy.enemyData == null) return null;

            // Turn-1 override (always wins).
            if (enemy.enemyData.hasTurn1MoveOverride
                && !enemy.hasActedThisTurn
                && enemy.enemyData.turn1MoveOverride != null)
            {
                return enemy.enemyData.turn1MoveOverride;
            }

            // Conditional overrides: first matching trigger wins. Polled here so
            // both SetEnemyIntent (intent telegraph) and the actual execution
            // route through the same logic.
            var conditional = ConditionalMoveResolver.PickOverride(enemy);
            if (conditional != null) return conditional;

            return enemy.GetNextEnemyMove();
        }

        /// <summary>
        /// Per-intent target picker — filters players by the intent's
        /// rangeMin/rangeMax then ranks by targetPreference. Returns null
        /// when nothing's in range (offensive effects whiff gracefully;
        /// Self / AllAllies / AllEnemies effects ignore the primary target).
        /// </summary>
        public static Unit SelectTargetForIntent(
            Unit enemy, EnemyIntent intent, List<Unit> playerUnits)
        {
            if (playerUnits == null || enemy == null) return null;

            int rangeMin = intent != null ? Mathf.Max(1, intent.rangeMin) : 1;
            int rangeMax = intent != null ? Mathf.Max(rangeMin, intent.rangeMax) : int.MaxValue;

            // Filter to alive + in-range.
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

        /// <summary>
        /// Legacy single-pick over alive players, ignoring range and intent.
        /// Kept for callers that don't have an intent in hand (debug tools,
        /// future scripted events). Prefer SelectTargetForIntent in resolution.
        /// </summary>
        public static Unit SelectTarget(List<Unit> playerUnits)
        {
            if (playerUnits == null) return null;
            var alive = new List<Unit>();
            for (int i = 0; i < playerUnits.Count; i++)
                if (playerUnits[i] != null && playerUnits[i].IsAlive) alive.Add(playerUnits[i]);
            if (alive.Count == 0) return null;
            return alive[Random.Range(0, alive.Count)];
        }

        /// <summary>
        /// Execute an enemy's chosen move. The resolver picks per-intent
        /// targets via SelectTargetForIntent; CombatManager iterates the
        /// returned results through the shared playback pipeline.
        /// </summary>
        public static List<CombatActionResult> ExecuteMove(
            Unit enemy, EnemyMove move,
            List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            return SkillResolver.ResolveEnemyMove(enemy, move, playerUnits, enemyUnits);
        }

        // ─── Target preference ranking ───────────────────────────────────────

        private static Unit PickByPreference(
            List<Unit> pool, EnemyTargetPreference pref, ConditionID condId)
        {
            if (pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            if (pref == EnemyTargetPreference.Random)
                return pool[Random.Range(0, pool.Count)];

            // Condition filter — pick uniformly among matches; fall back to
            // the full pool when no one matches (so the AI doesn't lock up).
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

            // Numeric extremum — rank by score, then random over the tied
            // best group so two equal targets share a 50/50 split.
            int bestScore = int.MaxValue;
            for (int i = 0; i < pool.Count; i++)
                bestScore = Mathf.Min(bestScore, ScoreFor(pool[i], pref));

            var ties = new List<Unit>();
            for (int i = 0; i < pool.Count; i++)
                if (ScoreFor(pool[i], pref) == bestScore) ties.Add(pool[i]);
            return ties[Random.Range(0, ties.Count)];
        }

        /// <summary>
        /// Lower score = preferred. Negate the underlying stat for "Highest"
        /// variants so the same min-finding pass works for both directions.
        /// </summary>
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
