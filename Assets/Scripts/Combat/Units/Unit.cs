using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class Unit
    {
        public string unitName;
        public bool isPlayerControlled;
        public Sprite combatSprite;
        public Sprite headIcon;
        public Alignment alignment;
        public int level = 1;

        public int maxHP, maxSP;
        public int basePOW, baseATK, baseDEF, baseSPD, baseWIL;

        public int currentHP, currentSP;

        public int currentStars;

        public WeaponData equippedWeapon;
        public List<SkillData> equippedSkills = new();

        public ConditionManager conditions = new();

        private readonly DamageContext damageCtx = new();

        public bool hasActedThisTurn;
        public bool hasFreeActedThisTurn;
        public int initiativeRoll;

        public int currentRank = 1;

        public List<OrbInstance> orbs = new();
        public int orbSlotMax = 3;

        public CharacterData characterData;
        public EnemyData enemyData;
        private int actionCycleIndex;

        public PartyMemberRuntime partyMember;

        [System.NonSerialized] public EnemyMove currentMove;
        [System.NonSerialized] public Unit[] lockedIntentTargets;

        [System.NonSerialized] public HashSet<ConditionID> conditionsAppliedSinceLastMove = new();
        [System.NonSerialized] public HashSet<ConditionID> conditionsRemovedSinceLastMove = new();
        [System.NonSerialized] public HashSet<int> firedConditionalIndices = new();

        public event Action<int> OnDamageTaken;
        public event Action<int> OnHealReceived;
        public event Action<int> OnDefenseChanged;
        public event Action OnDeath;
        public event Action OnStatsChanged;
        public event Action<int, int> OnRankChanged;

        public event Action<Unit> OnOrbsChanged;

        public void RaiseOrbsChanged() => OnOrbsChanged?.Invoke(this);

        public event Action<Unit> OnStarsChanged;

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

        public Unit(EnemyData data)
        {
            conditions.Initialize(this);
            unitName = data.enemyName;
            isPlayerControlled = false;
            combatSprite = data.combatSprite;
            alignment = Alignment.Neutral;
            enemyData = data;

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

        public ActionRefusalReason GetBasicActionRefusal()
        {
            if (IsStunned) return ActionRefusalReason.Immobilized;
            if (hasActedThisTurn) return ActionRefusalReason.NoAction;
            return ActionRefusalReason.None;
        }

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
                    break;
            }
            return ActionRefusalReason.None;
        }

        public bool CanUseItem(ItemData item)
            => GetItemRefusal(item) == ActionRefusalReason.None;

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
