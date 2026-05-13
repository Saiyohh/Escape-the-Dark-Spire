using UnityEngine;
using UnityEngine.Serialization;

namespace DarkSpire
{
    [System.Serializable]
    public class SkillEffectData
    {
        // ─── Core ────────────────────────────────────────────────────────────
        public SkillEffectType effectType;

        [Tooltip("For effectType=Apply: what is being applied — flat damage or a condition.")]
        public ApplyKind applyKind = ApplyKind.Damage;

        [Tooltip("Gate this effect on the outcome of the most recent Attack / Afflict " +
                 "against the same target. Always = run unconditionally. OnHit = only " +
                 "run after a successful Attack roll. Etc.")]
        public ConditionalGate gate = ConditionalGate.Always;

        [Tooltip("When gated (gate != Always), pick which target the effect applies to:\n" +
                 "  true  — SAME target the gate was evaluated on (e.g. 'OnHit: Apply Weak' " +
                 "to the unit you just hit).\n" +
                 "  false — RE-ROLL: if the gate passed on any target, pick a new target " +
                 "via this effect's own targetMode (e.g. 'OnKill: Attack another enemy').")]
        public bool sameTargetAsGate = true;

        [Tooltip("Optional 'do-again' link. Index of an earlier effect in this skill's " +
                 "effects list — after this effect resolves successfully, execution jumps " +
                 "back to that index and continues. -1 = no loop. Useful for 'on kill, " +
                 "attack another enemy, and keep chaining' patterns. Capped at " +
                 "SkillResolver.MaxLoopIterations per skill cast to prevent runaway loops.")]
        public int loopLinkIndex = -1;

        public TargetMode targetMode;

        [Tooltip("For SingleEnemy / SingleAlly targetMode: which of the skill's " +
                 "targetPickCount picks this effect uses (0-based). All effects " +
                 "default to index 0 so multiple effects naturally share the " +
                 "same pick — author multi-pick distributions by giving later " +
                 "effects pickIndex = 1, 2, etc. up to targetPickCount - 1.")]
        [Min(0)] public int targetPickIndex = 0;

        [Tooltip("Damage / heal amount / shields gained / SP restored / HP lost — " +
                 "meaning depends on effectType. For condition application this is " +
                 "a fallback if conditionStacks is 0.")]
        public int magnitude;

        [Tooltip("For Attack / Apply+Damage / legacy DirectDamage — which caster " +
                 "stat adds to final damage. POW is the classic physical scaling. " +
                 "DEX = finesse, WIL = spell. None = no bonus, magnitude is final.")]
        public DamageStat damageStat = DamageStat.POW;

        [Tooltip("How many times an Attack effect strikes per resolve. Each hit is an " +
                 "independent attack roll (so a 3-hit attack can hit, miss, or crit on " +
                 "each strike independently). Damage accumulates into the same result. " +
                 "1 = single strike (default). Used by enemy multi-attack intents " +
                 "(\"3×6\") and any future multi-strike skills. Ignored for non-Attack " +
                 "effect types — duplicate the effect or use loopLinkIndex if you want " +
                 "a no-roll Apply to fire multiple times.")]
        [Min(1)] public int hitCount = 1;

        // ─── Condition fields (Apply+Condition / Afflict / RemoveCondition) ─
        [Tooltip("Which condition to apply or remove. Resolved at runtime via " +
                 "ConditionLibrary.Instance.Get(conditionID) — the library is the " +
                 "single source of truth for condition SOs. Duration/stackType " +
                 "come from the SO itself, not per-effect.")]
        public ConditionID conditionID;

        [Tooltip("Stack count when stackCountKind=Fixed, or the 'X' in 'X per Y <source>' " +
                 "when derived.")]
        public int conditionStacks;

        [Tooltip("Fixed = use conditionStacks directly. Derived kinds compute stacks as " +
                 "floor(sourceValue / stackPer) * conditionStacks, so 'Apply 1 Vulnerable " +
                 "per 2 damage dealt' uses conditionStacks=1, stackPer=2, PerDamageDealt.")]
        [FormerlySerializedAs("stackSource")]
        public ConditionStackSource stackCountKind = ConditionStackSource.Fixed;

        [Tooltip("The 'Y' in 'X per Y <source>'. Divides the source value to compute stacks. " +
                 "Unused when stackCountKind=Fixed. Minimum 1.")]
        [Min(1)] public int stackPer = 1;

        // ─── Per-effect save override (Afflict DC, resisted movement, etc.) ─
        [Tooltip("0 = no per-effect save. >0 = force a target WIL save vs this DC " +
                 "before the effect applies. Used by Afflict effects and by Move " +
                 "effects with Pull/Knockback/Shuffle. Ignored when the parent skill's " +
                 "diceRule is already WilSave (skill-level save takes precedence).")]
        public int saveDC;

