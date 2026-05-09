// Unit.cs
// -----------------------------------------------------------------------------
// Runtime combatant. One Unit per party member or enemy in the fight.
// Constructed from CharacterData (player) or EnemyData (enemy).
//
// Owns: HP/SP, equipped weapon, equipped skills, a ConditionManager for
// status effects, and events the UI layer subscribes to (OnDamageTaken,
// OnHealReceived, OnDefenseChanged, OnDeath).
//
// Combat stats are normalized to Dark Spire's 5-stat model internally:
//   • POW — damage bonus
//   • ATK — attack-roll bonus (d20 + ATK vs target DEF)
//           For players:  Unit.baseATK = CharacterData.dex
//           For enemies:  Unit.baseATK = EnemyData.atk
//   • DEF — attack-roll target
//   • SPD — initiative-roll bonus
//           For players:  Unit.baseSPD = CharacterData.dex  (DEX doubles)
//           For enemies:  Unit.baseSPD = EnemyData.spd
//   • WIL — save-roll bonus
//
// Effective* properties fold condition modifiers into base stats; damage math
// in SkillResolver + DamageCalculator reads those effective values.
//
// Pass 2 divergences still to apply here:
//   • add currentRank + rank-change event for the position system (Pass 2.B)
// -----------------------------------------------------------------------------
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
        // NOTE: temporaryDefense is gone. Shields live as a stackable condition
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
        /// <summary>(oldRank, newRank). Fired after currentRank is mutated.</summary>
        public event Action<int, int> OnRankChanged;

        /// <summary>Fired by OrbManager whenever orbs is mutated (Channel/Evoke/Clear).</summary>
        public event Action<Unit> OnOrbsChanged;

        /// <summary>Internal raise hook for OrbManager — kept on Unit so callers
        /// don't need to know the event signature.</summary>
        public void RaiseOrbsChanged() => OnOrbsChanged?.Invoke(this);

        /// <summary>Fired whenever currentStars changes (gain or spend).</summary>
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

        /// <summary>
        /// Save-roll bonus: DiceRoller.SaveRoll adds this to d20 vs the DC.
        /// Used by Afflict-style skills (SkillDiceRule.WilSave) and by
        /// resisted movement effects with saveDC > 0.
        /// </summary>
        public int EffectiveWIL => baseWIL;

        public bool IsAlive => currentHP > 0;
        public bool IsStunned => conditions.HasCondition(ConditionID.Stunned);
        public bool HasActionAvailable => !hasActedThisTurn;
        public bool HasFreeActionAvailable => !hasFreeActedThisTurn;

        /// <summary>
        /// Returns true if the unit has at least one Free-Action-cost skill
        /// that is currently usable (has SP). SP is the sole gate — no cooldowns.
        /// Does NOT check the item pouch (not implemented yet).
        /// </summary>
        public bool HasPlayableFreeAction()
        {
            if (hasFreeActedThisTurn) return false;
            foreach (var skill in equippedSkills)
            {
                if (skill.actionCostType == ActionCostType.FreeAction && CanUseSkill(skill))
                    return true;
            }
            // TODO: check pouch inventory when item system exists
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

        /// <summary>
        /// Wipe the conditional-move event sets. Called after an enemy acts so
        /// OnConditionApplied / OnConditionRemoved triggers fire exactly once
        /// per gap between move executions. Also called at combat start (after
        /// starting conditions seed) so opening intents start from a clean
        /// slate instead of latching onto self-applied seeds.
        /// </summary>
        public void ClearConditionalMoveEventState()
        {
            conditionsAppliedSinceLastMove.Clear();
            conditionsRemovedSinceLastMove.Clear();
        }

        /// <summary>
        /// Main damage entry point. Fires OnTakeDamagePre so triggers (Shields,
        /// Dodge, Thorns, Vulnerable, etc.) can modify the amount or negate
        /// entirely. Then applies whatever remains to HP, then fires OnTakeDamagePost.
        /// </summary>
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
                OnDeath?.Invoke();
        }

        public void TakeDirectDamage(int damage)
        {
            if (damage <= 0) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            OnDamageTaken?.Invoke(damage);
            if (currentHP <= 0)
                OnDeath?.Invoke();
            OnStatsChanged?.Invoke();
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

        /// <summary>
        /// Add Shield stacks to this unit (1 per stack = 1 dmg absorbed).
        /// Looks up the Shields ConditionData via ConditionLibrary.Instance —
        /// requires a ConditionData with conditionID = Shields registered in
        /// the library at Assets/Resources/ConditionLibrary.asset.
        /// </summary>
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
        // then resource costs. The UI uses this to surface a refusal bubble.
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
        // then action-state per the item's ActionCostType. Items have no SP
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

        /// <summary>
        /// Add Stars to the Regent's pool (or any future character with the
        /// Star resource). Generated by GainStars effects and Divine Right.
        /// No upper cap. Fires OnStarsChanged.
        /// </summary>
        public void GainStars(int amount)
        {
            if (amount <= 0) return;
            currentStars += amount;
            OnStarsChanged?.Invoke(this);
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// Deduct Stars (skill starCost on play). Floors at 0 — callers should
        /// use CanUseSkill to gate before spending. Fires OnStarsChanged.
        /// </summary>
        public void SpendStars(int amount)
        {
            if (amount <= 0) return;
            currentStars = Mathf.Max(0, currentStars - amount);
            OnStarsChanged?.Invoke(this);
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// Set the unit's rank directly. RankHelper.MoveUnit uses this to commit
        /// shifts. Clamped to [RankHelper.MinRank, RankHelper.MaxRank]; fires
        /// OnRankChanged when the value actually changes.
        /// </summary>
        public void SetRank(int newRank)
        {
            int clamped = Mathf.Clamp(newRank, RankHelper.MinRank, RankHelper.MaxRank);
            if (clamped == currentRank) return;
            int old = currentRank;
            currentRank = clamped;
            OnRankChanged?.Invoke(old, clamped);
        }

        /// <summary>
        /// Pick this enemy's next move from its authored pattern. Honors the
        /// EnemyData.movePatternMode setting:
        ///   • WeightedRandom — rolls each call against per-move weights.
        ///   • Cycle          — iterates the pattern in order, looping back
        ///                       to index 0 at the end. Weights are ignored.
        /// Returns null when the enemy has no authored moves.
        /// </summary>
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

        /// <summary>
        /// Walk the pattern in authored order, skipping null / unauthored
        /// entries, and bump <see cref="actionCycleIndex"/> for next time.
        /// Loops back to the start once we run off the end.
        /// </summary>
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
