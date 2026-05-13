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

        public static void Resolve(
            Unit source, EnemyIntent intent,
            List<Unit> playerUnits, List<Unit> enemyUnits,
            List<IntentTargetEntry> output)
        {
            output.Clear();
            _dedupe.Clear();

            if (source == null || intent == null || intent.effects == null) return;
            if (playerUnits == null || playerUnits.Count == 0) return;

            var primary = ResolveLockedPrimary(source, intent, playerUnits);
            _selectedScratch.Clear();
            if (primary != null) _selectedScratch.Add(primary);

            var lib = ConditionLibrary.Instance;

            for (int i = 0; i < intent.effects.Length; i++)
            {
                var effect = intent.effects[i];
                if (effect == null) continue;

                var mode = effect.targetMode;
                if (mode == TargetMode.Self
                    || mode == TargetMode.AllAllies
                    || mode == TargetMode.SingleAlly
                    || mode == TargetMode.RandomAlly)
                    continue;

                bool isAttack  = effect.effectType == SkillEffectType.Attack && effect.magnitude > 0;
                bool isAfflict = effect.AppliesCondition && effect.conditionStacks > 0;
                if (!isAttack && !isAfflict) continue;

                if (isAfflict && lib != null)
                {
                    var condData = lib.Get(effect.conditionID);
                    if (condData != null && !condData.isDebuff) isAfflict = false;
                }
                if (!isAttack && !isAfflict) continue;

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
