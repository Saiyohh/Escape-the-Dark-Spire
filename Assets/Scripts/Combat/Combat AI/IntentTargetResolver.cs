// IntentTargetResolver.cs
// -----------------------------------------------------------------------------
// Expands an EnemyIntent into the per-player list it will threaten, with
// pre-computed hit% / afflict% per target. Used by DangerPreviewController
// to drive the on-hover red/purple auras over targeted players.
//
// Pipeline (per effect on the intent):
//   1. Skip Self / AllAllies / SingleAlly — those target the enemy or its
//      allies, not players, so they don't endanger anyone we'd highlight.
//   2. Pick the intent's primary target once via
//      EnemyAI.SelectTargetForIntent (honors rangeMin/rangeMax and
//      targetPreference). Reused across every effect in the intent so a
//      RandomEnemy / SingleEnemy follow-up isn't independently random.
//   3. Expand to the effect's full target list via
//      SkillResolver.ResolveTargets (handles AllEnemies fanning out, etc.).
//   4. Filter to alive player-controlled units (defensive — if the intent
//      somehow points at enemies via odd targetMode, skip).
//   5. Per target, accumulate hit% and/or afflict% into a deduped entry.
//
// Same target appearing in multiple effects merges:
//   - hit %       → first-encountered value is kept. Formula only depends on
//                   attacker.ATK / target.DEF so multiple Attack effects
//                   produce the same number; first-wins documented for the
//                   rare case where future content varies per-effect ATK.
//   - afflict %   → first-encountered value (same reasoning, vs casterWIL /
//                   targetWIL / saveDC of the first contributing effect).
//
// Random-target intents (TargetMode.RandomEnemy) preview against the primary
// the AI picks; the actual roll re-randomizes at resolve time. Documented
// limitation — fine for a "in danger" preview.
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace DarkSpire
{
    public readonly struct IntentTargetEntry
    {
        public readonly Unit unit;
        public readonly bool hasAttack;
        public readonly bool hasAfflict;
        public readonly float hitPct;
        public readonly float afflictPct;
        public readonly ConditionID afflictCondition;

        public IntentTargetEntry(
            Unit unit, bool hasAttack, bool hasAfflict,
            float hitPct, float afflictPct, ConditionID afflictCondition)
        {
            this.unit = unit;
            this.hasAttack = hasAttack;
            this.hasAfflict = hasAfflict;
            this.hitPct = hitPct;
            this.afflictPct = afflictPct;
            this.afflictCondition = afflictCondition;
        }
    }

    public static class IntentTargetResolver
    {
        // Reused across calls to avoid per-hover allocation. Cleared at the
        // top of Resolve(). Not thread-safe — single-threaded UI is the only
        // caller.
        private static readonly List<Unit> _selectedScratch = new();
        private static readonly List<Unit> _expandedScratch = new();
        private static readonly Dictionary<Unit, EntryBuilder> _dedupe = new();

        private struct EntryBuilder
        {
            public bool hasAttack;
            public bool hasAfflict;
            public float hitPct;
            public float afflictPct;
            public ConditionID afflictCondition;
        }

        /// <summary>
        /// Walk every effect on <paramref name="intent"/>, expand to player
        /// targets, and write one IntentTargetEntry per affected player into
        /// <paramref name="output"/>. Output is cleared first.
        /// </summary>
        public static void Resolve(
            Unit source, EnemyIntent intent,
            List<Unit> playerUnits, List<Unit> enemyUnits,
            List<IntentTargetEntry> output)
        {
            output.Clear();
            _dedupe.Clear();

            if (source == null || intent == null || intent.effects == null) return;
            if (playerUnits == null || playerUnits.Count == 0) return;

            // Read the primary target locked in at intent-set time so the
            // preview shows the same player the resolver will actually hit.
            // Fall back to a live pick only if locking didn't happen.
            var primary = ResolveLockedPrimary(source, intent, playerUnits);
            _selectedScratch.Clear();
            if (primary != null) _selectedScratch.Add(primary);

            var lib = ConditionLibrary.Instance;

            for (int i = 0; i < intent.effects.Length; i++)
            {
                var effect = intent.effects[i];
                if (effect == null) continue;

                // Skip effects that don't threaten players. Self / AllAllies /
                // SingleAlly on an enemy = the enemy's own side (other enemies).
                var mode = effect.targetMode;
                if (mode == TargetMode.Self
                    || mode == TargetMode.AllAllies
                    || mode == TargetMode.SingleAlly
                    || mode == TargetMode.RandomAlly)
                    continue;

                bool isAttack  = effect.effectType == SkillEffectType.Attack && effect.magnitude > 0;
                bool isAfflict = effect.AppliesCondition && effect.conditionStacks > 0;
                if (!isAttack && !isAfflict) continue;

                // Restrict afflicts to debuffs — enemies "afflicting" a player
                // with a positive condition isn't danger. Mirrors the old
                // ChanceBox.ResolveAfflictTint debuff filter.
                if (isAfflict && lib != null)
                {
                    var condData = lib.Get(effect.conditionID);
                    if (condData != null && !condData.isDebuff) isAfflict = false;
                }
                if (!isAttack && !isAfflict) continue;

                // Expand TargetMode → unit list. ResolveTargets handles
                // AllEnemies (=> all alive players when caster is the enemy),
                // SingleEnemy (=> the primary we pre-picked), RandomEnemy
                // (=> picks one at random; preview is optimistic).
                _expandedScratch.Clear();
                var resolved = SkillResolver.ResolveTargets(
                    mode, source, _selectedScratch, playerUnits, enemyUnits,
                    pickIndex: effect.targetPickIndex);
                if (resolved != null) _expandedScratch.AddRange(resolved);

                for (int t = 0; t < _expandedScratch.Count; t++)
                {
                    var unit = _expandedScratch[t];
                    if (unit == null || !unit.IsAlive) continue;
                    if (!unit.isPlayerControlled) continue;

                    _dedupe.TryGetValue(unit, out var b);

                    if (isAttack && !b.hasAttack)
                    {
                        b.hasAttack = true;
                        b.hitPct = AttackChance.Hit(source, unit);
                    }
                    if (isAfflict && !b.hasAfflict)
                    {
                        b.hasAfflict = true;
                        b.afflictPct = AttackChance.Afflict(source, unit, effect.saveDC);
                        b.afflictCondition = effect.conditionID;
                    }

                    _dedupe[unit] = b;
                }
            }

            foreach (var kvp in _dedupe)
            {
                var b = kvp.Value;
                output.Add(new IntentTargetEntry(
                    kvp.Key, b.hasAttack, b.hasAfflict,
                    b.hitPct, b.afflictPct, b.afflictCondition));
            }
        }

        /// <summary>
        /// Map an EnemyIntent back to the primary target the enemy committed
        /// to when their move was set. Falls back to a live SelectTargetForIntent
        /// if the lock is missing or stale (dead target, or this intent isn't
        /// part of the enemy's currentMove — debug paths, ad-hoc previews).
        /// </summary>
        private static Unit ResolveLockedPrimary(
            Unit source, EnemyIntent intent, List<Unit> playerUnits)
        {
            var move = source.currentMove;
            var locked = source.lockedIntentTargets;
            if (move != null && move.intents != null && locked != null)
            {
                for (int i = 0; i < move.intents.Length && i < locked.Length; i++)
                {
                    if (!ReferenceEquals(move.intents[i], intent)) continue;
                    var t = locked[i];
                    if (t != null && t.IsAlive) return t;
                    break;
                }
            }
            return EnemyAI.SelectTargetForIntent(source, intent, playerUnits);
        }
    }
}