        // ─── Movement (SkillEffectType.Move) ────────────────────────────────
        public MovementKind movementKind;
        [Min(1)] public int movementMagnitude = 1;

        // ─── Orb (Defect) — PLACEHOLDER ─────────────────────────────────────
        public OrbType orbType = OrbType.Lightning;
        [Min(1)] public int orbCount = 1;
        [Tooltip("Self = Defect channels. TargetAlly = an ally channels (Ignition). " +
                 "PerEnemy = one orb per enemy in combat (Chill).")]
        public OrbSource orbSource = OrbSource.Self;
        public EvokeKind evokeKind = EvokeKind.First;
        [Tooltip("How many times to fire the Evoke effect on the SAME targeted " +
                 "orb. Dualcast = 2 (evoke the rightmost orb, fire its effect " +
                 "twice, then consume once). Default 1 = standard Evoke.")]
        [Min(1)] public int evokeCount = 1;

        // ─── Companion / Osty (Necrobinder) — PLACEHOLDER ───────────────────
        public CompanionTargetKind companionTarget = CompanionTargetKind.Osty;
        [Tooltip("BindCompanion: how many turns Osty stays bound to the target ally.")]
        [Min(1)] public int bindDuration = 1;

        // ─── Item generation (Pouch) ────────────────────────────────────────
        public PouchItemType pouchItemType = PouchItemType.None;
        [Tooltip("Used when pouchItemType = Generic — references an ItemDataSO " +
                 "(to be added later; left null for now).")]
        public Object itemSO;
        [Min(1)] public int itemQuantity = 1;

        // ─── Resources (Stars / Forge — Regent) — PLACEHOLDER ───────────────
        [Tooltip("How many Stars / Forge stacks to grant. Which one is determined " +
                 "by effectType (GainStars vs Forge).")]
        [Min(0)] public int resourceAmount = 1;

        // ─── Skill manipulation (Seal) — PLACEHOLDER ────────────────────────
        public SealTargetKind sealTarget = SealTargetKind.SelfSkill;
        [Min(1)] public int sealDuration = 1;

        // ─── Action economy (Haste/Renew) ───────────────────────────────────
        public ExtraActionKind extraActionKind = ExtraActionKind.Haste;

        // ─── Triggered riders (Retaliate / OnAllyAttackRider) — PLACEHOLDER ──
        [Tooltip("How many turns the trigger persists. Flame Barrier is 1 " +
                 "(until start of next turn). Longer for sustained auras.")]
        [Min(1)] public int triggerDuration = 1;

        [Tooltip("Damage dealt by Retaliate when the trigger fires.")]
        [Min(0)] public int retaliateDamage = 0;

        // ─── Scaling (kept for forward-compat) ──────────────────────────────
        [HideInInspector] public float scalingMultiplier = 1f;
        [HideInInspector] public SaveType saveType;   // reserved — Dark Spire is WIL-only

        // ─── Category mapping (for inspector UI) ────────────────────────────
        public static SkillEffectCategory CategoryOf(SkillEffectType t) => t switch
        {
            SkillEffectType.Attack
                or SkillEffectType.Afflict
                or SkillEffectType.Apply
                or SkillEffectType.DirectDamage    // legacy
                or SkillEffectType.ApplyCondition  // legacy
                or SkillEffectType.Heal
                or SkillEffectType.RestoreSP
                or SkillEffectType.RemoveCondition
                or SkillEffectType.Move
                    => SkillEffectCategory.Core,
            SkillEffectType.LoseHP
                    => SkillEffectCategory.SelfCost,
            SkillEffectType.Haste or SkillEffectType.Renew
            or SkillEffectType.SealSkill
                    => SkillEffectCategory.ActionEconomy,
            SkillEffectType.GenerateItem
                    => SkillEffectCategory.Item,
            SkillEffectType.ChannelOrb or SkillEffectType.EvokeOrb
                    => SkillEffectCategory.Orb,
            SkillEffectType.SummonCompanion or SkillEffectType.BindCompanion
                    => SkillEffectCategory.Companion,
            SkillEffectType.GainStars or SkillEffectType.Forge
                    => SkillEffectCategory.Resource,
            SkillEffectType.Retaliate or SkillEffectType.OnAllyAttackRider
                    => SkillEffectCategory.Triggered,
            _ => SkillEffectCategory.Core,
        };

        public bool AppliesCondition =>
            effectType == SkillEffectType.Afflict
            || effectType == SkillEffectType.ApplyCondition
            || (effectType == SkillEffectType.Apply && applyKind == ApplyKind.Condition);
    }
}
