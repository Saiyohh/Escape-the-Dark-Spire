// SkillResolver.cs
// -----------------------------------------------------------------------------
// Static resolver that turns a click (Attack / Guard / Skill / enemy action)
// into a CombatActionResult. Handles:
//   • Weapon attacks (multi-hit, crit, dodge, on-hit conditions)
//   • Guard (Guarding condition application)
//   • Skills with 1-3 effects (damage, direct dmg, heal, SP restore,
//     defense, apply/remove conditions)
//   • Enemy actions (attacks, defense gain, condition application)
//
// Called from CombatManager (player actions) and EnemyAI.ExecuteMove (enemies).
// Uses DiceRoller for rolls and DamageCalculator for damage math.
//
// Crit rules (attack rolls):
//   • Natural 20 (or weapon.critThreshold hit) → hit, 2× base damage
//   • Natural 1 → crit miss; attack fails, attacker takes 1 self-damage
//
// Dice-rule dispatch (Pass 2.C):
//   • AttackRoll → d20 + ATK vs DEF with crit / crit-miss
//   • AutoHit    → skip the roll, every effect in the skill lands
//   • WilSave    → one save per target per cast (cached); success resists
//                  the entire effect block against that target
//   • Passive    → no-op on Play (reserved for event-subscription skills)
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class SkillResolver
    {
        /// <summary>Resolve a basic weapon attack from attacker to target.</summary>
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
                // Attack roll
                var (hit, crit, rawRoll, totalRoll) = DiceRoller.AttackRoll(
                    attacker.EffectiveATK, target.EffectiveDEF, critThreshold);

                // Natural 1 = crit miss. Attack auto-fails; attacker self-damages 1.
                bool critMiss = rawRoll == 1;
                if (critMiss)
                {
                    hit = false;
                    attacker.TakeDirectDamage(1);
                }

                // Store first roll info for display
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
                    // Check dodge
                    float dodgeChance = target.conditions.GetDodgeChance();
                    if (!crit && dodgeChance > 0 && Random.value < dodgeChance)
                    {
                        target.conditions.ConsumeDodge();
                        result.wasDodged = true;
                        continue;
                    }

                    int damage = DamageCalculator.CalculateDamage(baseDmg, attacker, target, crit);
                    target.TakeDamage(damage, attacker);
                    totalDamage += damage;

                    // On-hit condition — looked up from the library
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

        /// <summary>Resolve Guard action — Guarding condition (+4 DEF, or +2 if Frail).</summary>
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

        /// <summary>Resolve a skill with its multi-effect system.</summary>
        /// <param name="actionType">
        /// Tag applied to every emitted CombatActionResult. Skills pass
        /// ActionType.Skill (default); items pass ActionType.Item via
        /// ResolveItem. The combat log and submenu close-on-resolve filters
        /// key off this tag.
        /// </param>
        /// <param name="fireSkillPlayed">
        /// When true (default), fires OnSkillPlayed so conditions/aspects can
        /// react to the play. ResolveItem passes false — items aren't skills
        /// and shouldn't trigger "whenever you play a skill" reactions.
        /// </param>
        public static List<CombatActionResult> ResolveSkill(
            Unit caster, SkillData skill, List<Unit> targets,
            List<Unit> allPlayerUnits, List<Unit> allEnemyUnits,
            ConditionData[] conditionLookup,
            ActionType actionType = ActionType.Skill,
            bool fireSkillPlayed = true)
        {
            var results = new List<CombatActionResult>();

            // Passive skills are triggered by events, not by the Play action.
            // Stub here until the event-subscription system lands (Pass 3+).
            // No SP is consumed, no effects fire.
            if (skill.diceRule == SkillDiceRule.Passive)
            {
                Debug.LogWarning(
                    $"[SkillResolver] Passive skill '{skill.skillName}' was Played, " +
                    "but Passive event-subscription execution isn't implemented yet. " +
                    "No SP spent, no effects resolved.");
                return results;
            }

            // Spend SP (sole gate — no cooldowns in Dark Spire)
            caster.SpendSP(skill.spCost);

            // Spend Stars (Regent flat prerequisite — gated by CanUseSkill).
            if (skill.starCost > 0)
                caster.SpendStars(skill.starCost);

            // Alternate cost (HP sacrifice, etc.)
            if (skill.altCostType == AltCostType.HP && skill.altCostAmount > 0)
                caster.TakeDirectDamage(skill.altCostAmount);

            // Fire OnSkillPlayed so conditions that react to skill plays
            // (e.g. "Whenever you play a skill, channel 1 Lightning") get
            // a chance to run before effects resolve. Suppressed by
            // ResolveItem since items aren't skills.
            if (fireSkillPlayed)
            {
                caster.conditions.FireSkillPlayed(new SkillPlayedContext
                {
                    caster = caster,
                    skill = skill,
                    targets = targets,
                });
            }

            // WilSave skills: cache one save outcome per unique target across
            // all effects in this cast. A single save roll determines whether
            // THIS TARGET resists the whole effect block.
            Dictionary<Unit, SaveRollOutcome> saveCache =
                skill.diceRule == SkillDiceRule.WilSave
                    ? new Dictionary<Unit, SaveRollOutcome>()
                    : null;

            // Conditional-gate state per target. Repopulated whenever a new
            // Attack/Afflict effect starts — so gated effects read outcomes
            // from the MOST RECENT gate-provider, not anything older. This
            // also lets self-looping chains (loopLinkIndex) terminate
            // naturally: once the chained Attack doesn't produce the gate
            // condition, the next iteration finds nothing to fire.
            var gateState = new Dictionary<Unit, GateState>();

            int effectIdx = 0;
            int loopIters = 0;
            while (effectIdx < skill.effects.Length && loopIters <= MaxLoopIterations)
            {
                var effect = skill.effects[effectIdx];
                if (effect == null) { effectIdx++; continue; }

                // Fresh gate state for each new gate-providing effect.
                if (effect.effectType == SkillEffectType.Attack
                 || effect.effectType == SkillEffectType.Afflict)
                {
                    gateState.Clear();
                }

                // Resolve the target set for this effect.
                List<Unit> effectTargets = BuildEffectTargets(
                    effect, caster, targets, allPlayerUnits, allEnemyUnits, gateState);

                int producedBefore = results.Count;

                foreach (var target in effectTargets)
                {
                    if (target == null || !target.IsAlive) continue;

                    // SameAs mode already filtered by gate; skip re-check.
                    // Other cases still do a per-target check so ReRoll + OnX
                    // effects on a newly-rolled target fire only when the gate
                    // was observed on some prior target.
                    if (effect.gate != ConditionalGate.Always && effect.sameTargetAsGate)
                    {
                        // Already filtered in BuildEffectTargets.
                    }
                    else if (effect.gate != ConditionalGate.Always)
                    {
                        if (!AnyTargetPassesGate(gateState, effect.gate)) continue;
                    }

                    // If this is a WilSave skill, compute (or re-use) the target's
                    // save roll. DC uses effect.saveDC when >0, else 10 + caster WIL.
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

                // Do-again link: jump back to the linked effect index if the
                // effect actually ran. Hard-capped by MaxLoopIterations.
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

        /// <summary>Hard cap on loop-link iterations per skill cast.</summary>
        public const int MaxLoopIterations = 16;

        /// <summary>
        /// Resolve an item use through the same effect pipeline as skills.
        /// Items are mini-skills — same SkillEffectData[] authoring, same
        /// targeting, same condition gates and loop links. The only
        /// divergences are zero SP/Star/altCost (handled by zeroing the
        /// wrapper SkillData fields) and ActionType=Item tagging.
        ///
        /// The transient SkillData wrapper is allocated per use and destroyed
        /// after — Unity-idiomatic pattern for ad-hoc SO data, no asset
        /// footprint. Item charge consumption is the caller's responsibility
        /// (CombatManager.OnPlayerChooseItem calls Inventory.Consume).
        /// </summary>
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

        /// <summary>
        /// Pick targets for an effect. Three paths:
        ///   • Gate = Always        → effect.targetMode (normal).
        ///   • Gate + sameTargetAsGate = true → filter gateState to entries that pass
        ///     the gate; the effect runs only on targets the gate was observed on
        ///     ("OnHit: Apply Weak to the unit you hit").
        ///   • Gate + sameTargetAsGate = false → re-roll via effect.targetMode; the
        ///     gate acts as a global "did this outcome happen anywhere?" check
        ///     ("OnKill: Attack another enemy").
        /// </summary>
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

        /// <summary>True if any entry in gateState satisfies the given gate.</summary>
        private static bool AnyTargetPassesGate(Dictionary<Unit, GateState> gateState, ConditionalGate gate)
        {
            foreach (var kvp in gateState)
                if (PassesGate(gate, kvp.Value)) return true;
            return false;
        }

        /// <summary>Per-target gate outcomes carried across effects in a single skill cast.</summary>
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
                // TakeDamage runs inside ResolveAttack before we land here, so
                // !IsAlive here means "this Attack dropped the target to 0 HP".
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

        /// <summary>Cache entry for WilSave skills — one save per target per cast.</summary>
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

        /// <summary>
        /// Resolve a single effect against one target. Shared between player
        /// skill casts and enemy intents — the only skill-vs-intent
        /// differences are passed in: actionName/actionType for the result,
        /// useAttackRoll for whether Attack effects roll d20-vs-DEF or
        /// auto-hit, and skillIsWilSave to suppress double-saves on Move
        /// effects whose parent skill already gated on a save.
        /// </summary>
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
                    // No-roll application. Sub-kind picks damage vs condition.
                    if (effect.applyKind == ApplyKind.Damage)
                        ResolveDirectDamage(caster, target, effect, result);
                    else
                        ResolveApplyCondition(caster, target, effect, result);
                    break;

                case SkillEffectType.Afflict:
                    ResolveAfflict(caster, target, effect, result);
                    break;

                // ── Legacy types — functionally equivalent to their Apply form ──
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
                    // Not damage "dealt" — record via placeholder route if needed
                    break;

                case SkillEffectType.ChannelOrb:
                    ResolveChannelOrb(caster, target, effect, result);
                    break;

                case SkillEffectType.EvokeOrb:
                    ResolveEvokeOrb(caster, effect, result);
                    break;

                case SkillEffectType.GainStars:
                    // Stars go to the caster's pool — no other unit can hold
                    // them. We gate on `target == caster` so that even if a
                    // skill is authored with a multi-target effect (e.g.
                    // AllAllies), the per-target loop in ResolveSkill grants
                    // exactly one batch of Stars (when target lands on the
                    // caster). Canonical authoring still uses targetMode=Self.
                    if (target == caster)
                        caster.GainStars(Mathf.Max(0, effect.resourceAmount));
                    result.didHit = true;
                    break;

                case SkillEffectType.GenerateItem:
                    // Grant the authored item to the party. The legacy itemSO
                    // field is typed Object for back-compat; cast to ItemData
                    // when present and route to Inventory.Grant which honors
                    // category (Pouch → owner's pouch; Party/Brewed → shared
                    // bag) and stackability. Per-target loop in ResolveSkill
                    // can fire on multiple targets — only the first one gates
                    // (gate on target == caster) so the grant happens once,
                    // matching the GainStars pattern.
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
                    // Placeholder effect types — inspector fields are authored, runtime
                    // implementation lands in the per-character kit pass (Necrobinder
                    // Osty, Regent Forge, Silent Shivs/Seal, etc.).
                    Debug.LogWarning(
                        $"[SkillResolver] Effect '{effect.effectType}' on action " +
                        $"'{actionName}' is a placeholder — no runtime behavior yet.");
                    result.didHit = true;
                    break;

                case SkillEffectType.RemoveCondition:
                    // Removal needs only the ID — ConditionManager handles the lookup.
                    target.conditions.RemoveCondition(effect.conditionID);
                    break;

                case SkillEffectType.Move:
                    ResolveMovement(caster, target, skillIsWilSave, effect, result);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Attack effect: d20 + ATK vs DEF, crit on nat crit-threshold, crit-miss on nat 1.
        /// Honors useAttackRoll — when false, skips the roll and damage lands
        /// (AutoHit / WilSave-gated skills that already passed their save).
        /// </summary>
        private static void ResolveAttack(
            Unit caster, Unit target, bool useAttackRoll, SkillEffectData effect,
            CombatActionResult result)
        {
            // Multi-hit attacks (intents like "3×6") loop hitCount times. Each
            // strike is an independent roll — so a 3-hit attack can hit, miss,
            // or crit on each strike. Damage accumulates into one result; only
            // the FIRST hit's roll is recorded for the dice-display animation
            // (matches legacy multi-hit behavior — one floater shows the total).
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
                        target.TakeDamage(damage, caster);
                        result.damageDealt += damage;
                        anyHit = true;
                    }
                }
                else
                {
                    int damage = DamageCalculator.CalculateDamage(
                        effect.magnitude, caster, target, false, effect.damageStat);
                    target.TakeDamage(damage, caster);
                    result.damageDealt += damage;
                    anyHit = true;
                }

                // Don't keep swinging at a corpse.
                if (!target.IsAlive) break;
            }

            // OnHit gates fire if ANY strike landed. The dice display still
            // uses the first roll (raw / crit / critMiss) — if hit#1 missed
            // but later strikes landed, the dice shows the miss but damage
            // floats up via the accumulated damageDealt and OnHit effects
            // (e.g. "On hit, apply Vulnerable") still trigger correctly.
            result.didHit = anyHit;
        }

        /// <summary>
        /// Afflict effect: WIL save against (effect.saveDC ?: 10 + caster WIL).
        /// On FAIL → apply the condition. On SUCCESS → condition resisted.
        /// </summary>
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
                // Resisted — target wins, condition does not land.
                return;
            }

            ResolveApplyCondition(caster, target, effect, result);
        }

        /// <summary>
        /// No-roll direct damage (what was SkillEffectType.DirectDamage, now
        /// reachable via Apply+ApplyKind.Damage). Bypasses DEF and Shields.
        /// Honors effect.damageStat so a Direct Damage effect can still scale
        /// off POW/DEX/WIL if the designer wants.
        /// </summary>
        private static void ResolveDirectDamage(
            Unit caster, Unit target, SkillEffectData effect, CombatActionResult result)
        {
            int dmg = DamageCalculator.CalculateDirectDamage(
                effect.magnitude, caster, effect.damageStat);
            target.TakeDirectDamage(dmg);
            result.damageDealt = dmg;
            result.didHit = true;
        }

        /// <summary>
        /// Apply a condition with stacks. Shared by Apply+Condition, Afflict
        /// (after failed save), and the legacy ApplyCondition path.
        /// </summary>
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

            // UI parity: applying Shields to self/ally populates defenseGained
            // so the log reads "shields +N".
            if (effect.conditionID == ConditionID.Shields)
                result.defenseGained = stacks;
        }

        /// <summary>
        /// Compute how many stacks to apply for a condition effect.
        ///   • Fixed                → conditionStacks directly.
        ///   • Per<Source>          → floor(sourceValue / stackPer) * conditionStacks.
        ///                             "Apply 1 Vulnerable per 2 damage dealt" =
        ///                             conditionStacks=1, stackPer=2, UnblockedDamage.
        /// stackPer clamps to min 1 to avoid div-by-zero.
        /// </summary>
        private static int ResolveConditionStacks(
            SkillEffectData effect, Unit caster, Unit target, CombatActionResult result)
        {
            int perUnit = effect.conditionStacks > 0 ? effect.conditionStacks : Mathf.Max(1, effect.magnitude);
            int per = Mathf.Max(1, effect.stackPer);

            switch (effect.stackCountKind)
            {
                case ConditionStackSource.Fixed:
                    return perUnit;

                // "Apply Doom equal to unblocked damage" (Blight Strike).
                // UnblockedDamage + DamageDealt both read result.damageDealt here
                // (a Shields system distinction that isn't split downstream yet).
                case ConditionStackSource.UnblockedDamage:
                case ConditionStackSource.DamageDealt:
                    return (result.damageDealt / per) * perUnit;

                case ConditionStackSource.CasterPOW:
                    return (caster.EffectivePOW / per) * perUnit;

                // "Apply Doom equal to target's Cursed" — matches the condition being
                // applied for now. Future: let designer pick a specific scaling condition.
                case ConditionStackSource.TargetStacks:
                    return (target.conditions.GetStacks(effect.conditionID) / per) * perUnit;

                default:
                    return perUnit;
            }
        }

        /// <summary>
        /// Resolves the positional movement keywords. Advance/Withdraw move the
        /// caster on their own side and always succeed. Pull/Knockback/Shuffle
        /// move the effect's target — if <c>effect.saveDC &gt; 0</c> and the
        /// parent skill isn't already WilSave-gated, the target rolls a WIL
        /// save; success resists the movement.
        /// </summary>
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

            // Per-effect WIL save gate. Skipped when the parent skill is WilSave
            // (ResolveSkill already rolled + already gated this call — if we're
            // here the target already failed the skill-level save, so auto-apply).
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
                    // Resisted — no displacement. didHit stays false so the log
                    // reads the action as a save-gated failure.
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

            // Reuse didHit as a "completed successfully" flag so the log + UI
            // can tell the movement ran (no dice, auto-hit behavior).
            result.didHit = true;
        }

        // ─── Orb effects (Defect) ────────────────────────────────────────────

        /// <summary>
        /// Channel one or more orbs onto the orb-bearer indicated by
        /// effect.orbSource: Self = the caster (Defect), TargetAlly = the
        /// effect's target ally, PerEnemy = one orb per living enemy onto
        /// the caster. Each orb is resolved through OrbLibrary by orbType,
        /// or picked at random when orbType == Random.
        /// </summary>
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

        /// <summary>
        /// Evoke an orb on the caster's tray. evokeKind selects which:
        /// First = leftmost (the standard Evoke keyword), Rightmost = newest
        /// (Dualcast pattern), All = drain the whole tray.
        /// </summary>
        private static void ResolveEvokeOrb(
            Unit caster, SkillEffectData effect, CombatActionResult result)
        {
            int count = Mathf.Max(1, effect.evokeCount);

            switch (effect.evokeKind)
            {
                case EvokeKind.First:
                    // Dualcast pattern: fire the SAME orb's Evoke `count` times,
                    // then consume once. evokeCount=1 falls through to standard
                    // single-fire behavior.
                    OrbManager.EvokeFirstRepeated(caster, count);
                    break;
                case EvokeKind.Rightmost:
                    // No multi-fire variant for Rightmost yet — call once.
                    OrbManager.EvokeRightmost(caster);
                    break;
                case EvokeKind.All:
                    OrbManager.EvokeAll(caster);
                    break;
            }
            result.didHit = true;
        }

        /// <summary>Resolve targets based on TargetMode.</summary>
        /// <param name="pickIndex">For SingleEnemy / SingleAlly: which entry in
        /// selectedTargets to use. Ignored by non-pick modes. Defaults to 0 so
        /// external callers and legacy single-pick flows pick the first target.</param>
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
                        // Use pickIndex if in bounds, else fall back to 0 so
                        // multi-effect skills with only one pick still work.
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

        // ─── Enemy resolution ────────────────────────────────────────────────

        /// <summary>
        /// Resolve every intent in an enemy's chosen move. Returns one
        /// CombatActionResult per (effect, target) pair, in execution order —
        /// matching the shape ResolveSkill returns so CombatManager can play
        /// both back through the same per-result pipeline.
        ///
        /// Each intent picks its own primary target via
        /// EnemyAI.SelectTargetForIntent (honoring the intent's range and
        /// targetPreference). SingleEnemy effects use that pick; AllEnemies /
        /// Self / AllAllies effects fan out via BuildEffectTargets. When no
        /// player is in range the offensive effects whiff and Self / AllAllies
        /// effects still resolve.
        /// </summary>
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

                Unit primaryTarget = EnemyAI.SelectTargetForIntent(enemy, intent, allPlayerUnits);
                var baseTargets = primaryTarget != null
                    ? new List<Unit> { primaryTarget }
                    : new List<Unit>();

                ResolveIntentEffects(enemy, move.name, intent, baseTargets,
                                      allPlayerUnits, allEnemyUnits, results);
            }

            return results;
        }

        /// <summary>
        /// Iterate one intent's effect chain with the same gate / loop /
        /// per-target-pick logic ResolveSkill uses. No SP cost, no
        /// FireSkillPlayed trigger — those are skill-only concerns.
        /// </summary>
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

                // Fresh gate state at each new gate-providing effect.
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
                        // Already filtered in BuildEffectTargets.
                    }
                    else if (effect.gate != ConditionalGate.Always)
                    {
                        if (!AnyTargetPassesGate(gateState, effect.gate)) continue;
                    }

                    // Enemy intents always roll d20-vs-DEF for Attack effects
                    // (no diceRule field on intents). Move effects evaluate
                    // their own per-effect saveDC via the standard path.
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
