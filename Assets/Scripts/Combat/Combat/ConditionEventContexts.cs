namespace DarkSpire
{
    public class DamageContext
    {
        public Unit source;            // attacker or null (for environmental damage)
        public Unit target;            // unit receiving damage
        public int amount;             // mutable — current damage after handlers so far
        public bool negated;           // set true to cancel the hit (Dodge/Artifact)
        public bool isCrit;            // set by the attack roll
        public bool bypassesShields;   // Poison/Burning/Doom damage
        public bool bypassesDEF;

        public void Reset(Unit src, Unit tgt, int dmg)
        {
            source = src; target = tgt;
            amount = dmg;
            negated = false;
            isCrit = false;
            bypassesShields = false;
            bypassesDEF = false;
        }
    }

    public class OutgoingDamageContext
    {
        public Unit attacker;
        public Unit defender;
        public int amount;             // mutable
        public bool isCrit;
        public bool didHit;            // true only if the attack actually connected
    }

    public class AttackRollContext
    {
        public Unit attacker;
        public Unit target;
        public int rawRoll;            // mutable — raw d20
        public int totalRoll;          // mutable — raw + modifiers so far
        public int modifier;           // running modifier stack
        public int targetDEF;
    }

    public class ConditionApplicationContext
    {
        public Unit target;            // unit receiving the condition
        public Unit source;            // unit applying it (may be null)
        public ConditionData condition;
        public int stacks;             // mutable
        public bool negated;           // Artifact sets this true to block application
    }

    public class SkillPlayedContext
    {
        public Unit caster;
        public SkillData skill;
        public System.Collections.Generic.List<Unit> targets;
    }
}
