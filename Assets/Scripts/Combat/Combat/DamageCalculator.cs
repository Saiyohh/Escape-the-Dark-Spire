// DamageCalculator.cs
// -----------------------------------------------------------------------------
// Computes final damage for weapon hits and skill damage effects.
// Called by SkillResolver during player + enemy actions.
//
// Vulnerable is a damage amp (×1.5), not an accuracy debuff — intentional
// and distinct from Weak (which reduces POW). Applied here as a post-
// modifier on the target side.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public static class DamageCalculator
    {
        /// <summary>
        /// Calculate final damage for a weapon/skill hit.
        /// Formula: (baseDmg * critMult) + attacker<stat> — Weak is applied on the
        /// stat side via EffectivePOW already.
        /// Crit: doubles base damage BEFORE adding stat.
        /// Vulnerable on target: ×1.5 final (round down) — damage amp only,
        /// does NOT stack with any DEF change.
        /// </summary>
        /// <param name="stat">Which caster stat adds to damage. Defaults to POW
        /// so existing weapon and skill call sites behave as before.</param>
        public static int CalculateDamage(
            int baseDamage, Unit attacker, Unit target, bool isCrit,
            DamageStat stat = DamageStat.POW)
        {
            int base_ = isCrit ? baseDamage * 2 : baseDamage;

            // Stat bonus from the caster.
            int damage = base_ + StatBonus(attacker, stat);

            // Floor at 0
            damage = Mathf.Max(0, damage);

            // Vulnerable on target: ×1.5
            if (target.conditions.HasCondition(ConditionID.Vulnerable))
                damage = Mathf.FloorToInt(damage * 1.5f);

            return Mathf.Max(0, damage);
        }

        /// <summary>
        /// Direct damage (poison, burn, Apply+Damage). No DEF check. Optionally
        /// scales from a caster stat — pass <see cref="DamageStat.None"/> (the
        /// default) for the classic "magnitude is final" behavior.
        /// </summary>
        public static int CalculateDirectDamage(
            int baseDamage, Unit attacker = null, DamageStat stat = DamageStat.None)
        {
            int damage = baseDamage + StatBonus(attacker, stat);
            return Mathf.Max(0, damage);
        }

        /// <summary>
        /// Non-mutating preview of what an outgoing damage value would be
        /// AFTER the attacker's own modifiers (Strength, Weak, …). Used by
        /// IntentIconUI so the displayed enemy intent damage reflects, e.g.,
        /// a Weak stack landing on the enemy before they attack.
        ///
        /// Excludes target-side modifiers (Vulnerable, DEF, Shields) because
        /// the intent preview doesn't know which party member will take the
        /// hit. Excludes crit (intent assumes a normal hit).
        ///
        /// Currently models the slice's two main outgoing-damage modifiers:
        ///   • Strength is already in EffectivePOW via passive POW modifier.
        ///   • Weak is a -25%/stack multiplicative reduction applied here.
        /// Add new modifiers (e.g. Enraged, Crippled) by extending the
        /// `attacker.conditions.GetStacks` checks below.
        /// </summary>
        public static int PreviewOutgoingDamage(
            int baseDamage, Unit attacker, DamageStat stat = DamageStat.POW)
        {
            int damage = baseDamage + StatBonus(attacker, stat);

            if (attacker != null && attacker.conditions != null)
            {
                int weak = attacker.conditions.GetStacks(ConditionID.Weak);
                if (weak > 0)
                    damage = Mathf.FloorToInt(damage * Mathf.Pow(0.75f, weak));
            }
            return Mathf.Max(0, damage);
        }

        /// <summary>Resolves the caster-stat bonus. Null attacker or None stat → 0.</summary>
        private static int StatBonus(Unit attacker, DamageStat stat)
        {
            if (attacker == null) return 0;
            return stat switch
            {
                DamageStat.POW => attacker.EffectivePOW,
                DamageStat.DEX => attacker.EffectiveDEX,
                DamageStat.WIL => attacker.EffectiveWIL,
                _              => 0,
            };
        }

        /// <summary>
        /// Description-system hook: the multiplicative damage modifier the
        /// target's currently-active conditions would impose on incoming attack
        /// damage. Today: Vulnerable → ×1.5. Returns false when nothing applies.
        ///
        /// Kept here (not in NumberEvaluator) so the rule lives next to
        /// CalculateDamage's hardcoded ×1.5 — when a new target-side amp lands
        /// (e.g. Bruise rules), update both call sites here and the description
        /// breakdown picks it up automatically.
        /// </summary>
        public static bool TryGetIncomingMultiplier(Unit target, out float multiplier, out string label)
        {
            multiplier = 1f;
            label = null;
            if (target == null || target.conditions == null) return false;

            // Vulnerable: ×1.5 (matches CalculateDamage). Only emits a single
            // multiplier today; if multiple amps stack later, return their
            // product and a combined label, or restructure to emit a list.
            if (target.conditions.HasCondition(ConditionID.Vulnerable))
            {
                multiplier = 1.5f;
                label = "Vulnerable";
                return true;
            }
            return false;
        }
    }
}
