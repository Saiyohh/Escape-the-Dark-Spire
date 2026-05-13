using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class SkillResolver
    {
        public static CombatActionResult ResolveWeaponAttack(Unit attacker, Unit target)
        {
            var result = new CombatActionResult
            {
                source = attacker,
                target = target,
                actionType = ActionType.Attack,
                actionName = attacker.equippedWeapon != null ? attacker.equippedWeapon.weaponName : "Attack"
            };

            var weapon = attacker.equippedWeapon;
            int baseDmg = weapon != null ? weapon.baseDamage : 1;
            int critThreshold = weapon != null ? weapon.critThreshold : 20;
            int hitCount = weapon != null ? weapon.hitCount : 1;

            int totalDamage = 0;

            for (int i = 0; i < hitCount; i++)
            {
                var (hit, crit, rawRoll, totalRoll) = DiceRoller.AttackRoll(
                    attacker.EffectiveATK, target.EffectiveDEF, critThreshold);

                bool critMiss = rawRoll == 1;
                if (critMiss)
                {
                    hit = false;
                    attacker.TakeDirectDamage(1);
                }

                if (i == 0)
                {
                    result.didRoll = true;
                    result.rawD20Roll = rawRoll;
                    result.totalAttackRoll = totalRoll;
                    result.targetDEF = target.EffectiveDEF;
                    result.didHit = hit;
                    result.wasCrit = crit;
                    result.wasCritMiss = critMiss;
                }

                if (hit)
                {
                    float dodgeChance = target.conditions.GetDodgeChance();
                    if (!crit && dodgeChance > 0 && Random.value < dodgeChance)
                    {
                        target.conditions.ConsumeDodge();
                        result.wasDodged = true;
                        continue;
                    }

                    int damage = DamageCalculator.CalculateDamage(baseDmg, attacker, target, crit);
                    damage = DamageCalculator.ApplyOutgoingTriggers(damage, attacker, target, crit, didHit: true);
                    target.TakeDamage(damage, attacker);
                    totalDamage += damage;

                    if (weapon != null && weapon.hasOnHit
                        && Random.value <= weapon.onHitConditionChance)
                    {
                        var lib = ConditionLibrary.Instance;
                        var condData = lib != null ? lib.Get(weapon.onHitConditionID) : null;
                        if (condData != null && weapon.onHitConditionStacks > 0)
                        {
                            target.conditions.ApplyCondition(condData, weapon.onHitConditionStacks);
                            result.conditionsApplied.Add((weapon.onHitConditionID, weapon.onHitConditionStacks));
                        }
                    }
                }
            }

            result.damageDealt = totalDamage;
            return result;
        }

        public static CombatActionResult ResolveGuard(Unit unit, ConditionData guardingCondition)
        {
            var result = new CombatActionResult
            {
                source = unit,
                target = unit,
                actionType = ActionType.Guard,
                actionName = "Guard"
            };

            if (guardingCondition != null)
            {
                unit.conditions.ApplyCondition(guardingCondition, 1);
                result.conditionsApplied.Add((ConditionID.Guard, 1));
            }

            return result;
        }

        public static List<CombatActionResult> ResolveSkill(
            Unit caster, SkillData skill, List<Unit> targets,
            List<Unit> allPlayerUnits, List<Unit> allEnemyUnits,
            ConditionData[] conditionLookup,
            ActionType actionType = ActionType.Skill,
            bool fireSkillPlayed = true)
        {
            var results = new List<CombatActionResult>();

            if (skill.diceRule == SkillDiceRule.Passive)
            {
                Debug.LogWarning(
                    $"[SkillResolver] Passive skill '{skill.skillName}' was Played, " +
                    "but Passive event-subscription execution isn't implemented yet. " +
                    "No SP spent, no effects resolved.");
                return results;
            }

            caster.SpendSP(skill.spCost);

            if (skill.starCost > 0)
                caster.SpendStars(skill.starCost);

            if (skill.altCostType == AltCostType.HP && skill.altCostAmount > 0)
                caster.TakeDirectDamage(skill.altCostAmount);

            if (fireSkillPlayed)
            {
                caster.conditions.FireSkillPlayed(new SkillPlayedContext
                {
                    caster = caster,
                    skill = skill,
                    targets = targets,
                });
            }

            Dictionary<Unit, SaveRollOutcome> saveCache =
                skill.diceRule == SkillDiceRule.WilSave
                    ? new Dictionary<Unit, SaveRollOutcome>()
                    : null;

            var gateState = new Dictionary<Unit, GateState>();

            int effectIdx = 0;
            int loopIters = 0;
            while (effectIdx < skill.effects.Length && loopIters <= MaxLoopIterations)
            {
                var effect = skill.effects[effectIdx];
                if (effect == null) { effectIdx++; continue; }

                if (effect.effectType == SkillEffectType.Attack
                 || effect.effectType == SkillEffectType.Afflict)
                {
                    gateState.Clear();
                }

                List<Unit> effectTargets = BuildEffectTargets(
                    effect, caster, targets, allPlayerUnits, allEnemyUnits, gateState);

                int producedBefore = results.Count;

                foreach (var target in effectTargets)
                {
                    if (target == null || !target.IsAlive) continue;

                    if (effect.gate != ConditionalGate.Always && effect.sameTargetAsGate)
                    {
                    }
                    else if (effect.gate != ConditionalGate.Always)
                    {
                        if (!AnyTargetPassesGate(gateState, effect.gate)) continue;
                    }

                    SaveRollOutcome? save = null;
                    bool resisted = false;
                    if (saveCache != null)
                    {
                        if (!saveCache.TryGetValue(target, out var existing))
                        {
                            int dc = effect.saveDC > 0 ? effect.saveDC : 10 + caster.EffectiveWIL;
                            var (success, raw, total) = DiceRoller.SaveRoll(
                                target.EffectiveWIL, caster.EffectiveWIL, dc);
                            existing = new SaveRollOutcome(success, raw, total, dc);
                            saveCache[target] = existing;
                        }
                        save = existing;
                        resisted = existing.success;
                    }

                    CombatActionResult result;
                    if (resisted)
                    {
                        result = new CombatActionResult
                        {
                            source = caster,
                            target = target,
                            actionType = actionType,
                            actionName = skill.skillName,
                        };
                    }
                    else
                    {
                        result = ResolveSingleEffect(
                            caster, skill.skillName, actionType,
                            useAttackRoll: skill.diceRule == SkillDiceRule.AttackRoll,
                            skillIsWilSave: skill.diceRule == SkillDiceRule.WilSave,
                            effect, target);
                    }

                    if (save.HasValue)
                    {
                        var s = save.Value;
                        result.didSave = true;
                        result.saveSucceeded = s.success;
                        result.saveRawD20 = s.rawD20;
                        result.saveTotal = s.total;
                        result.saveDC = s.dc;
                    }

                    UpdateGateState(gateState, target, effect, result);

                    result.spSpent = skill.spCost;
                    results.Add(result);
                }

                bool producedResult = results.Count > producedBefore;

                if (producedResult
                    && effect.loopLinkIndex >= 0
                    && effect.loopLinkIndex < skill.effects.Length)
                {
                    loopIters++;
                    effectIdx = effect.loopLinkIndex;
                }
                else
                {
                    effectIdx++;
                }
            }

            if (loopIters > MaxLoopIterations)
            {
                Debug.LogWarning(
                    $"[SkillResolver] Skill '{skill.skillName}' hit the loop cap " +
                    $"of {MaxLoopIterations} iterations. Check loopLinkIndex authoring.");
            }

            return results;
        }

        public const int MaxLoopIterations = 16;

        public static List<CombatActionResult> ResolveItem(
            Unit user, ItemData item, List<Unit> targets,
            List<Unit> allPlayerUnits, List<Unit> allEnemyUnits)
        {
            if (item == null) return new List<CombatActionResult>();

            var wrapper = ScriptableObject.CreateInstance<SkillData>();
            wrapper.skillName        = item.itemName;
            wrapper.spCost           = 0;
            wrapper.starCost         = 0;
            wrapper.altCostType      = AltCostType.None;
            wrapper.altCostAmount    = 0;
            wrapper.diceRule         = item.diceRule;
            wrapper.primaryTargetMode = item.primaryTargetMode;
            wrapper.targetPickCount  = Mathf.Max(1, item.targetPickCount);
            wrapper.effects          = item.effects;
            wrapper.rangeMin         = 0;
            wrapper.rangeMax         = 0;

            try
            {
                return ResolveSkill(
                    user, wrapper, targets,
                    allPlayerUnits, allEnemyUnits,
                    conditionLookup: null,
                    actionType: ActionType.Item,
                    fireSkillPlayed: false);
            }
            finally
            {
                Object.Destroy(wrapper);
            }
        }

        private static List<Unit> BuildEffectTargets(
            SkillEffectData effect, Unit caster,
            List<Unit> selectedTargets, List<Unit> allPlayers, List<Unit> allEnemies,
            Dictionary<Unit, GateState> gateState)
        {
            if (effect.gate != ConditionalGate.Always && effect.sameTargetAsGate)
            {
                var list = new List<Unit>();
                foreach (var kvp in gateState)
                {
                    if (kvp.Key == null) continue;
                    if (PassesGate(effect.gate, kvp.Value))
                        list.Add(kvp.Key);
                }
                return list;
            }

            return ResolveTargets(effect.targetMode, caster, selectedTargets, allPlayers, allEnemies,
                pickIndex: effect.targetPickIndex);
        }

        private static bool AnyTargetPassesGate(Dictionary<Unit, GateState> gateState, ConditionalGate gate)
        {
            foreach (var kvp in gateState)
                if (PassesGate(gate, kvp.Value)) return true;
            return false;
        }

        private struct GateState
        {
            public bool didAttack;    // true once an Attack effect has targeted this unit
            public bool hit;
            public bool crit;
            public bool killed;       // target is dead after the attack (OnKill)
            public bool didAfflict;   // true once an Afflict effect has targeted this unit
            public bool afflictFailed; // true if the save failed (caster wins, condition lands)
        }

        private static bool PassesGate(ConditionalGate gate, GateState s) => gate switch
        {
            ConditionalGate.OnHit    => s.didAttack && s.hit,
            ConditionalGate.OnMiss   => s.didAttack && !s.hit,
            ConditionalGate.OnCrit   => s.didAttack && s.crit,
            ConditionalGate.OnKill   => s.didAttack && s.killed,
            ConditionalGate.OnResist => s.didAfflict && !s.afflictFailed,
            ConditionalGate.OnFail   => s.didAfflict && s.afflictFailed,
            _                        => true,
        };

        private static void UpdateGateState(
            Dictionary<Unit, GateState> map, Unit target,
            SkillEffectData effect, CombatActionResult result)
        {
            if (effect.effectType == SkillEffectType.Attack)
            {
                map.TryGetValue(target, out var s);
                s.didAttack = true;
                s.hit  = result.didHit;
                s.crit = result.wasCrit;
                s.killed = result.didHit && !target.IsAlive;
                map[target] = s;
            }
            else if (effect.effectType == SkillEffectType.Afflict)
            {
                map.TryGetValue(target, out var s);
                s.didAfflict = true;
                s.afflictFailed = !result.saveSucceeded;
                map[target] = s;
            }
        }

        private readonly struct SaveRollOutcome
        {
            public readonly bool success;
            public readonly int rawD20;
            public readonly int total;
            public readonly int dc;

            public SaveRollOutcome(bool success, int rawD20, int total, int dc)
            {
                this.success = success;
                this.rawD20 = rawD20;
                this.total = total;
                this.dc = dc;
            }
        }

        private static CombatActionResult ResolveSingleEffect(
            Unit caster, string actionName, ActionType actionType,
            bool useAttackRoll, bool skillIsWilSave,
            SkillEffectData effect, Unit target)
        {
            var result = new CombatActionResult
            {
                source = caster,
                target = target,
                actionType = actionType,
                actionName = actionName
            };

            switch (effect.effectType)
            {
                case SkillEffectType.Attack:
                    ResolveAttack(caster, target, useAttackRoll, effect, result);
                    break;

                case SkillEffectType.Apply:
                    if (effect.applyKind == ApplyKind.Damage)
                        ResolveDirectDamage(caster, target, effect, result);
                    else
                        ResolveApplyCondition(caster, target, effect, result);
                    break;

                case SkillEffectType.Afflict:
                    ResolveAfflict(caster, target, effect, result);
                    break;

                case SkillEffectType.DirectDamage:
                    ResolveDirectDamage(caster, target, effect, result);
                    break;

                case SkillEffectType.ApplyCondition:
                    ResolveApplyCondition(caster, target, effect, result);
                    break;

                case SkillEffectType.Heal:
                    target.Heal(effect.magnitude);
                    result.healingDone = effect.magnitude;
                    break;

                case SkillEffectType.RestoreSP:
                    target.currentSP = Mathf.Min(target.maxSP, target.currentSP + effect.magnitude);
                    break;

                case SkillEffectType.LoseHP:
                    target.TakeDirectDamage(effect.magnitude);
                    break;

                case SkillEffectType.ChannelOrb:
                    ResolveChannelOrb(caster, target, effect, result);
                    break;

                case SkillEffectType.EvokeOrb:
                    ResolveEvokeOrb(caster, effect, result);
                    break;

                case SkillEffectType.GainStars:
                    if (target == caster)
                        caster.GainStars(Mathf.Max(0, effect.resourceAmount));
                    result.didHit = true;
                    break;

                case SkillEffectType.GenerateItem:
                    if (target == caster)
                    {
                        var itemAsset = effect.itemSO as ItemData;
                        if (itemAsset != null)
                        {
                            int qty = Mathf.Max(1, effect.itemQuantity);
                            Inventory.Grant(itemAsset.itemID, qty, caster);
                        }
                        else if (effect.itemSO != null)
                        {
                            Debug.LogWarning(
                                $"[SkillResolver] GenerateItem on '{actionName}' has " +
                                $"an itemSO that isn't an ItemData ({effect.itemSO.GetType().Name}). " +
                                "Replace with an ItemData asset.");
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[SkillResolver] GenerateItem on '{actionName}' has " +
                                "no itemSO assigned. Drop an ItemData into the slot.");
                        }
                    }
                    result.didHit = true;
                    break;

                case SkillEffectType.Haste:
                case SkillEffectType.Renew:
                case SkillEffectType.SealSkill:
                case SkillEffectType.SummonCompanion:
                case SkillEffectType.BindCompanion:
                case SkillEffectType.Forge:
                case SkillEffectType.Retaliate:
                case SkillEffectType.OnAllyAttackRider:
                    Debug.LogWarning(
                        $"[SkillResolver] Effect '{effect.effectType}' on action " +
                        $"'{actionName}' is a placeholder — no runtime behavior yet.");
                    result.didHit = true;
                    break;

                case SkillEffectType.RemoveCondition:
                    target.conditions.RemoveCondition(effect.conditionID);
                    break;

                case SkillEffectType.Move:
                    ResolveMovement(caster, target, skillIsWilSave, effect, result);
                    break;
            }

            return result;
        }

        private static void ResolveAttack(
            Unit caster, Unit target, bool useAttackRoll, SkillEffectData effect,
            CombatActionResult result)
        {
            int hits = Mathf.Max(1, effect.hitCount);
            bool anyHit = false;

            for (int i = 0; i < hits; i++)
            {
                if (useAttackRoll)
                {
                    var (hit, crit, rawRoll, totalRoll) = DiceRoller.AttackRoll(
                        caster.EffectiveATK, target.EffectiveDEF);

                    bool critMiss = rawRoll == 1;
                    if (critMiss)
                    {
                        hit = false;
                        caster.TakeDirectDamage(1);
                    }

                    if (i == 0)
                    {
                        result.didRoll = true;
                        result.rawD20Roll = rawRoll;
                        result.totalAttackRoll = totalRoll;
                        result.targetDEF = target.EffectiveDEF;
                        result.wasCrit = crit;
                        result.wasCritMiss = critMiss;
                    }

                    if (hit)
                    {
                        int damage = DamageCalculator.CalculateDamage(
                            effect.magnitude, caster, target, crit, effect.damageStat);
                        damage = DamageCalculator.ApplyOutgoingTriggers(damage, caster, target, crit, didHit: true);
                        target.TakeDamage(damage, caster);
                        result.damageDealt += damage;
                        anyHit = true;
                    }
                }
                else
                {
                    int damage = DamageCalculator.CalculateDamage(
                        effect.magnitude, caster, target, false, effect.damageStat);
                    damage = DamageCalculator.ApplyOutgoingTriggers(damage, caster, target, false, didHit: true);
                    target.TakeDamage(damage, caster);
                    result.damageDealt += damage;
                    anyHit = true;
                }

                if (!target.IsAlive) break;
            }

            result.didHit = anyHit;
        }

        private static void ResolveAfflict(
            Unit caster, Unit target, SkillEffectData effect, CombatActionResult result)
        {
            int dc = effect.saveDC > 0 ? effect.saveDC : 10 + caster.EffectiveWIL;
            var (success, raw, total) = DiceRoller.SaveRoll(
                target.EffectiveWIL, caster.EffectiveWIL, dc);

            result.didSave = true;
            result.saveSucceeded = success;
            result.saveRawD20 = raw;
            result.saveTotal = total;
            result.saveDC = dc;

            if (success)
            {
                return;
            }

            ResolveApplyCondition(caster, target, effect, result);
        }

        private static void ResolveDirectDamage(
            Unit caster, Unit target, SkillEffectData effect, CombatActionResult result)
        {
            int dmg = DamageCalculator.CalculateDirectDamage(
                effect.magnitude, caster, effect.damageStat);
            target.TakeDirectDamage(dmg);
            result.damageDealt = dmg;
            result.didHit = true;
        }

        private static void ResolveApplyCondition(
            Unit caster, Unit target, SkillEffectData effect, CombatActionResult result)
        {
            var lib = ConditionLibrary.Instance;
            var condData = lib != null ? lib.Get(effect.conditionID) : null;
            if (condData == null)
            {
                Debug.LogWarning(
                    $"[SkillResolver] Condition apply failed: " +
                    $"ConditionLibrary has no entry for '{effect.conditionID}'. " +
                    "Create a ConditionData SO with that ID, or refresh the library.");
                return;
            }

            int stacks = ResolveConditionStacks(effect, caster, target, result);
            if (stacks <= 0) return;

            target.conditions.ApplyCondition(condData, stacks);
            result.conditionsApplied.Add((effect.conditionID, stacks));

            if (effect.conditionID == ConditionID.Shields)
                result.defenseGained = stacks;
        }

        private static int ResolveConditionStacks(
            SkillEffectData effect, Unit caster, Unit target, CombatActionResult result)
        {
            int perUnit = effect.conditionStacks > 0 ? effect.conditionStacks : Mathf.Max(1, effect.magnitude);
            int per = Mathf.Max(1, effect.stackPer);

            switch (effect.stackCountKind)
            {
                case ConditionStackSource.Fixed:
                    return perUnit;

                case ConditionStackSource.UnblockedDamage:
                case ConditionStackSource.DamageDealt:
                    return (result.damageDealt / per) * perUnit;

                case ConditionStackSource.CasterPOW:
                    return (caster.EffectivePOW / per) * perUnit;

                case ConditionStackSource.TargetStacks:
                    return (target.conditions.GetStacks(effect.conditionID) / per) * perUnit;

                default:
                    return perUnit;
            }
        }

        private static void ResolveMovement(
            Unit caster, Unit target, bool skillIsWilSave, SkillEffectData effect,
            CombatActionResult result)
        {
            var mgr = CombatManager.Instance;
            if (mgr == null) return;

            int steps = Mathf.Max(1, effect.movementMagnitude);

            bool targetMove = effect.movementKind == MovementKind.Pull
                           || effect.movementKind == MovementKind.Knockback
                           || effect.movementKind == MovementKind.Shuffle;

            if (targetMove && effect.saveDC > 0 && !skillIsWilSave)
            {
                var (success, raw, total) = DiceRoller.SaveRoll(
                    target.EffectiveWIL, caster.EffectiveWIL, effect.saveDC);

                result.didSave = true;
                result.saveSucceeded = success;
                result.saveRawD20 = raw;
                result.saveTotal = total;
                result.saveDC = effect.saveDC;

                if (success)
                {
                    return;
                }
            }

            switch (effect.movementKind)
            {
                case MovementKind.Advance:
                    RankHelper.Advance(mgr.GetLineup(caster), caster, steps);
                    break;
                case MovementKind.Withdraw:
                    RankHelper.Withdraw(mgr.GetLineup(caster), caster, steps);
                    break;
                case MovementKind.Pull:
                    RankHelper.Pull(mgr.GetLineup(target), target, steps);
                    break;
                case MovementKind.Knockback:
                    RankHelper.Knockback(mgr.GetLineup(target), target, steps);
                    break;
                case MovementKind.Shuffle:
                    RankHelper.Shuffle(mgr.GetLineup(target), target);
                    break;
            }

            result.didHit = true;
        }

        private static void ResolveChannelOrb(
            Unit caster, Unit target, SkillEffectData effect, CombatActionResult result)
        {
            var lib = OrbLibrary.Instance;
            if (lib == null) return;

            int count = Mathf.Max(1, effect.orbCount);

            switch (effect.orbSource)
            {
                case OrbSource.Self:
                    for (int i = 0; i < count; i++)
                        OrbManager.Channel(caster, lib.Get(effect.orbType));
                    break;

                case OrbSource.TargetAlly:
                    if (target != null)
                        for (int i = 0; i < count; i++)
                            OrbManager.Channel(target, lib.Get(effect.orbType));
                    break;

                case OrbSource.PerEnemy:
                {
                    var mgr = CombatManager.Instance;
                    if (mgr == null) break;
                    var enemies = caster.isPlayerControlled ? mgr.EnemyUnits : mgr.PlayerUnits;
                    int alive = 0;
                    foreach (var e in enemies) if (e != null && e.IsAlive) alive++;
                    int toChannel = alive * count;
                    for (int i = 0; i < toChannel; i++)
                        OrbManager.Channel(caster, lib.Get(effect.orbType));
                    break;
                }
            }

            result.didHit = true;
        }

        private static void ResolveEvokeOrb(
            Unit caster, SkillEffectData effect, CombatActionResult result)
        {
            int count = Mathf.Max(1, effect.evokeCount);

            switch (effect.evokeKind)
            {
                case EvokeKind.First:
                    OrbManager.EvokeFirstRepeated(caster, count);
                    break;
                case EvokeKind.Leftmost:
                    OrbManager.EvokeLeftmost(caster);
                    break;
                case EvokeKind.All:
                    OrbManager.EvokeAll(caster);
                    break;
            }
            result.didHit = true;
        }

        public static List<Unit> ResolveTargets(
            TargetMode mode, Unit caster, List<Unit> selectedTargets,
            List<Unit> allPlayerUnits, List<Unit> allEnemyUnits,
            int pickIndex = 0)
        {
            var result = new List<Unit>();
            bool casterIsPlayer = caster.isPlayerControlled;

            switch (mode)
            {
                case TargetMode.SingleEnemy:
                    if (selectedTargets != null && selectedTargets.Count > 0)
                    {
                        int idx = pickIndex >= 0 && pickIndex < selectedTargets.Count
                            ? pickIndex : 0;
                        result.Add(selectedTargets[idx]);
                    }
                    break;

                case TargetMode.AllEnemies:
                    var enemies = casterIsPlayer ? allEnemyUnits : allPlayerUnits;
                    foreach (var e in enemies)
                        if (e.IsAlive) result.Add(e);
                    break;

                case TargetMode.RandomEnemy:
                    var pool = casterIsPlayer ? allEnemyUnits : allPlayerUnits;
                    var alive = new List<Unit>();
                    foreach (var e in pool)
                        if (e.IsAlive) alive.Add(e);
                    if (alive.Count > 0)
                        result.Add(alive[Random.Range(0, alive.Count)]);
                    break;

                case TargetMode.Self:
                    result.Add(caster);
                    break;

                case TargetMode.SingleAlly:
                    if (selectedTargets != null && selectedTargets.Count > 0)
                    {
                        int idx = pickIndex >= 0 && pickIndex < selectedTargets.Count
                            ? pickIndex : 0;
                        result.Add(selectedTargets[idx]);
                    }
                    else
                        result.Add(caster);
                    break;

                case TargetMode.AllAllies:
                    var allies = casterIsPlayer ? allPlayerUnits : allEnemyUnits;
                    foreach (var a in allies)
                        if (a.IsAlive) result.Add(a);
                    break;
            }

            return result;
        }

        public static List<CombatActionResult> ResolveEnemyMove(
            Unit enemy, EnemyMove move,
            List<Unit> allPlayerUnits, List<Unit> allEnemyUnits)
        {
            var results = new List<CombatActionResult>();
            if (enemy == null || move == null || move.intents == null) return results;

            for (int i = 0; i < move.intents.Length; i++)
            {
                var intent = move.intents[i];
                if (intent == null) continue;

                Unit primaryTarget = null;
                if (enemy.lockedIntentTargets != null
                    && i < enemy.lockedIntentTargets.Length)
                {
                    primaryTarget = enemy.lockedIntentTargets[i];
                }
                if (primaryTarget == null || !primaryTarget.IsAlive)
                {
                    primaryTarget = EnemyAI.SelectTargetForIntent(enemy, intent, allPlayerUnits);
                }

                var baseTargets = primaryTarget != null
                    ? new List<Unit> { primaryTarget }
                    : new List<Unit>();

                ResolveIntentEffects(enemy, move.name, intent, baseTargets,
                                      allPlayerUnits, allEnemyUnits, results);
            }

            return results;
        }

        private static void ResolveIntentEffects(
            Unit caster, string actionName, EnemyIntent intent,
            List<Unit> baseTargets, List<Unit> allPlayerUnits, List<Unit> allEnemyUnits,
            List<CombatActionResult> results)
        {
            if (intent.effects == null || intent.effects.Length == 0) return;

            var gateState = new Dictionary<Unit, GateState>();

            int effectIdx = 0;
            int loopIters = 0;
            while (effectIdx < intent.effects.Length && loopIters <= MaxLoopIterations)
            {
                var effect = intent.effects[effectIdx];
                if (effect == null) { effectIdx++; continue; }

                if (effect.effectType == SkillEffectType.Attack
                 || effect.effectType == SkillEffectType.Afflict)
                {
                    gateState.Clear();
                }

                List<Unit> effectTargets = BuildEffectTargets(
                    effect, caster, baseTargets, allPlayerUnits, allEnemyUnits, gateState);

                int producedBefore = results.Count;

                foreach (var target in effectTargets)
                {
                    if (target == null || !target.IsAlive) continue;

                    if (effect.gate != ConditionalGate.Always && effect.sameTargetAsGate)
                    {
                    }
                    else if (effect.gate != ConditionalGate.Always)
                    {
                        if (!AnyTargetPassesGate(gateState, effect.gate)) continue;
                    }

                    var result = ResolveSingleEffect(
                        caster, actionName, ActionType.Attack,
                        useAttackRoll: true,
                        skillIsWilSave: false,
                        effect, target);

                    UpdateGateState(gateState, target, effect, result);
                    results.Add(result);
                }

                bool producedResult = results.Count > producedBefore;

                if (producedResult
                    && effect.loopLinkIndex >= 0
                    && effect.loopLinkIndex < intent.effects.Length)
                {
                    loopIters++;
                    effectIdx = effect.loopLinkIndex;
                }
                else
                {
                    effectIdx++;
                }
            }

            if (loopIters > MaxLoopIterations)
            {
                Debug.LogWarning(
                    $"[SkillResolver] Enemy intent in move '{actionName}' hit the loop cap " +
                    $"of {MaxLoopIterations} iterations. Check loopLinkIndex authoring.");
            }
        }
    }
}
