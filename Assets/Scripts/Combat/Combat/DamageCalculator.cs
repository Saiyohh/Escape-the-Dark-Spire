using UnityEngine;

namespace DarkSpire
{
    public static class DamageCalculator
    {
        // Reusable context object so the OnDealDamage hot path doesn't allocate
        // per hit. Single-threaded combat → fine to share one static instance.
        private static readonly OutgoingDamageContext sharedOutgoingCtx = new();

        public static int CalculateDamage(
            int baseDamage, Unit attacker, Unit target, bool isCrit,
            DamageStat stat = DamageStat.POW)
        {
            int base_ = isCrit ? baseDamage * 2 : baseDamage;
            int damage = base_ + StatBonus(attacker, stat);
            return Mathf.Max(0, damage);
        }

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

        public static int CalculateDirectDamage(
            int baseDamage, Unit attacker = null, DamageStat stat = DamageStat.None)
        {
            int damage = baseDamage + StatBonus(attacker, stat);
            return Mathf.Max(0, damage);
        }

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
