// DamageCalculator.cs
// -----------------------------------------------------------------------------
// Computes final damage for weapon hits and skill damage effects.
// Called by SkillResolver during player + enemy actions.
//
// Pipeline (per hit):
//   1. CalculateDamage         — base + stat, floored. Pure, no triggers fire.
//   2. ApplyOutgoingTriggers   — fire attacker.conditions OnDealDamage; this is
//                                where Weak / Vigor / future outgoing rules apply.
//   3. target.TakeDamage(...)  — fires target.conditions OnTakeDamagePre; this is
//                                where Vulnerable / Shields / Dodge apply.
//
// Vulnerable was historically a hardcoded ×1.5 here in CalculateDamage AND
// a trigger on CND_Vulnerable that did the same thing — double-amping every
// hit. The hardcoded path is gone now; the trigger is authoritative.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public static class DamageCalculator
    {
        // Reusable context object so the OnDealDamage hot path doesn't allocate
        // per hit. Single-threaded combat → fine to share one static instance.
        private static readonly OutgoingDamageContext sharedOutgoingCtx = new();

        /// <summary>
        /// Pre-modifier hit damage: (base × critMult) + attacker&lt;stat&gt;, floored at 0.
        /// All other buffs/debuffs are applied by the trigger system afterwards:
        /// caller is responsible for chaining <see cref="ApplyOutgoingTriggers"/>
        /// before passing the result to <see cref="Unit.TakeDamage"/>.
        /// </summary>
        /// <param name="stat">Which caster stat adds to damage. Defaults to POW.</param>
        public static int CalculateDamage(
            int baseDamage, Unit attacker, Unit target, bool isCrit,
            DamageStat stat = DamageStat.POW)
        {
            int base_ = isCrit ? baseDamage * 2 : baseDamage;
            int damage = base_ + StatBonus(attacker, stat);
            return Mathf.Max(0, damage);
        }

        /// <summary>
        /// Run the attacker's OnDealDamage triggers so caster-side outgoing
        /// modifiers (Weak's ×0.75, Vigor's +N, future Burn-style amps) can
        /// adjust the damage before it lands. Call this between
        /// <see cref="CalculateDamage"/> and <see cref="Unit.TakeDamage"/> on
        /// every actual-damage resolution path.
        /// </summary>
        public static int ApplyOutgoingTriggers(
            int damage, Unit attacker, Unit target, bool isCrit, bool didHit)
        {
            if (attacker == null || attacker.conditions == null) return Mathf.Max(0, damage);

            sharedOutgoingCtx.attacker = attacker;
            sharedOutgoingCtx.defender = target;
            sharedOutgoingCtx.amount   = damage;
            sharedOutgoingCtx.isCrit   = isCrit;
            sharedOutgoingCtx.didHit   = didHit;

            attacker.conditions.FireDealDamage(sharedOutgoingCtx);

            return Mathf.Max(0, sharedOutgoingCtx.amount);
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
        /// Non-mutating preview of what an outgoing damage value would be after
        /// the attacker's own modifiers. Used by IntentIconUI for enemy intents.
        ///
        /// SIMULATION (intentional duplication): mirrors the actual trigger
        /// behavior on CND_Weak (-25% flat per the SO's trigger configuration).
        /// We don't actually fire the trigger here because Fire() has side
        /// effects (StackOp consumes, ApplyCondition actions, etc.) that we
        /// don't want during a hover/preview. If you add new outgoing-damage
        /// triggers (Vigor, Enraged, …) update this method too.
        /// </summary>
        public static int PreviewOutgoingDamage(
            int baseDamage, Unit attacker, DamageStat stat = DamageStat.POW)
        {
            int damage = baseDamage + StatBonus(attacker, stat);

            if (attacker != null && attacker.conditions != null)
            {
                // Matches CND_Weak.asset's trigger: ModifyOutgoingDamagePercent(-0.25).
                // Flat -25% when Weak is present, not per-stack compound. If you
                // want compounding Weak, change both this and the trigger setup.
                if (attacker.conditions.HasCondition(ConditionID.Weak))
                    damage = Mathf.FloorToInt(damage * 0.75f);
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
        /// Description-system hook: target-side multiplicative modifier the
        /// hovered target would impose on incoming attack damage. Used by the
        /// number calc box to show "× Vulnerable (1.5x)" in the breakdown.
        ///
        /// Stays in sync with whatever target-side triggers exist on the SO —
        /// when a new amp lands (e.g. Bruise rules), add a case here.
        /// </summary>
        public static bool TryGetIncomingMultiplier(Unit target, out float multiplier, out string label)
        {
            multiplier = 1f;
            label = null;
            if (target == null || target.conditions == null) return false;

            // Vulnerable: ×1.5 (mirrors CND_Vulnerable's OnTakeDamagePre trigger).
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
