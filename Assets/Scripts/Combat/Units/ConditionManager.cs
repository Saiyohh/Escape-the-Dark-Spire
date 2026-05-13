using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkSpire
{
    public class ConditionManager
    {
        private Dictionary<ConditionID, ConditionInstance> conditions = new();

        public Unit Owner { get; private set; }

        public event Action<ConditionID, int> OnConditionApplied;
        public event Action<ConditionID> OnConditionRemoved;
        public event Action<ConditionID, int> OnConditionChanged;

        public void Initialize(Unit owner) => Owner = owner;

        // ═════════════════════════════════════════════════════════════════════
        //  Applying / removing / queries
        // ═════════════════════════════════════════════════════════════════════

        public void ApplyCondition(ConditionData data, int amount, Unit source = null)
        {
            if (data == null || amount <= 0) return;

            // Fire apply-event first so triggers (Artifact) can negate before
            // the stack touches storage.
            var ctx = new ConditionApplicationContext
            {
                target = Owner,
                source = source,
                condition = data,
                stacks = amount,
                negated = false,
            };

            if (data.isDebuff)
                Fire(TriggerEvent.OnDebuffApplied, ctx, source);
            else
                Fire(TriggerEvent.OnBuffApplied, ctx, source);
            Fire(TriggerEvent.OnConditionApplied, ctx, source);

            if (ctx.negated) return;

            if (conditions.TryGetValue(data.conditionID, out var existing))
            {
                existing.Apply(ctx.stacks);
                if (existing.sourceUnit == null) existing.sourceUnit = source;
                OnConditionChanged?.Invoke(data.conditionID, existing.GetDisplayValue());
                // Mirror to the global bus so floater spawners / VFX systems
                // get one event regardless of which unit was affected.
                CombatEvents.InvokeConditionApplied(Owner, data.conditionID, ctx.stacks);
            }
            else
            {
                var inst = new ConditionInstance(data, ctx.stacks);
                inst.sourceUnit = source;
                conditions[data.conditionID] = inst;
                OnConditionApplied?.Invoke(data.conditionID, ctx.stacks);
                CombatEvents.InvokeConditionApplied(Owner, data.conditionID, ctx.stacks);
            }
        }

        public void RemoveConditionsFromSource(Unit caster)
        {
            if (caster == null) return;
            var toRemove = new List<ConditionID>();
            foreach (var kvp in conditions)
            {
                if (kvp.Value.sourceUnit == caster)
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove) RemoveCondition(id);
        }

        public void RemoveCondition(ConditionID id)
        {
            if (conditions.Remove(id))
            {
                OnConditionRemoved?.Invoke(id);
                CombatEvents.InvokeConditionRemoved(Owner, id);
            }
        }

        public bool HasCondition(ConditionID id) => conditions.ContainsKey(id);

        public int GetStacks(ConditionID id) =>
            conditions.TryGetValue(id, out var inst) ? inst.stacks : 0;

        public ConditionInstance GetCondition(ConditionID id) =>
            conditions.TryGetValue(id, out var inst) ? inst : null;

        private readonly List<ConditionInstance> cachedConditionList = new();
        public List<ConditionInstance> GetAllConditions()
        {
            cachedConditionList.Clear();
            foreach (var kvp in conditions)
                cachedConditionList.Add(kvp.Value);
            return cachedConditionList;
        }

        public void ClearAll()
        {
            var ids = conditions.Keys.ToList();
            conditions.Clear();
            foreach (var id in ids)
                OnConditionRemoved?.Invoke(id);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Passive stat modifiers — summed from active conditions.
        // ═════════════════════════════════════════════════════════════════════

        public float GetPassiveModifier(StatKind stat)
        {
            float total = 0f;
            foreach (var kvp in conditions)
            {
                var inst = kvp.Value;
                var mods = inst.data.passiveModifiers;
                if (mods == null) continue;
                for (int i = 0; i < mods.Length; i++)
                {
                    if (mods[i].stat == stat)
                        total += mods[i].amountPerStack * inst.stacks;
                }
            }
            return total;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Trigger dispatch
        // ═════════════════════════════════════════════════════════════════════

        // Fire wrappers — one per event for call-site clarity.
        public void FireTurnStart()        => Fire(TriggerEvent.OnTurnStart, null, null);
        public void FireTurnEnd()          => Fire(TriggerEvent.OnTurnEnd, null, null);
        public void FireRoundStart()       => Fire(TriggerEvent.OnRoundStart, null, null);
        public void FireRoundEnd()         => Fire(TriggerEvent.OnRoundEnd, null, null);
        public void FirePlayerPhaseStart() => Fire(TriggerEvent.OnPlayerPhaseStart, null, null);
        public void FirePlayerPhaseEnd()   => Fire(TriggerEvent.OnPlayerPhaseEnd, null, null);
        public void FireEnemyPhaseStart()  => Fire(TriggerEvent.OnEnemyPhaseStart, null, null);
        public void FireEnemyPhaseEnd()    => Fire(TriggerEvent.OnEnemyPhaseEnd, null, null);
        public void FireCleanup()          => Fire(TriggerEvent.OnCleanup, null, null);
        public void FireCombatStart()      => Fire(TriggerEvent.OnCombatStart, null, null);
        public void FireCombatEnd()        => Fire(TriggerEvent.OnCombatEnd, null, null);

        public void FireTakeDamagePre(DamageContext ctx)       => Fire(TriggerEvent.OnTakeDamagePre, ctx, ctx?.source);
        public void FireTakeDamagePost(DamageContext ctx)      => Fire(TriggerEvent.OnTakeDamagePost, ctx, ctx?.source);
        public void FireDealDamage(OutgoingDamageContext ctx)  => Fire(TriggerEvent.OnDealDamage, ctx, ctx?.defender);
        public void FireAttackRoll(AttackRollContext ctx)      => Fire(TriggerEvent.OnAttackRoll, ctx, ctx?.target);
        public void FireHit(OutgoingDamageContext ctx)         => Fire(TriggerEvent.OnHit, ctx, ctx?.defender);
        public void FireMiss(AttackRollContext ctx)            => Fire(TriggerEvent.OnMiss, ctx, ctx?.target);
        public void FireSkillPlayed(SkillPlayedContext ctx)    => Fire(TriggerEvent.OnSkillPlayed, ctx, null);

        private void Fire(TriggerEvent ev, object context, Unit source)
        {
            // Copy to avoid modification-during-iteration (triggers can apply/remove conditions)
            var snapshot = new List<ConditionInstance>(conditions.Values);
            foreach (var inst in snapshot)
            {
                if (inst.stacks <= 0) continue;
                if (inst.data.triggers == null) continue;

                foreach (var trig in inst.data.triggers)
                {
                    if (trig == null || trig.when != ev) continue;
                    if (!AllConditionalsPass(trig.onlyIf, inst, context, source)) continue;

                    bool actionLanded = ExecuteActions(trig.actions, inst, context, source);
                    ApplyStackOp(trig.afterFiring, trig.stackOpAmount, inst, actionLanded);
                }
            }
        }

        private bool AllConditionalsPass(
            TriggerConditional[] conds, ConditionInstance inst, object ctx, Unit source)
        {
            if (conds == null || conds.Length == 0) return true;
            for (int i = 0; i < conds.Length; i++)
            {
                if (!EvaluateConditional(conds[i], inst, ctx, source)) return false;
            }
            return true;
        }

        private bool EvaluateConditional(
            TriggerConditional c, ConditionInstance inst, object ctx, Unit source)
        {
            switch (c.kind)
            {
                case ConditionalKind.Always:                return true;
                case ConditionalKind.StacksAtLeast:         return inst.stacks >= c.intParam;
                case ConditionalKind.StacksAtMost:          return inst.stacks <= c.intParam;

                case ConditionalKind.TargetHPBelowPercent:
                    return Owner != null && Owner.maxHP > 0 &&
                           (Owner.currentHP / (float)Owner.maxHP) < c.floatParam;
                case ConditionalKind.TargetHPAbovePercent:
                    return Owner != null && Owner.maxHP > 0 &&
                           (Owner.currentHP / (float)Owner.maxHP) > c.floatParam;
                case ConditionalKind.SourceHPBelowPercent:
                    return source != null && source.maxHP > 0 &&
                           (source.currentHP / (float)source.maxHP) < c.floatParam;

                case ConditionalKind.RollSucceeds:
                    return UnityEngine.Random.value < c.floatParam;
                case ConditionalKind.RollSucceedsPerStack:
                    return UnityEngine.Random.value < (c.floatParam * inst.stacks);

                case ConditionalKind.IncomingIsDebuff:
                    return ctx is ConditionApplicationContext ca0 && ca0.condition.isDebuff;
                case ConditionalKind.IncomingIsBuff:
                    return ctx is ConditionApplicationContext ca1 && !ca1.condition.isDebuff;
                case ConditionalKind.IncomingConditionIs:
                    return ctx is ConditionApplicationContext ca2
                           && ca2.condition.conditionID == c.conditionParam;

                case ConditionalKind.SourceSkillHasTag:
                    return ctx is SkillPlayedContext sp
                           && sp.skill != null
                           && (sp.skill.tags & c.tagFilter) != 0;

                case ConditionalKind.IsFirstEventThisTurn:
                    // so Vigor-style "next attack" conditions still fire (will consume
                    // via ConsumeAll so the scale is correct after first hit).
                    return true;
            }
            return true;
        }

        private bool ExecuteActions(
            TriggerAction[] actions, ConditionInstance inst, object ctx, Unit source)
        {
            if (actions == null) return false;
            bool anyLanded = false;
            for (int i = 0; i < actions.Length; i++)
            {
                if (ExecuteAction(actions[i], inst, ctx, source)) anyLanded = true;
            }
            return anyLanded;
        }

        private bool ExecuteAction(
            TriggerAction a, ConditionInstance inst, object ctx, Unit source)
        {
            Unit target = ResolveTarget(a.target, source);

            switch (a.kind)
            {
                case TriggerActionKind.DealDamage:
                    if (target != null) { target.TakeDirectDamage(a.amount); return true; }
                    return false;
                case TriggerActionKind.DealDamagePerStack:
                    if (target != null) { target.TakeDirectDamage(a.amountPerStack * inst.stacks); return true; }
                    return false;

                case TriggerActionKind.HealTarget:
                    if (target != null) { target.Heal(a.amount); return true; }
                    return false;
                case TriggerActionKind.HealTargetPerStack:
                    if (target != null) { target.Heal(a.amountPerStack * inst.stacks); return true; }
                    return false;

                case TriggerActionKind.DamageSource:
                    if (source != null) { source.TakeDirectDamage(a.amount); return true; }
                    return false;
                case TriggerActionKind.DamageSourcePerStack:
                    if (source != null) { source.TakeDirectDamage(a.amountPerStack * inst.stacks); return true; }
                    return false;

                case TriggerActionKind.AbsorbDamage:
                    if (ctx is DamageContext dc1)
                    {
                        int absorb = Mathf.Min(a.amount, dc1.amount);
                        dc1.amount -= absorb;
                        return absorb > 0;
                    }
                    return false;
                case TriggerActionKind.AbsorbDamagePerStack:
                    if (ctx is DamageContext dc2)
                    {
                        int absorb = Mathf.Min(a.amountPerStack * inst.stacks, dc2.amount);
                        dc2.amount -= absorb;
                        return absorb > 0;
                    }
                    return false;

                case TriggerActionKind.NegateIncomingEffect:
                    if (ctx is DamageContext dc3)          { dc3.negated = true; dc3.amount = 0; return true; }
                    if (ctx is ConditionApplicationContext ca) { ca.negated = true; return true; }
                    return false;

                case TriggerActionKind.ModifyIncomingDamageFlat:
                    if (ctx is DamageContext dc4) { dc4.amount = Mathf.Max(0, dc4.amount + a.amount); return true; }
                    return false;
                case TriggerActionKind.ModifyIncomingDamagePercent:
                    if (ctx is DamageContext dc5)
                    {
                        dc5.amount = Mathf.Max(0, Mathf.FloorToInt(dc5.amount * (1f + a.percentValue)));
                        return true;
                    }
                    return false;

                case TriggerActionKind.ModifyOutgoingDamageFlat:
                    if (ctx is OutgoingDamageContext oc) { oc.amount += a.amount; return true; }
                    return false;
                case TriggerActionKind.ModifyOutgoingDamagePerStack:
                    if (ctx is OutgoingDamageContext oc2)
                    {
                        oc2.amount += a.amountPerStack * inst.stacks;
                        return true;
                    }
                    return false;
                case TriggerActionKind.ModifyOutgoingDamagePercent:
                    if (ctx is OutgoingDamageContext oc3)
                    {
                        // Weak: percentValue = -0.25 → outgoing × 0.75 (floor, clamp ≥0).
                        // Vigor-style amp would use percentValue = +0.25 → × 1.25.
                        oc3.amount = Mathf.Max(0, Mathf.FloorToInt(oc3.amount * (1f + a.percentValue)));
                        return true;
                    }
                    return false;

                // ── Extended damage-modifier actions ─────────────────────────
                case TriggerActionKind.ModifyIncomingDamagePerStack:
                    if (ctx is DamageContext dcps)
                    {
                        dcps.amount = Mathf.Max(0, dcps.amount + a.amountPerStack * inst.stacks);
                        return true;
                    }
                    return false;
                case TriggerActionKind.ModifyIncomingDamagePercentPerStack:
                    if (ctx is DamageContext dcpp)
                    {
                        // Compound: (1 + p)^stacks. Each stack multiplies independently.
                        // -0.25 × 3 stacks = ×0.75^3 = ×0.4219 → 42% damage taken.
                        // +0.5  × 2 stacks = ×1.5^2  = ×2.25.
                        float mult = Mathf.Pow(1f + a.percentValue, inst.stacks);
                        dcpp.amount = Mathf.Max(0, Mathf.FloorToInt(dcpp.amount * mult));
                        return true;
                    }
                    return false;
                case TriggerActionKind.ModifyOutgoingDamagePercentPerStack:
                    if (ctx is OutgoingDamageContext ocpp)
                    {
                        // Compound: (1 + p)^stacks. Same shape as the incoming variant.
                        float mult = Mathf.Pow(1f + a.percentValue, inst.stacks);
                        ocpp.amount = Mathf.Max(0, Mathf.FloorToInt(ocpp.amount * mult));
                        return true;
                    }
                    return false;

                case TriggerActionKind.CapIncomingDamageAt:
                    if (ctx is DamageContext dcCap)
                    {
                        // Hits below the cap aren't softened — only run the
                        // clamp when the incoming damage actually exceeds it
                        // so afterFiring=ConsumeIfActionLanded works correctly
                        // for "first N significant hits" patterns. We also
                        // return false on no-op so a Slippery stack isn't
                        // burned by a hit that already deals ≤ cap.
                        if (dcCap.amount > a.amount)
                        {
                            dcCap.amount = Mathf.Max(0, a.amount);
                            return true;
                        }
                        return false;
                    }
                    return false;

                case TriggerActionKind.ModifyAttackRollBy:
                    if (ctx is AttackRollContext ar)
                    {
                        ar.totalRoll += a.amount;
                        ar.modifier += a.amount;
                        return true;
                    }
                    return false;
                case TriggerActionKind.ModifyAttackRollByPerStack:
                    if (ctx is AttackRollContext ar2)
                    {
                        int m = a.amountPerStack * inst.stacks;
                        ar2.totalRoll += m;
                        ar2.modifier += m;
                        return true;
                    }
                    return false;

                case TriggerActionKind.ApplyCondition:
                {
                    var lib = ConditionLibrary.Instance;
                    var data = lib != null ? lib.Get(a.conditionID) : null;
                    if (data != null && target != null)
                    {
                        target.conditions.ApplyCondition(data, a.conditionStacks, Owner);
                        return true;
                    }
                    return false;
                }
                case TriggerActionKind.ApplyConditionPerStack:
                {
                    var lib = ConditionLibrary.Instance;
                    var data = lib != null ? lib.Get(a.conditionID) : null;
                    if (data != null && target != null)
                    {
                        target.conditions.ApplyCondition(data, a.conditionStacks * inst.stacks, Owner);
                        return true;
                    }
                    return false;
                }
                case TriggerActionKind.RemoveCondition:
                    if (target != null) { target.conditions.RemoveCondition(a.conditionID); return true; }
                    return false;

                case TriggerActionKind.GrantGuard:
                case TriggerActionKind.GrantGuardPerStack:
                {
                    var lib = ConditionLibrary.Instance;
                    var data = lib != null ? lib.Get(ConditionID.Guard) : null;
                    if (data != null && target != null)
                    {
                        int amt = a.kind == TriggerActionKind.GrantGuardPerStack
                            ? a.amountPerStack * inst.stacks
                            : a.amount;
                        target.conditions.ApplyCondition(data, amt, Owner);
                        return amt > 0;
                    }
                    return false;
                }

                case TriggerActionKind.KillTarget:
                    if (target != null && target.IsAlive)
                    {
                        target.TakeDirectDamage(target.currentHP);
                        return true;
                    }
                    return false;
            }
            return false;
        }

        private Unit ResolveTarget(ActionTarget t, Unit source)
        {
            switch (t)
            {
                case ActionTarget.Self:   return Owner;
                case ActionTarget.Source: return source;
                // Enemies/allies/random require CombatManager lineups; falling
                // back to owner for now. Full support lands when triggers need it.
                default: return Owner;
            }
        }

        private void ApplyStackOp(StackOp op, int amount, ConditionInstance inst, bool actionLanded)
        {
            int before = inst.stacks;
            switch (op)
            {
                case StackOp.NoChange:              break;
                case StackOp.DecrementByOne:        inst.stacks -= 1; break;
                case StackOp.DecrementByN:          inst.stacks -= Mathf.Max(1, amount); break;
                case StackOp.ConsumeAll:            inst.stacks = 0; break;
                case StackOp.ConsumeN:              inst.stacks -= Mathf.Max(1, amount); break;
                case StackOp.ConsumeIfActionLanded:
                    if (actionLanded) inst.stacks -= Mathf.Max(1, amount);
                    break;
            }

            if (inst.stacks <= 0)
            {
                RemoveCondition(inst.data.conditionID);
            }
            else if (inst.stacks != before)
            {
                OnConditionChanged?.Invoke(inst.data.conditionID, inst.stacks);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Legacy methods — thin shims kept for external callers during migration.
        //  Every one of these should eventually be reachable via the trigger
        //  system; the shims let us migrate incrementally.
        // ═════════════════════════════════════════════════════════════════════

        public int ProcessPoisonTick()
        {
            // Caller (CombatManager) previously expected damage amount returned,
            // but the new trigger applies damage directly to Owner. Return 0 so
            // the caller's `if (poisonDmg > 0) TakeDirectDamage(poisonDmg);`
            // becomes a no-op — the poison trigger already hit the unit.
            FireTurnStart();
            return 0;
        }

        public (int damage, int healing) ProcessTickEffects()
        {
            FireCleanup();
            return (0, 0);
        }

        public void TickDurations()
        {
            var toRemove = new List<ConditionID>();
            foreach (var kvp in conditions)
            {
                if (kvp.Value.TickDuration())
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
                RemoveCondition(id);
        }

        public void ClearByTiming(ClearTiming timing)
        {
            bool defenseSticks = HasAnyDefensePersist();
            var toRemove = new List<ConditionID>();
            foreach (var kvp in conditions)
            {
                var data = kvp.Value.data;
                if (data.clearTiming != timing) continue;
                if (kvp.Key == ConditionID.Shields && defenseSticks) continue;
                toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove) RemoveCondition(id);
        }

        public void ClearTurnStartConditions() => ClearByTiming(ClearTiming.OwnerTurnStart);

        private bool HasAnyDefensePersist()
        {
            foreach (var kvp in conditions)
                if (kvp.Value.data.defensePersists) return true;
            return false;
        }

        // Shields helpers — now just reads on the condition instance. AbsorbDamagePerStack
        // trigger drives the actual consumption during OnTakeDamagePre.
        public int GetShields() => GetStacks(ConditionID.Shields);

        public int ConsumeShields(int amount)
        {
            if (amount <= 0 || !HasCondition(ConditionID.Shields)) return 0;
            var inst = conditions[ConditionID.Shields];
            int consumed = Mathf.Min(inst.stacks, amount);
            inst.stacks -= consumed;
            if (inst.stacks <= 0)
                RemoveCondition(ConditionID.Shields);
            else
                OnConditionChanged?.Invoke(ConditionID.Shields, inst.stacks);
            return consumed;
        }

        public int GetDEFModifier() => Mathf.RoundToInt(GetPassiveModifier(StatKind.DEF));

        public int GetDamageModifier() => Mathf.RoundToInt(GetPassiveModifier(StatKind.POW));

        public float GetDodgeChance() => GetPassiveModifier(StatKind.DodgeChancePerStack);

        public void ConsumeDodge()
        {
            // Dodge consumption is now handled by the Dodge condition's
            // ConsumeN StackOp on its trigger; this shim is a no-op kept so
            // older callers don't NPE.
        }
    }
}
