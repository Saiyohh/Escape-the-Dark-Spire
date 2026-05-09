// ConditionTrigger.cs
// -----------------------------------------------------------------------------
// The compositional core of the condition system. A ConditionData asset pairs
// identity fields with:
//   • PassiveModifier[]  — always-on stat changes (Strength +POW, Weak −POW, etc.)
//   • ConditionTrigger[] — event-driven reactions (when X happens, do Y, then
//                          change my stacks by Z)
//
// The four parts of each trigger:
//   WHEN       TriggerEvent — which combat event fires me
//   IF...      TriggerConditional[] — optional filters (stacks >=, chance, etc.)
//   DO         TriggerAction[] — what to do when I fire
//   THEN       StackOp — how my stacks change after firing
//
// Runtime dispatch lives in ConditionManager. Designers compose new conditions
// entirely in the inspector via the custom ConditionDataEditor.
// -----------------------------------------------------------------------------
using System;

namespace DarkSpire
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Event vocabulary — the moments a condition can hook into.
    // ═════════════════════════════════════════════════════════════════════════

    public enum TriggerEvent
    {
        // Per-unit turn flow (afflicted unit's individual turn)
        OnTurnStart,
        OnTurnEnd,

        // Round-level flow (global — fires on every unit at once)
        OnRoundStart,           // before player phase begins, before any turns
        OnRoundEnd,             // after enemy phase + cleanup
        OnPlayerPhaseStart,     // before any player takes their turn
        OnPlayerPhaseEnd,       // after all players have acted
        OnEnemyPhaseStart,
        OnEnemyPhaseEnd,

        OnCleanup,              // global CleanupPhase tick (legacy — Regen uses this)
        OnCombatStart,
        OnCombatEnd,

        // Damage flow (afflicted unit as target)
        OnTakeDamagePre,        // before Shields/HP mutation — can modify / negate
        OnTakeDamagePost,       // after damage resolved — reactive
        OnDealDamage,           // this unit dealt damage — can modify outgoing
        OnAttackRoll,           // this unit's d20 attack roll — can modify

        // Hit/miss reactions (afflicted unit as ATTACKER)
        OnHit,
        OnMiss,

        // Condition/skill reactions (afflicted unit as owner)
        OnDebuffApplied,        // any debuff just landed on me
        OnBuffApplied,
        OnSkillPlayed,          // I played a skill (can filter by tag)
        OnConditionApplied,     // I applied a condition to someone

        // Life events
        OnKill,
        OnDeath,
    }

    /// <summary>
    /// When a condition with this timing auto-clears. Replaces the old
    /// `clearsAtTurnStart` bool to disambiguate owner-vs-round scoped
    /// removal.
    ///
    ///   • Never           — persist until removed explicitly (Strength, Doom)
    ///   • OwnerTurnStart  — clears when this unit's OWN turn begins.
    ///                        Used for per-unit cooldowns: Guarding (+4 DEF),
    ///                        Flanked, Intangible, most Duration-style turn buffs.
    ///   • RoundStart      — clears at the start of the NEXT round, before any
    ///                        turn begins. Used for buffs meant to protect the
    ///                        whole round regardless of who got them — Shields
    ///                        is the canonical example (if Ally 1 gives Ally 2
    ///                        Shields mid-round, Ally 2 still has it when their
    ///                        turn comes).
    ///   • RoundEnd        — clears at end of current round (before cleanup→next).
    ///                        Rarely needed; provides a symmetric option.
    ///
    /// The structural flag `defensePersists` (Barricade) overrides RoundStart
    /// clears of Shields specifically.
    /// </summary>
    public enum ClearTiming
    {
        Never,
        OwnerTurnStart,
        RoundStart,
        RoundEnd,
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stat kinds — what passive modifiers (and stat-modify actions) can touch.
    // ═════════════════════════════════════════════════════════════════════════

    public enum StatKind
    {
        POW,                    // Strength/Weak/Enraged
        DEX,                    // Dexterity/Enraged
        DEF,                    // Guarding/Vulnerable-variant
        WIL,
        SPD,
        DodgeChancePerStack,    // Dodge (0-1 per stack)
        DamageTakenMultiplier,  // Vulnerable (×1.5 flat, treated as 1+amount)
        OutgoingDamageFlat,     // Vigor fallback, specific damage adds
        OrbFocusBonus,          // Defect-subsystem hook (future)
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Conditionals — the "IF..." filters that gate trigger firing.
    // ═════════════════════════════════════════════════════════════════════════

    public enum ConditionalKind
    {
        Always,                 // no-op; useful placeholder

        // Stack thresholds
        StacksAtLeast,          // intParam = threshold
        StacksAtMost,

        // Target state
        TargetHPBelowPercent,   // floatParam = 0-1
        TargetHPAbovePercent,
        SourceHPBelowPercent,

        // Chance
        RollSucceeds,           // floatParam = 0-1 probability (flat)
        RollSucceedsPerStack,   // chance = floatParam × stacks

        // Event-context filters
        IncomingIsDebuff,       // Artifact: only negate debuffs
        IncomingIsBuff,
        IncomingConditionIs,    // conditionParam = specific ConditionID
        SourceSkillHasTag,      // tagFilter bitmask
        IsFirstEventThisTurn,   // Vigor: "next attack" — first OnDealDamage this turn
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Actions — the "DO" that executes when the trigger fires.
    // ═════════════════════════════════════════════════════════════════════════

    public enum TriggerActionKind
    {
        // Damage / heal
        DealDamage,                  // flat amount
        DealDamagePerStack,          // amount = amountPerStack × currentStacks
        HealTarget,
        HealTargetPerStack,
        DamageSource,                // retaliate — damages the attacker (Thorns, FlameBarrier)
        DamageSourcePerStack,

        // Defense / absorption
        AbsorbDamage,                // reduce incoming damage by flat amount
        AbsorbDamagePerStack,        // reduce by amountPerStack × stacks (Shields pattern)
        GrantGuard,                  // add Guard condition stacks
        GrantGuardPerStack,
        NegateIncomingEffect,        // Artifact, Dodge — cancel the incoming damage/condition

        // Attack-roll modifiers (OnAttackRoll handlers)
        ModifyAttackRollBy,          // flat
        ModifyAttackRollByPerStack,  // Dazed: −1 per stack

        // Damage-modifier actions
        ModifyIncomingDamageFlat,    // +N or −N to incoming
        ModifyIncomingDamagePercent, // Vulnerable: ×1.5 → percentValue = 0.5 (extra)
        ModifyOutgoingDamageFlat,    // Vigor: +N to this attack's damage
        ModifyOutgoingDamagePerStack,
        ModifyOutgoingDamagePercent, // Weak: ×0.75 → percentValue = -0.25 (25% less damage dealt)

        // Condition manipulation
        ApplyCondition,              // conditionID + amount
        ApplyConditionPerStack,
        RemoveCondition,

        // Stat passive-override (rare; usually PassiveModifier handles this)
        ModifyStat,

        // Death / lifecycle
        KillTarget,                  // Doom's execute
    }

    public enum ActionTarget
    {
        Self,                        // afflicted unit
        Source,                      // whoever caused the trigger (attacker, debuff applier, etc.)
        AllEnemies,                  // from afflicted unit's perspective
        AllAllies,
        RandomEnemy,
        RandomAlly,
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stack ops — how stacks change after the trigger fires.
    // ═════════════════════════════════════════════════════════════════════════

    public enum StackOp
    {
        NoChange,                    // Strength, Doom — stacks persist
        DecrementByOne,              // Poison, Plating, Dazed-after-attack
        DecrementByN,                // custom (uses amount field on trigger)
        ConsumeAll,                  // Vigor-on-hit
        ConsumeN,                    // Artifact (1), Dodge (1 on successful dodge)
        ConsumeIfActionLanded,       // Dodge-style: only consume if NegateIncomingEffect actually negated
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Serializable data classes
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Static stat bonus applied while this condition has stacks. Evaluated
    /// every time Unit.Effective* is read — cheap because ConditionManager
    /// sums these with a single pass.
    /// </summary>
    [Serializable]
    public class PassiveModifier
    {
        public StatKind stat;
        public float amountPerStack;   // float to support DodgeChance (0.15) + int stats
    }

    /// <summary>
    /// Optional "IF..." filter on a trigger. All conditionals on a trigger must
    /// pass (AND semantics) for the actions to fire.
    /// </summary>
    [Serializable]
    public class TriggerConditional
    {
        public ConditionalKind kind;
        public int intParam;
        public float floatParam;
        public ConditionID conditionParam;
        public SkillTag tagFilter;
    }

    /// <summary>
    /// One "DO" step. Multiple actions on a trigger run in order (they can
    /// read the mutable event context from previous actions).
    /// </summary>
    [Serializable]
    public class TriggerAction
    {
        public TriggerActionKind kind;
        public ActionTarget target = ActionTarget.Self;
        public int amount;
        public int amountPerStack;
        public float percentValue;
        public ConditionID conditionID;
        public int conditionStacks = 1;
        public StatKind stat;
    }

    /// <summary>
    /// A single trigger on a condition: WHEN + IF + DO + STACK-OP.
    /// A condition can have any number of these (each fires independently
    /// when its event matches).
    /// </summary>
    [Serializable]
    public class ConditionTrigger
    {
        [UnityEngine.Tooltip("When in combat this trigger fires.")]
        public TriggerEvent when;

        [UnityEngine.Tooltip("Optional filters. ALL must pass for actions to execute.")]
        public TriggerConditional[] onlyIf;

        [UnityEngine.Tooltip("What happens when the trigger fires. Runs in order.")]
        public TriggerAction[] actions;

        [UnityEngine.Tooltip("How the condition's own stacks change after firing.")]
        public StackOp afterFiring = StackOp.NoChange;

        [UnityEngine.Tooltip("Used by DecrementByN / ConsumeN when afterFiring needs a count.")]
        public int stackOpAmount = 1;
    }
}
