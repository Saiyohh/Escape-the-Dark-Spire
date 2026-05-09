// CombatEnums.cs
// -----------------------------------------------------------------------------
// All shared enums used by combat systems. Kept in one file so SkillData,
// EnemyData, ConditionData, Unit, CombatManager etc. can reference them
// without circular-include concerns.
//
// Dark Spire note: Pass 2 rules work is complete as of 2.C. Done: stat
// renames (AC→DEF, MGT→POW, AGI→SPD, SOUL/VIG→WIL); cooldown removal;
// TargetMode.RandomAlly/WholeParty; Shields condition replacing
// temporaryDefense; Guarding data-driven via defModifier; hasBonus → hasFree;
// Poison tick at afflicted unit's TurnStart; nat-1 crit-miss = 1 self-damage;
// rank + range + movement keywords; WIL save (SkillDiceRule + DiceRoller.SaveRoll).
// Powers/Echoes relic enums below are dead — left in so the ported files
// compile unchanged.
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    public enum CombatPhase
    {
        CombatStart,
        PlayerInitiativeRoll,
        PlayerPhase,
        EnemyInitiativeRoll,
        EnemyPhase,
        CleanupPhase,
        Victory,
        Defeat
    }

    public enum ActionType { Attack, Guard, Skill, Pass, Item, Move, Flee }

    public enum ActionCostType { Action, FreeAction }

    public enum AltCostType { None, HP, Stars, Focus, Souls }

    public enum TargetMode
    {
        SingleEnemy,
        AllEnemies,
        RandomEnemy,
        Self,
        SingleAlly,
        AllAllies,
        RandomAlly,   // Pass 2.B — rank-aware targeting
        WholeParty,   // Hits all four characters regardless of KO status (revive effects)
    }

    /// <summary>
    /// All skill-effect operations. Each maps to a SkillResolver case. Grouped
    /// by category for inspector organization via EffectCategoryOf().
    ///
    /// Subsystem effects (Orb, Osty, Stars/Forge, Shiv, Seal, Extra Action,
    /// Retaliate) are PLACEHOLDERS in Pass 2 — SkillResolver logs a warning
    /// when they fire. Their inspector fields are fully authored so designers
    /// can keep building skill assets; runtime implementation lands per
    /// character-kit pass (Defect Orbs, Necrobinder Osty, Regent Stars, etc.).
    /// </summary>
    public enum SkillEffectType
    {
        // ── Core combat ──────────────────────────────────────
        Attack,            // d20 + ATK vs DEF — hit deals magnitude damage. (Was "Damage".)
        DirectDamage,      // [LEGACY] Use Apply + ApplyKind.Damage. Kept for serialized compat.
        Heal,
        RestoreSP,
        ApplyCondition,    // [LEGACY] Use Apply + ApplyKind.Condition. Kept for serialized compat.
        RemoveCondition,
        Move,              // Positional: uses movementKind + movementMagnitude

        // ── Self-cost (alt-costs on the skill itself) ───────
        LoseHP,            // Self-damage as part of the effect block (Offering, Hemokinesis)

        // ── Action economy ───────────────────────────────────
        Haste,             // Target gets an extra action on their NEXT turn
        Renew,             // Target takes an extra turn immediately after this one
        SealSkill,         // Put a skill on cooldown for N turns (self / chosen / random)

        // ── Item generation (Pouch) ──────────────────────────
        GenerateItem,      // Shiv / Soul / SovereignBlade / etc. (uses pouchItemType)

        // ── Orb system (Defect) — PLACEHOLDERS ──────────────
        ChannelOrb,        // Channel N orbs of orbType (may use orbSource, orbMulti)
        EvokeOrb,          // Trigger an orb's Evoke (first/rightmost via evokeKind)

        // ── Osty / Companions (Necrobinder) — PLACEHOLDERS ──
        SummonCompanion,   // Add HP to Osty (revive if dead); magnitude = HP
        BindCompanion,     // Bind Osty to a target ally for N turns

        // ── Stars & Forge (Regent) — PLACEHOLDERS ───────────
        GainStars,         // Add N Stars to the Regent's pool
        Forge,             // Forge N — bumps Sovereign Blade damage, generates into Pouch if absent

        // ── Triggered / persistent (run-through-turn) — PLACEHOLDER ──
        Retaliate,         // "If an enemy attacks you, they take X damage" until TurnStart
        OnAllyAttackRider, // "Whenever ally attacks, gain X Guard" etc. until TurnStart

        // ── New unified types (added post-rework) ───────────
        Apply,             // No-roll application. Sub-kind (applyKind) chooses Damage or Condition.
        Afflict,           // WIL-save gated condition application. On FAILED save, condition lands.
    }

    /// <summary>
    /// Sub-kind for <see cref="SkillEffectType.Apply"/>. Lets one Apply
    /// effect mean either "deal flat damage" or "put a condition on the
    /// target" without needing two separate enum values.
    /// </summary>
    public enum ApplyKind
    {
        Damage,    // Flat damage; uses magnitude.
        Condition, // Puts stacks of conditionID on the target; stacks via stackCountKind.
    }

    /// <summary>
    /// Which caster stat adds to an Attack or Apply+Damage effect's final
    /// damage. None = no stat bonus (pure magnitude). Picked per-effect so a
    /// single skill can mix POW-scaling attacks with DEX-scaling followups.
    /// </summary>
    public enum DamageStat
    {
        None,   // No stat bonus — magnitude is final.
        POW,    // + caster POW (classic physical scaling).
        DEX,    // + caster DEX (finesse / precision).
        WIL,    // + caster WIL (spell / willpower scaling).
    }

    /// <summary>
    /// Per-effect gate. Each effect in a skill's effect list can be made
    /// conditional on the outcome of the most recent Attack or Afflict
    /// against the same target. Effects gate independently per target —
    /// if only target A was hit, an OnHit effect runs only against A.
    /// </summary>
    public enum ConditionalGate
    {
        Always,     // Unconditional — effect always fires.
        OnHit,      // Requires the most recent Attack against this target to have hit.
        OnMiss,     // Requires the most recent Attack against this target to have missed.
        OnCrit,     // Requires the most recent Attack against this target to have crit.
        OnKill,     // Requires the most recent Attack against this target to have killed it.
        OnResist,   // Requires the most recent Afflict to have been resisted (save succeeded).
        OnFail,     // Requires the most recent Afflict to have failed (caster won; condition landed).
    }

    /// <summary>Coarse grouping used by the skill inspector to title sections.</summary>
    public enum SkillEffectCategory
    {
        Core,
        SelfCost,
        ActionEconomy,
        Item,
        Orb,
        Companion,
        Resource,     // Stars, Forge
        Triggered,
    }

    public enum OrbType { Lightning, Frost, Dark, Light, Plasma, Glass, Random }

    public enum OrbSource
    {
        Self,          // Defect channels
        TargetAlly,    // "An ally channels" (Ignition)
        PerEnemy,      // One per enemy in combat (Chill)
    }

    public enum EvokeKind
    {
        First,         // Default: first orb's Evoke (consumes orb)
        Rightmost,     // Last orb (Dualcast targets this one)
        All,           // Every currently-slotted orb
    }

    public enum PouchItemType
    {
        None,
        Shiv,           // Silent
        Soul,           // Necrobinder
        SovereignBlade, // Regent
        Generic,        // Arbitrary — references the itemSO field instead
    }

    public enum SealTargetKind
    {
        SelfSkill,     // This skill self-seals as a cost
        ChosenSkill,   // Player picks which skill to seal
        RandomSkill,   // Random from remaining
    }

    public enum ExtraActionKind
    {
        Haste,         // Extra action on target's NEXT turn
        Renew,         // Extra turn immediately after current turn ends
    }

    public enum CompanionTargetKind
    {
        Owner,         // The owner (Necrobinder herself)
        Osty,          // The Necrobinder's bound companion
        Any,           // Ally including companions
    }

    /// <summary>
    /// For effects that apply condition stacks, how is the stack count derived?
    ///   • Fixed — uses effect.conditionStacks verbatim
    ///   • UnblockedDamage — apply stacks equal to unblocked damage dealt this
    ///     effect (Blight Strike: "apply Doom equal to unblocked damage dealt")
    ///   • DamageDealt — total damage dealt this effect (pre-Shields)
    ///   • CasterPOW — caster's EffectivePOW
    ///   • TargetStacks — stacks of another condition on the target (e.g. Cursed scaling)
    /// </summary>
    public enum ConditionStackSource { Fixed, UnblockedDamage, DamageDealt, CasterPOW, TargetStacks }

    // Skills carry one or more mechanical-subsystem tags. These mirror the Tags
    // multi-select in the Notion Skills Database — Aspect Tree triggers, Forge
    // accumulation, Orb routing, WIL save resolution, etc. all key off these.
    // Flags so tags combine cheaply: `if ((skill.tags & SkillTag.OnHit) != 0)`.
    //
    // Nightsaint-only tags (HumanForm/WolfForm/Bleeding/Fervor) are reserved so
    // DLC Pack 1 skill assets parse cleanly but are not referenced at Launch.
    [System.Flags]
    public enum SkillTag
    {
        None          = 0,
        Strike        = 1 << 0,   // Counts as a Strike for Ironclad / Silent "first attack" triggers
        Defend        = 1 << 1,   // Counts as a Defend for Fortress-branch triggers
        Exhaust       = 1 << 2,   // One-use, removed from loadout for the combat after playing
        Forge         = 1 << 3,   // Contributes to Sovereign Blade (Regent)
        StarGenerator = 1 << 4,   // Grants Stars on play (Regent)
        OstyAttack    = 1 << 5,   // Routed through Osty (Necrobinder)
        Orb           = 1 << 6,   // Channels or interacts with Orbs (Defect)
        OnHit         = 1 << 7,   // Has an after-hit rider (apply condition, etc.)
        Positional    = 1 << 8,   // Moves the caster or target
        WilSave       = 1 << 9,   // Resolves via WIL save (see SkillDiceRule.WilSave)

        // Nightsaint (DLC Pack 1) — reserved, unused at Launch
        HumanForm     = 1 << 10,
        WolfForm      = 1 << 11,
        Bleeding      = 1 << 12,
        Fervor        = 1 << 13,
    }

    public enum ConditionID
    {
        Strength,
        Dexterity,
        Vulnerable,
        Weak,
        Frail,
        Guard,
        Plating,
        FlameBarrier,
        Blur,
        Artifact,
        Poison,
        Regen,
        Noxious,
        Thorns,
        Constricted,
        Stunned,
        Asleep,
        Entangled,
        Dodge,
        Focus,
        LockOn,
        Doom,
        Rupture,
        Intangible,
        Accuracy,
        Flanked,
        DieForYou,
        Debilitate,
        Oblivion,
        Barricade,
        Burst,
        Bruise,
        Cursed,
        Shields,     // Stackable damage absorb (1 per stack) — replaces temporaryDefense.
                     // clearTiming=RoundStart on the SO; Barricade (defensePersists) overrides.
    }

    public enum ConditionStackType { Counter, Duration, Single }

    public enum ConditionTiming { Passive, TurnStart, TurnEnd, Cleanup, OnHit, OnApply }

    // Retained from Echoes so ported files compile. Powers/Relics themselves
    // are NOT ported — this enum is dead data until Aspect Trees replace it.
    public enum RelicTrigger
    {
        OnCombatStart,
        OnCombatEnd,
        OnTurnStart,
        OnTurnEnd,
        OnPlayerAttack,
        OnPlayerSkillUsed,
        OnPlayerItemUsed,
        OnPlayerHit,
        OnEnemyDeath,
        OnConditionApplied,
        Passive
    }

    public enum RelicEffectType
    {
        Heal,
        ApplyCondition,
        ModifyStat,
        GenerateItem,
        RestoreSP,
        ChannelOrb,
        ReduceCooldowns,
        Custom
    }

    public enum EnemyIntentType { Attack, Guard, Buff, Debuff, Skill, Stunned, Unknown }

    /// <summary>
    /// Encounter-tier classification for enemies. Drives the visual treatment
    /// (HUD badge, name color) and gates encounter authoring (one Boss per
    /// fight, etc.). Pure stat scaling stays on EnemyData itself — designers
    /// pick the numbers per-enemy rather than blanket-multiplying by tier.
    /// </summary>
    public enum EnemyType
    {
        Normal,  // Standard mook. No badge.
        Elite,   // Tougher mid-floor encounter. Silver badge.
        Boss,    // Floor / act capper. Red badge.
    }

    /// <summary>
    /// How an enemy intent picks among in-range players. State-based heuristics
    /// (HP / max HP / WIL) compare across the eligible pool and pick the
    /// extremum; ties break randomly. Condition-based variants prefer or avoid
    /// targets carrying a specific condition.
    /// </summary>
    /// <summary>
    /// How an enemy chooses its next move from the authored move pattern.
    /// WeightedRandom rolls each turn against the per-move weight (default,
    /// good for variety). Cycle iterates the pattern in order and loops back
    /// at the end, ignoring weights — designed for telegraphed bosses or
    /// simple mooks with a fixed routine ("smash, smash, regroup, repeat").
    /// </summary>
    public enum EnemyMovePatternMode
    {
        WeightedRandom,
        Cycle,
    }

    public enum EnemyTargetPreference
    {
        Random,           // Legacy default — uniform random.
        LowestHP,         // Picks the wounded — finishers / opportunist mooks.
        HighestHP,        // Tank-busting — go for the meatiest target.
        LowestMaxHP,      // Squishies first.
        HighestMaxHP,     // Frontliners first.
        LowestWIL,        // Best afflict landing chance.
        HighestWIL,       // Off-meta — hunt the priest/mage.
        HasCondition,     // Prefer targets carrying preferredCondition (1+ stacks).
        LacksCondition,   // Prefer targets WITHOUT preferredCondition.
    }

    /// <summary>
    /// How a skill's attack-style roll is resolved. Drives SkillResolver's
    /// dispatch tree. Maps directly to the Notion Skills DB "Dice Rule" column.
    ///   • AttackRoll — classic d20 + ATK vs DEF; crit + crit-miss apply
    ///   • AutoHit    — no roll, effects always land (Brace, Heal, etc.)
    ///   • WilSave    — target rolls d20 + WIL vs (10 + caster WIL);
    ///                   on SUCCESS the whole effect block is resisted
    ///                   (one roll per target per cast, cached)
    ///   • Passive    — skill doesn't execute when Played. Intended to be
    ///                   event-subscription driven; for now SkillResolver
    ///                   logs a warning if a Passive skill is Played and
    ///                   returns empty results without spending SP
    /// </summary>
    public enum SkillDiceRule { AttackRoll, AutoHit, WilSave, Passive }

    /// <summary>
    /// Direction/flavor of skill-driven movement. Self-movement (Advance/Withdraw)
    /// always succeeds. Target-movement (Pull/Knockback/Shuffle) can be resisted
    /// by a WIL save on the target — set SkillEffectData.saveDC > 0 to gate the
    /// move on a save (target rolls d20 + WIL vs saveDC; success = resisted).
    /// Leaving saveDC = 0 means the movement auto-applies.
    ///   • Advance   N — user moves N ranks toward front (lower rank number)
    ///   • Withdraw  N — user moves N ranks toward back  (higher rank number)
    ///   • Pull      N — target moves N ranks toward the caster's side
    ///   • Knockback N — target moves N ranks away from the caster's side
    ///   • Shuffle     — target moves to a random rank
    /// </summary>
    public enum MovementKind { Advance, Withdraw, Pull, Knockback, Shuffle }

    public enum Alignment
    {
        Ironclad,
        Silent,
        Defect,
        Necrobinder,
        Regent,       // Launch roster 5th slot
        Nightsaint,   // DLC Pack 1 (reserved, unused at Launch)
        Party,
        Neutral,
    }

    public enum Rarity { Starter, Common, Uncommon, Rare, Shop, Legendary }

    public enum SaveType { None, Soul, Vigor }
}
