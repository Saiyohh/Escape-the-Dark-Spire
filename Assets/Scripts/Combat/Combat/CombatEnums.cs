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

    public enum SkillEffectType
    {
        Attack,            // d20 + ATK vs DEF — hit deals magnitude damage. (Was "Damage".)
        DirectDamage,      // [LEGACY] Use Apply + ApplyKind.Damage. Kept for serialized compat.
        Heal,
        RestoreSP,
        ApplyCondition,    // [LEGACY] Use Apply + ApplyKind.Condition. Kept for serialized compat.
        RemoveCondition,
        Move,              // Positional: uses movementKind + movementMagnitude

        LoseHP,            // Self-damage as part of the effect block (Offering, Hemokinesis)

        Haste,             // Target gets an extra action on their NEXT turn
        Renew,             // Target takes an extra turn immediately after this one
        SealSkill,         // Put a skill on cooldown for N turns (self / chosen / random)

        GenerateItem,      // Shiv / Soul / SovereignBlade / etc. (uses pouchItemType)

        ChannelOrb,        // Channel N orbs of orbType (may use orbSource, orbMulti)
        EvokeOrb,          // Trigger an orb's Evoke (first/rightmost via evokeKind)

        SummonCompanion,   // Add HP to Osty (revive if dead); magnitude = HP
        BindCompanion,     // Bind Osty to a target ally for N turns

        GainStars,         // Add N Stars to the Regent's pool
        Forge,             // Forge N — bumps Sovereign Blade damage, generates into Pouch if absent

        Retaliate,         // "If an enemy attacks you, they take X damage" until TurnStart
        OnAllyAttackRider, // "Whenever ally attacks, gain X Guard" etc. until TurnStart

        Apply,             // No-roll application. Sub-kind (applyKind) chooses Damage or Condition.
        Afflict,           // WIL-save gated condition application. On FAILED save, condition lands.
    }

    public enum ApplyKind
    {
        Damage,    // Flat damage; uses magnitude.
        Condition, // Puts stacks of conditionID on the target; stacks via stackCountKind.
    }

    public enum DamageStat
    {
        None,   // No stat bonus — magnitude is final.
        POW,    // + caster POW (classic physical scaling).
        DEX,    // + caster DEX (finesse / precision).
        WIL,    // + caster WIL (spell / willpower scaling).
    }

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
        First,
        Leftmost,
        All,
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

    public enum ConditionStackSource { Fixed, UnblockedDamage, DamageDealt, CasterPOW, TargetStacks }

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
        Slippery,    // Caps the next N incoming damage instances to 1 (one stack consumed per hit).
        Shrink,      // -25% outgoing damage; auto-clears when the applying caster dies.
        Territorial, // OnTurnEnd: grants Strength per stack (Byrdonis ramp passive).
    }

    public enum ConditionStackType { Counter, Duration, Single }

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

    public enum EnemyType
    {
        Normal,  // Standard mook. No badge.
        Elite,   // Tougher mid-floor encounter. Silver badge.
        Boss,    // Floor / act capper. Red badge.
    }

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

    public enum SkillDiceRule { AttackRoll, AutoHit, WilSave, Passive }

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
