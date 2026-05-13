// ConditionData.cs
// -----------------------------------------------------------------------------
// ScriptableObject describing a single status effect. Compositional model:
//
//   [Identity]            ID, display name, description, icon, tint
//   [Stacking]            stackType, maxStacks, isDebuff
//   [Passive Modifiers]   Static stat bonuses (+POW per stack, etc.)
//   [Triggers]            Event-driven reactions with optional filters
//   [Structural Flags]    Behaviors that bypass normal combat flow
//                         (preventsAction, clearsAtTurnStart, defensePersists,
//                          bypassesShields, bypassesDEF)
//
// Runtime dispatch is handled by ConditionManager, which subscribes to combat
// events and walks each active trigger. Passive modifiers are read by
// Unit.Effective* properties through ConditionManager.GetPassiveModifier.
//
// See ConditionTrigger.cs for the trigger/action/conditional enum reference.
//
// Legacy fields (defModifier, damageModifier, tickDamage, tickHeal, dodgeChance,
// retaliationDamage) are deprecated in the new model but kept for backward
// compatibility during migration. New conditions should use passiveModifiers +
// triggers exclusively.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewCondition", menuName = "DarkSpire/Condition")]
    public class ConditionData : ScriptableObject
    {
        // ─── Identity ───────────────────────────────────────────────────────
        public ConditionID conditionID;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public Color tintColor = Color.white;

        // ─── Icon color adjust (per-hue B&W mixer) ─────────────────────────
        //
        // Same adjust used by SkillData.bannerGrayscale + channel weights —
        // mirrors Photoshop's Black & White tool via DarkSpire/UI/BlackAndWhite.
        // Useful for placeholder art that needs to fit the monochrome HUD
        // without a Photoshop pass. Swap per-condition to a hand-drawn icon
        // later and just flip iconGrayscale off.
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

        // ─── Stacking ──────────────────────────────────────────────────────
        public ConditionStackType stackType;
        public bool isDebuff;
        public bool ticksDown = true;
        public int maxStacks; // 0 = unlimited

        // ─── Passive modifiers (always-on stat bonuses) ────────────────────
        [Header("Passive Stat Modifiers (always-on while active)")]
        [Tooltip("Each entry applies amountPerStack × currentStacks to the stat. " +
                 "Sum across all conditions is read by Unit.Effective* properties.")]
        public PassiveModifier[] passiveModifiers = System.Array.Empty<PassiveModifier>();

        // ─── Triggers (event-driven reactions) ─────────────────────────────
        [Header("Triggers (event-driven)")]
        [Tooltip("Each trigger: WHEN (event) + IF (filters) + DO (actions) + STACK-OP. " +
                 "Dispatched by ConditionManager when the unit receives the matching event.")]
        public ConditionTrigger[] triggers = System.Array.Empty<ConditionTrigger>();

        // ─── Structural flags (bypass normal flow) ─────────────────────────
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

        // ─── Legacy fields (deprecated — kept for migration) ───────────────
        // New conditions should use passiveModifiers + triggers instead.
        // These are hidden in the new inspector but keep their serialized values
        // so existing SOs don't silently lose data during the refactor.
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
