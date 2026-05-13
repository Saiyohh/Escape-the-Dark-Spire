using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewCondition", menuName = "DarkSpire/Condition")]
    public class ConditionData : ScriptableObject
    {
        public ConditionID conditionID;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public Color tintColor = Color.white;

        [Header("Icon Color Adjust")]
        [Tooltip("When true, renders the condition icon through the BlackAndWhite " +
                 "shader using the six channel weights below.")]
        public bool iconGrayscale = false;

        [Range(0f, 3f)] public float iconBwReds     = 0.40f;
        [Range(0f, 3f)] public float iconBwYellows  = 0.60f;
        [Range(0f, 3f)] public float iconBwGreens   = 0.40f;
        [Range(0f, 3f)] public float iconBwCyans    = 0.60f;
        [Range(0f, 3f)] public float iconBwBlues    = 0.20f;
        [Range(0f, 3f)] public float iconBwMagentas = 0.80f;

        public ConditionStackType stackType;
        public bool isDebuff;
        public bool ticksDown = true;
        public int maxStacks; // 0 = unlimited

        [Header("Passive Stat Modifiers (always-on while active)")]
        [Tooltip("Each entry applies amountPerStack × currentStacks to the stat. " +
                 "Sum across all conditions is read by Unit.Effective* properties.")]
        public PassiveModifier[] passiveModifiers = System.Array.Empty<PassiveModifier>();

        [Header("Triggers (event-driven)")]
        [Tooltip("Each trigger: WHEN (event) + IF (filters) + DO (actions) + STACK-OP. " +
                 "Dispatched by ConditionManager when the unit receives the matching event.")]
        public ConditionTrigger[] triggers = System.Array.Empty<ConditionTrigger>();

        [Header("Structural Flags")]
        [Tooltip("Skips this unit's turn entirely (Stunned).")]
        public bool preventsAction;

        [Tooltip("When this condition auto-clears. Critical for round-safe buffs:\n" +
                 "  • Never — persist until removed (Strength, Doom)\n" +
                 "  • OwnerTurnStart — at this unit's next turn (Guarding, per-turn buffs)\n" +
                 "  • RoundStart — at next round's start (Shields — protects whole round\n" +
                 "                 no matter who got it; persists through all turns)\n" +
                 "  • RoundEnd — at end of current round")]
        public ClearTiming clearTiming = ClearTiming.Never;

        [Tooltip("If any active condition on the unit has this, Shields are NOT cleared on schedule (Barricade).")]
        public bool defensePersists;

        [Tooltip("Damage from this condition goes directly to HP, skipping Shields absorb. " +
                 "Typical for DoTs: Poison, Burning, Doom.")]
        public bool bypassesShields;

        [Tooltip("Damage from this condition ignores DEF in any attack-roll context.")]
        public bool bypassesDEF;

        [HideInInspector] public int defModifier;
        [HideInInspector] public int damageModifier;
        [HideInInspector] public int tickDamage;
        [HideInInspector] public int tickHeal;
        [HideInInspector] public float dodgeChance;
        [HideInInspector] public int retaliationDamage;
        [HideInInspector] public bool ignoresDEF;          // renamed to bypassesDEF above
        [HideInInspector] public bool ignoresDefense;      // renamed to bypassesShields above
        [HideInInspector] public bool clearsAtTurnStart;   // superseded by clearTiming (OwnerTurnStart)
    }
}
