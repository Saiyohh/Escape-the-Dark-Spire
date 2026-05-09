// ConditionEventContexts.cs
// -----------------------------------------------------------------------------
// Mutable context objects passed to condition-trigger handlers. Each event
// that a condition can subscribe to gets its own context class so trigger
// actions can read + modify the event state without breaking the per-event
// signature.
//
// Flow:
//   1. Combat code builds a context (e.g. new DamageContext { amount = 10, ... })
//   2. Fires the event via ConditionManager.Fire(TriggerEvent.OnTakeDamagePre, ctx)
//   3. Handlers may mutate ctx.amount, ctx.negated, etc.
//   4. Combat code reads the final ctx to apply the outcome
//
// Using classes (not structs) so the runtime can pass them around and let
// multiple handlers compose modifications without copying.
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    /// <summary>
    /// Context for OnTakeDamagePre / OnTakeDamagePost. Pre-handlers can
    /// reduce the amount or negate entirely. Post-handlers are reactive
    /// (e.g. retaliate after a hit resolves).
    /// </summary>
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

    /// <summary>
    /// Context for OnDealDamage. Fires when a unit deals damage — handlers can
    /// add to outgoing damage (Vigor: +N to next attack that hits).
    /// </summary>
    public class OutgoingDamageContext
    {
        public Unit attacker;
        public Unit defender;
        public int amount;             // mutable
        public bool isCrit;
        public bool didHit;            // true only if the attack actually connected
    }

    /// <summary>
    /// Context for OnAttackRoll. Handlers can modify rawRoll / totalRoll
    /// before the hit/crit determination finalizes (e.g. Dazed subtracts
    /// from total, Precision adds to total).
    /// </summary>
    public class AttackRollContext
    {
        public Unit attacker;
        public Unit target;
        public int rawRoll;            // mutable — raw d20
        public int totalRoll;          // mutable — raw + modifiers so far
        public int modifier;           // running modifier stack
        public int targetDEF;
    }

    /// <summary>
    /// Context for OnDebuffApplied / OnBuffApplied / OnConditionApplied.
    /// Pre-apply so Artifact can negate before the stack touches the manager.
    /// </summary>
    public class ConditionApplicationContext
    {
        public Unit target;            // unit receiving the condition
        public Unit source;            // unit applying it (may be null)
        public ConditionData condition;
        public int stacks;             // mutable
        public bool negated;           // Artifact sets this true to block application
    }

    /// <summary>
    /// Context for OnSkillPlayed. Handlers subscribed by skill-triggered
    /// conditions (e.g. "whenever you play a skill, channel 1 Lightning")
    /// read the skill + tags.
    /// </summary>
    public class SkillPlayedContext
    {
        public Unit caster;
        public SkillData skill;
        public System.Collections.Generic.List<Unit> targets;
    }
}
