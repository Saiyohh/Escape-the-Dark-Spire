using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "DarkSpire/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName;
        public Sprite combatSprite;
        [Tooltip("Encounter-tier classification. Drives the HUD badge above the " +
                 "HP bar (Elite = silver, Boss = red, Normal = no badge) and is " +
                 "available to encounter selection / reward systems.")]
        public EnemyType enemyType = EnemyType.Normal;

        [Tooltip("Optional dungeon-map override sprite. ONLY used when enemyType " +
                 "= Boss — the map peeks the floor's boss encounter and uses this " +
                 "icon in place of MapEntitySpriteLibrary.boss when set. Normal " +
                 "and Elite enemies always use the library default.")]
        public Sprite mapIcon;

        [Header("Display")]
        [Tooltip("Per-enemy UnitDisplay prefab with custom hitbox and intent anchor.\n" +
                 "If null, CombatManager falls back to its default prefab.\n" +
                 "Per-enemy intent positioning lives on the prefab's enemyIntentAnchorOffset, " +
                 "not on this asset.")]
        public GameObject displayPrefab;

        [Header("Stats")]
        [Tooltip("Lower bound (inclusive) of the random HP roll at combat start. " +
                 "Set to 0 (or > maxHP) to skip the roll and always spawn at maxHP.")]
        public int minHP;
        [Tooltip("Upper bound (inclusive) of the random HP roll, AND the value used " +
                 "when minHP is unset / invalid. Spawn HP = Random.Range(minHP, maxHP+1).")]
        public int maxHP;
        [Tooltip("Damage bonus added to this enemy's attack effects.")] public int pow;
        [Tooltip("Attack-roll bonus added to D20 when this enemy attacks.")] public int atk;
        [Tooltip("Attack-roll target; players need total ≥ DEF to hit.")] public int def;
        [Tooltip("Initiative-roll bonus; higher = moves sooner in turn order.")] public int spd;
        [Tooltip("Save-roll bonus for resisting player Afflict effects.")] public int wil;

        [Header("Sprite — Black & White Filter")]
        [Tooltip("When true, the combat sprite renders through the per-hue Black & " +
                 "White adjust (DarkSpire/UI/BlackAndWhite). Designer wires the source " +
                 "material on UnitDisplay; the runtime clones it per-enemy and pushes " +
                 "the channel weights below.")]
        public bool bwGrayscale;
        [Range(0f, 3f)] public float bwReds     = 0.40f;
        [Range(0f, 3f)] public float bwYellows  = 0.60f;
        [Range(0f, 3f)] public float bwGreens   = 0.40f;
        [Range(0f, 3f)] public float bwCyans    = 0.60f;
        [Range(0f, 3f)] public float bwBlues    = 0.20f;
        [Range(0f, 3f)] public float bwMagentas = 0.80f;

        [Header("Starting Conditions")]
        [Tooltip("Conditions applied to this enemy at combat start (before " +
                 "OnCombatStart triggers fire). Use to seed signature passives — " +
                 "e.g. Byrdonis spawns with Territorial 1, which then grants +1 " +
                 "Strength every turn through the Territorial condition's own " +
                 "trigger. Stacks > 0 are required; 0 entries are skipped.")]
        public EnemyStartingCondition[] startingConditions;

        [Header("AI Behavior")]
        [Tooltip("How the enemy picks its next move. WeightedRandom rolls each " +
                 "turn against the per-move weight (default — variety). Cycle " +
                 "iterates the pattern in order, looping back at the end, and " +
                 "ignores weights — for telegraphed bosses or simple mooks " +
                 "with a fixed routine.")]
        public EnemyMovePatternMode movePatternMode = EnemyMovePatternMode.WeightedRandom;

        [Tooltip("List of moves the enemy can choose each turn. Each move " +
                 "shows 1+ intent icons above the enemy's head. Selection is " +
                 "driven by movePatternMode above.")]
        public EnemyMove[] movePattern;

        [Tooltip("Reactive overrides that interrupt the move pattern when their " +
                 "trigger fires. Listed in priority order — first match wins. " +
                 "Use for boss phase shifts (HP < 50%), counter-moves when " +
                 "shields are stripped, scripted opening turns, etc.")]
        public EnemyConditionalMove[] conditionalMoves;

        [Header("Optional")]
        [Tooltip("Force this move on turn 1 instead of rolling the move pattern. " +
                 "Useful for telegraphing a wind-up or guaranteed opener.")]
        public bool hasTurn1MoveOverride;
        public EnemyMove turn1MoveOverride;

        [Tooltip("XP awarded to the party when this enemy is defeated.")]
        public int expValue;
    }

    // ──────────────────────────────────────────────────────────
    //  MOVE — one whole turn-action (1+ intents shown side-by-side)
    // ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class EnemyMove
    {
        [Tooltip("Move name — shown as intent tooltip header and in the combat log.")]
        public string name;

        [Tooltip("Relative weight for weighted-random selection. 0 = unpickable.")]
        [Min(0)] public int weight = 1;

        [Tooltip("All intents in this move, shown side-by-side in the HUD. " +
                 "Each intent is one icon (Attack / Buff / Debuff / Guard / ...) " +
                 "and one chain of effects.")]
        public EnemyIntent[] intents;
    }

    // ──────────────────────────────────────────────────────────
    //  INTENT — one icon above the head, one chain of effects
    // ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class EnemyIntent
    {
        [Tooltip("Picks the icon shown above the enemy's head for this intent.")]
        public EnemyIntentType intentType;

        [Header("Range")]
        [Tooltip("Minimum rank distance to the target for offensive intents " +
                 "(Attack/Debuff). Distance = casterRank + targetRank - 1, so a " +
                 "rank-1 enemy hitting a rank-1 player has distance 1 (melee). " +
                 "Default 1, max 4 — i.e. \"can hit anyone\" by default; tighten " +
                 "to 1..1 for explicit melee or 3..4 for a back-line-only spell.")]
        [Min(1)] public int rangeMin = 1;
        [Tooltip("Maximum rank distance. Defaults to 4 so a fresh intent can " +
                 "hit any player; lower it for melee (1) or strict reach windows.")]
        [Min(1)] public int rangeMax = 4;

        [Header("Target Preference (offensive intents)")]
        [Tooltip("How this intent picks among players in range. Random = legacy " +
                 "behavior. LowestHP / LowestWIL etc. let designers express AI " +
                 "personality (a wolf goes for the wounded; a witch tags the " +
                 "low-WIL caster). Self-targeting effects ignore this.")]
        public EnemyTargetPreference targetPreference = EnemyTargetPreference.Random;

        [Tooltip("ConditionID consulted when targetPreference is HasCondition / " +
                 "LacksCondition. Ignored otherwise.")]
        public ConditionID preferredCondition;

        [Header("Effects")]
        [Tooltip("Chain of effects this intent runs when the enemy acts. " +
                 "Same effect data structure skills use — supports gates " +
                 "(OnHit/OnMiss/OnKill/...), loop links, derived condition " +
                 "stacks, per-effect save DCs, etc.")]
        public SkillEffectData[] effects;
    }

    // ──────────────────────────────────────────────────────────
    //  STARTING CONDITION — auto-applied at combat start
    // ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class EnemyStartingCondition
    {
        public ConditionID conditionId;
        [Min(1)] public int stacks = 1;
    }

    // ──────────────────────────────────────────────────────────
    //  CONDITIONAL MOVES — interrupts that override the move pattern
    // ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class EnemyConditionalMove
    {
        [Tooltip("Designer-friendly identifier. Shown in the inspector header so " +
                 "long lists stay scannable. No runtime meaning.")]
        public string label;

        public EnemyConditionalTrigger trigger;

        [Tooltip("HP percent threshold for HPBelowPercent / HPAbovePercent " +
                 "triggers. 0.5 = 50%. Ignored otherwise.")]
        [Range(0f, 1f)] public float percent = 0.5f;

        [Tooltip("Turn number for OnTurnNumber. Ignored otherwise.")]
        [Min(1)] public int turnNumber = 1;

        [Tooltip("Condition consulted by OnConditionApplied / OnConditionRemoved / " +
                 "OnConditionAtStacks. Ignored otherwise.")]
        public ConditionID conditionId;

        [Tooltip("Stack threshold for OnConditionAtStacks. Ignored otherwise.")]
        [Min(1)] public int stacks = 1;

        [Tooltip("Override move used when the trigger fires. Required.")]
        public EnemyMove move;

        [Tooltip("When true, this trigger fires only once per combat. After firing " +
                 "it's latched and won't re-fire even if the condition stays true. " +
                 "When false, fires every turn the condition is true.")]
        public bool oncePerCombat = true;
    }

    public enum EnemyConditionalTrigger
    {
        HPBelowPercent,         // unit.currentHP / unit.maxHP < percent
        HPAbovePercent,         // strictly above
        OnTurnNumber,           // CombatManager.TurnNumber == turnNumber
        OnConditionApplied,     // conditionId was applied to this unit since last move-pick
        OnConditionRemoved,     // conditionId was removed since last move-pick (e.g. Shields stripped)
        OnConditionAtStacks,    // unit.conditions.GetStacks(conditionId) >= stacks
        AnyTargetLacksCondition,// at least one living player has zero stacks of conditionId — for re-applier mooks
    }
}
