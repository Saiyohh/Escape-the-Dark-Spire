using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class Unit
    {
        // Identity
        public string unitName;
        public bool isPlayerControlled;
        public Sprite combatSprite;
        public Sprite headIcon;
        public Alignment alignment;
        public int level = 1;

        // Base stats
        public int maxHP, maxSP;
        public int basePOW, baseATK, baseDEF, baseSPD, baseWIL;

        // Runtime stats
        public int currentHP, currentSP;

        // Regent-only spendable resource (gated by characterData.hasStarSystem
        // implicitly — non-Regent characters always have starCost = 0 on their
        // skills so this just sits at 0 for the rest of the cast). Initialized
        // to characterData.startingStars at combat start (Divine Right = 3 for
        // the Regent). No upper cap.
        public int currentStars;
        // (ConditionID.Shields) on the ConditionManager. Query via
        // conditions.GetShields(); the TakeDamage path auto-consumes them.

        // Loadout (player only)
        public WeaponData equippedWeapon;
        public List<SkillData> equippedSkills = new();

        // Conditions — initialized in both constructors so Owner is always set.
        public ConditionManager conditions = new();

        // Reused context objects to avoid per-damage allocation.
        private readonly DamageContext damageCtx = new();

        // Turn state
        public bool hasActedThisTurn;
        public bool hasFreeActedThisTurn;
        public int initiativeRoll;

        // Position — rank 1 = front, rank 4 = back. Set by CombatManager at spawn
        // and updated by RankHelper when the unit moves (or is moved via
        // Pull/Knockback/Shuffle). UnitDisplay subscribes to OnRankChanged so
        // the sprite slides to the new spawn position transform.
        public int currentRank = 1;

        // Orb queue (Defect only, gated by characterData.hasOrbSystem). Populated
        // by OrbManager.Channel; drained by OrbManager.Evoke* and OnTurnEnd.
        // Slot 0 is the leftmost / next-to-evoke per the canonical GDD.
        public List<OrbInstance> orbs = new();
        public int orbSlotMax = 3;

        // Source data (for per-unit prefab lookup, etc.)
        public CharacterData characterData;
        public EnemyData enemyData;
        private int actionCycleIndex;

        // Persistent run-state record for this party member (HP/SP/pouch
        // items carry across scene swaps). Set by CombatBootstrap during
        // hydration; null on enemies and on the editor-test path where
        // RunContext.partyState isn't populated. The Inventory helper uses
        // this to find the owner's pouch in O(1).
        public PartyMemberRuntime partyMember;

        // ── Telegraphed enemy move (enemies only) ──────────────────────────
        // Set by CombatManager.SetEnemyIntent at combat start and after every
        // enemy turn. `currentMove` is the move the enemy has committed to
        // execute on their next action; `lockedIntentTargets` is the per-intent
        // primary target picked AT INTENT-SET TIME, indexed parallel to
        // `currentMove.intents`. Both preview (DangerPreviewController /
        // IntentTargetResolver) and resolution (SkillResolver.ResolveEnemyMove)
        // read from this cache so they agree — fresh Random.Range rolls only
        // happen once per intent-set, never per-hover or per-resolve.
        [System.NonSerialized] public EnemyMove currentMove;
        [System.NonSerialized] public Unit[] lockedIntentTargets;

        // ── Conditional-move state (enemies only) ─────────────────────────
        // Tracks event-style triggers between AI move-picks. Populated by the
        // ConditionManager event subscriptions (OnConditionApplied/Removed)
        // wired in the EnemyData constructor; cleared after each move
        // execution so triggers fire exactly once per gap between enemy
        // actions. firedConditionalIndices latches once-per-combat triggers.
        [System.NonSerialized] public HashSet<ConditionID> conditionsAppliedSinceLastMove = new();
        [System.NonSerialized] public HashSet<ConditionID> conditionsRemovedSinceLastMove = new();
        [System.NonSerialized] public HashSet<int> firedConditionalIndices = new();

        // Events
        public event Action<int> OnDamageTaken;
        public event Action<int> OnHealReceived;
        public event Action<int> OnDefenseChanged;
        public event Action OnDeath;
        public event Action OnStatsChanged;
        public event Action<int, int> OnRankChanged;

        public event Action<Unit> OnOrbsChanged;

        public void RaiseOrbsChanged() => OnOrbsChanged?.Invoke(this);

        public event Action<Unit> OnStarsChanged;

        // Computed properties
        //
        // Each Effective* reads base + the sum of passive modifiers on active
        // conditions with the matching StatKind. Strength/Weak on POW, Guarding
        // on DEF, Dexterity on SPD+DEF, etc. — all driven by PassiveModifier
        // entries on the condition SOs. No hard-coded condition lookups here.

        public int EffectiveDEF =>
            Mathf.Max(0, baseDEF + Mathf.RoundToInt(conditions.GetPassiveModifier(StatKind.DEF)));

        public int EffectiveATK =>
            baseATK + (equippedWeapon != null ? equippedWeapon.attackBonus : 0);

        public int EffectivePOW =>
            basePOW + Mathf.RoundToInt(conditions.GetPassiveModifier(StatKind.POW));

        public int EffectiveSPD =>
            Mathf.Max(0, baseSPD + Mathf.RoundToInt(conditions.GetPassiveModifier(StatKind.SPD)));

        public int EffectiveDEX =>
            Mathf.RoundToInt(conditions.GetPassiveModifier(StatKind.DEX));

        public int EffectiveWIL => baseWIL;

        public bool IsAlive => currentHP > 0;
        public bool IsStunned => conditions.HasCondition(ConditionID.Stunned);
        public bool HasActionAvailable => !hasActedThisTurn;
        public bool HasFreeActionAvailable => !hasFreeActedThisTurn;

        public bool HasPlayableFreeAction()
        {
            if (hasFreeActedThisTurn) return false;
            foreach (var skill in equippedSkills)
            {
                if (skill.actionCostType == ActionCostType.FreeAction && CanUseSkill(skill))
                    return true;
            }
            return false;
        }

        // Constructor from CharacterData (player)
        public Unit(CharacterData data)
        {
            conditions.Initialize(this);
            unitName = data.characterName;
            isPlayerControlled = true;
            characterData = data;
            combatSprite = data.combatSprite;
            headIcon = data.headIcon;
            alignment = data.alignment;
            level = data.startingLevel;

            maxHP = data.maxHP;
            maxSP = data.maxSP;
            basePOW = data.pow;
            baseATK = data.dex;     // DEX feeds attack-roll bonus
            baseDEF = data.def;
            baseSPD = data.dex;     // DEX also feeds initiative for players
            baseWIL = data.wil;

            currentHP = maxHP;
            currentSP = maxSP;

            equippedWeapon = data.startingWeapon;
            if (data.startingSkills != null)
            {
                foreach (var skill in data.startingSkills)
                {
                    if (skill != null)
                        equippedSkills.Add(skill);
                }
            }
        }

        // Constructor from EnemyData (enemy)
        public Unit(EnemyData data)
        {
            conditions.Initialize(this);
            unitName = data.enemyName;
            isPlayerControlled = false;
            combatSprite = data.combatSprite;
            alignment = Alignment.Neutral;
            enemyData = data;

            // Roll a random HP in [minHP, maxHP] inclusive when the range is
            // valid. minHP <= 0 or minHP > maxHP means "no variance" — spawn
            // exactly at maxHP. Lets designers give an enemy a stat bracket
            // (e.g. "Nibbit: 38–46 HP") for combat-to-combat variation.
            int rolledMaxHP = data.maxHP;
            if (data.minHP > 0 && data.minHP < data.maxHP)
                rolledMaxHP = UnityEngine.Random.Range(data.minHP, data.maxHP + 1);
            maxHP = rolledMaxHP;
            maxSP = 0;
            basePOW = data.pow;
            baseATK = data.atk;
            baseDEF = data.def;
            baseSPD = data.spd;
            baseWIL = data.wil;

            currentHP = maxHP;
            currentSP = 0;

            // Track condition-application events for conditional-move triggers
            // (OnConditionApplied / OnConditionRemoved). Sets are cleared after
            // each move execution; the unit's firedConditionalIndices latches
            // once-per-combat overrides.
            conditions.OnConditionApplied += (id, _) => conditionsAppliedSinceLastMove.Add(id);
            conditions.OnConditionRemoved += id     => conditionsRemovedSinceLastMove.Add(id);
        }

        public void ClearConditionalMoveEventState()
        {
            conditionsAppliedSinceLastMove.Clear();
            conditionsRemovedSinceLastMove.Clear();
        }

        public void TakeDamage(int rawDamage, Unit source = null)
        {
            if (rawDamage <= 0) return;

            damageCtx.Reset(source, this, rawDamage);
            conditions.FireTakeDamagePre(damageCtx);

            if (damageCtx.negated || damageCtx.amount <= 0)
            {
                // Dodge/Artifact/full absorb — fire Post so reactive triggers
                // (e.g. "on dodge, gain X") still get their moment.
                conditions.FireTakeDamagePost(damageCtx);
                OnDefenseChanged?.Invoke(conditions.GetShields());
                OnStatsChanged?.Invoke();
                return;
            }

            int final = damageCtx.amount;
            currentHP = Mathf.Max(0, currentHP - final);
            OnDamageTaken?.Invoke(final);
            OnDefenseChanged?.Invoke(conditions.GetShields());
            OnStatsChanged?.Invoke();

            conditions.FireTakeDamagePost(damageCtx);

            if (currentHP <= 0)
                HandleDeath();
        }

        public void TakeDirectDamage(int damage)
        {
            if (damage <= 0) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            OnDamageTaken?.Invoke(damage);
            if (currentHP <= 0)
                HandleDeath();
            OnStatsChanged?.Invoke();
        }

        // Single death-edge entry point: strip every caster-gated condition
        // (Shrink, etc.) sourced by this unit from everyone on the field,
        // see the conditions already gone.
        private void HandleDeath()
        {
            var combat = CombatManager.Instance;
            if (combat != null)
            {
                foreach (var p in combat.PlayerUnits)
                    p?.conditions?.RemoveConditionsFromSource(this);
                foreach (var e in combat.EnemyUnits)
                    e?.conditions?.RemoveConditionsFromSource(this);
            }
            OnDeath?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            int actual = Mathf.Min(amount, maxHP - currentHP);
            currentHP += actual;
            if (actual > 0)
            {
                OnHealReceived?.Invoke(actual);
                OnStatsChanged?.Invoke();
            }
        }

        public void GainDefense(int amount)
        {
            if (amount <= 0) return;
            var lib = ConditionLibrary.Instance;
            var shieldsSO = lib != null ? lib.Get(ConditionID.Shields) : null;
            if (shieldsSO == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[Unit] GainDefense: no ConditionData with conditionID=Shields " +
                    "in ConditionLibrary. Create a Condition_Shields.asset and " +
                    "refresh the library (DarkSpire → Conditions → Refresh Library).");
                return;
            }
            conditions.ApplyCondition(shieldsSO, amount);
            OnDefenseChanged?.Invoke(conditions.GetShields());
            OnStatsChanged?.Invoke();
        }

        public void ResetTurnState()
        {
            hasActedThisTurn = false;
            hasFreeActedThisTurn = false;
        }

        // Why this skill can't be played right now, or None if it's legal.
        // Order: Immobilized first (overrides everything), then action-state,
        public ActionRefusalReason GetSkillRefusal(SkillData skill)
        {
            if (skill == null) return ActionRefusalReason.None;
            if (IsStunned) return ActionRefusalReason.Immobilized;
            if (skill.actionCostType == ActionCostType.Action     && hasActedThisTurn)
                return ActionRefusalReason.NoAction;
            if (skill.actionCostType == ActionCostType.FreeAction && hasFreeActedThisTurn)
                return ActionRefusalReason.NoFreeAction;
            if (currentSP    < skill.spCost)    return ActionRefusalReason.NotEnoughSP;
            if (currentStars < skill.starCost)  return ActionRefusalReason.NotEnoughStars;
            return ActionRefusalReason.None;
        }

        // Refusal for the basic action bar (Attack / Guard / Skill button /
        // Advance / Withdraw). All consume the unit's Action and are blocked
        // by Immobilized.
        public ActionRefusalReason GetBasicActionRefusal()
        {
            if (IsStunned) return ActionRefusalReason.Immobilized;
            if (hasActedThisTurn) return ActionRefusalReason.NoAction;
            return ActionRefusalReason.None;
        }

        // Why this item can't be used right now, or None if it's legal.
        // Mirrors GetSkillRefusal: Immobilized first (overrides everything),
        // or Star cost — consumed-on-use is the cost. ZeroCost bypasses both
        // action and free-action gates.
        public ActionRefusalReason GetItemRefusal(ItemData item)
        {
            if (item == null) return ActionRefusalReason.None;
            if (IsStunned) return ActionRefusalReason.Immobilized;

            switch (item.actionCostType)
            {
                case ItemActionCostType.Action:
                    if (hasActedThisTurn) return ActionRefusalReason.NoAction;
                    break;
                case ItemActionCostType.FreeAction:
                    if (hasFreeActedThisTurn) return ActionRefusalReason.NoFreeAction;
                    break;
                case ItemActionCostType.ZeroCost:
                    // No gate — usable any number of times per turn.
                    break;
            }
            return ActionRefusalReason.None;
        }

        public bool CanUseItem(ItemData item)
            => GetItemRefusal(item) == ActionRefusalReason.None;

        // Convenience wrapper. Now includes action-state and Immobilized
        // gates (matches what GetSkillRefusal returns). Existing callers —
        // HasPlayableFreeAction (above), SkillCardUI.Bind, and any AI/auto
        // resolve paths — all benefit from the consistent gate.
        public bool CanUseSkill(SkillData skill)
            => GetSkillRefusal(skill) == ActionRefusalReason.None;

        public void SpendSP(int amount)
        {
            currentSP = Mathf.Max(0, currentSP - amount);
            OnStatsChanged?.Invoke();
        }

        public void GainStars(int amount)
        {
            if (amount <= 0) return;
            currentStars += amount;
            OnStarsChanged?.Invoke(this);
            OnStatsChanged?.Invoke();
        }

        public void SpendStars(int amount)
        {
            if (amount <= 0) return;
            currentStars = Mathf.Max(0, currentStars - amount);
            OnStarsChanged?.Invoke(this);
            OnStatsChanged?.Invoke();
        }

        public void SetRank(int newRank)
        {
            int clamped = Mathf.Clamp(newRank, RankHelper.MinRank, RankHelper.MaxRank);
            if (clamped == currentRank) return;
            int old = currentRank;
            currentRank = clamped;
            OnRankChanged?.Invoke(old, clamped);
        }

        public EnemyMove GetNextEnemyMove()
        {
            if (enemyData == null) return null;
            var pattern = enemyData.movePattern;
            if (pattern == null || pattern.Length == 0) return null;

            switch (enemyData.movePatternMode)
            {
                case EnemyMovePatternMode.Cycle:
                    return NextCyclicMove(pattern);
                default:
                    return WeightedRandom(pattern);
            }
        }

        private EnemyMove NextCyclicMove(EnemyMove[] pattern)
        {
            int len = pattern.Length;
            // Try up to len entries from the current index, looping around,
            // so a pattern with one valid + several null entries still works.
            for (int step = 0; step < len; step++)
            {
                int idx = ((actionCycleIndex + step) % len + len) % len;
                var pick = pattern[idx];
                if (pick == null) continue;
                actionCycleIndex = ((idx + 1) % len + len) % len;
                return pick;
            }
            return null;
        }

        private static EnemyMove WeightedRandom(EnemyMove[] moves)
        {
            if (moves == null || moves.Length == 0) return null;

            int totalWeight = 0;
            for (int i = 0; i < moves.Length; i++)
                if (moves[i] != null) totalWeight += Mathf.Max(0, moves[i].weight);

            // All zero / null entries — fall back to the first non-null move.
            if (totalWeight <= 0)
            {
                for (int i = 0; i < moves.Length; i++)
                    if (moves[i] != null) return moves[i];
                return null;
            }

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] == null) continue;
                cumulative += Mathf.Max(0, moves[i].weight);
                if (roll < cumulative) return moves[i];
            }

            return moves[0];
        }
    }
}
