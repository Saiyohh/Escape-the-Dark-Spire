using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class NumberEvaluator
    {
        public struct Result
        {
            public int Display;
            public int BaseExpected;
            public CalcBreakdown Breakdown;
        }

        public static Result Evaluate(NumberSpec spec, EvaluationContext ctx)
        {
            var steps = new List<BreakdownStep>(8);

            int baseExpected = ComputeBaseExpected(spec, ctx);
            int display = ComputeDisplay(spec, ctx, steps, isCrit: false, populateSteps: true);

            bool showCrit = spec.Kind == NumberKind.AttackDamage;
            int critTotal = 0;
            if (showCrit)
                critTotal = ComputeDisplay(spec, ctx, null, isCrit: true, populateSteps: false);

            return new Result
            {
                Display = display,
                BaseExpected = baseExpected,
                Breakdown = new CalcBreakdown
                {
                    Steps = steps,
                    Total = display,
                    CritTotal = critTotal,
                    ShowCritLine = showCrit,
                },
            };
        }

        private static int ComputeBaseExpected(NumberSpec spec, EvaluationContext ctx)
        {
            int baseValue = spec.BaseMagnitude;
            int statBonus = BaseStatBonus(ctx.Caster, spec.Stat);
            int total = Mathf.Max(0, baseValue + statBonus);

            return total;
        }

        private static int BaseStatBonus(Unit caster, DamageStat stat)
        {
            if (caster == null) return 0;
            return stat switch
            {
                DamageStat.POW => caster.basePOW,
                DamageStat.DEX => 0,
                DamageStat.WIL => caster.baseWIL,
                _              => 0,
            };
        }

        private static int ComputeDisplay(
            NumberSpec spec,
            EvaluationContext ctx,
            List<BreakdownStep> steps,
            bool isCrit,
            bool populateSteps)
        {
            int baseValue = isCrit ? spec.BaseMagnitude * 2 : spec.BaseMagnitude;
            if (populateSteps)
                steps.Add(BreakdownStep.MakeInitial("Base", baseValue));

            int running = baseValue;

            if (spec.Stat != DamageStat.None && ctx.Caster != null)
            {
                StatKind statKind = StatKindFor(spec.Stat);

                int baseStat = BaseStatBonus(ctx.Caster, spec.Stat);
                if (baseStat != 0)
                {
                    if (populateSteps)
                        steps.Add(BreakdownStep.MakeAdd(spec.Stat.ToString(), baseStat));
                    running += baseStat;
                }

                int conditionContribution = 0;
                if (ctx.Caster.conditions != null)
                {
                    var all = ctx.Caster.conditions.GetAllConditions();
                    for (int i = 0; i < all.Count; i++)
                    {
                        var inst = all[i];
                        if (inst == null || inst.data == null || inst.stacks <= 0) continue;
                        var mods = inst.data.passiveModifiers;
                        if (mods == null) continue;
                        for (int m = 0; m < mods.Length; m++)
                        {
                            if (mods[m].stat != statKind) continue;
                            int contribution = Mathf.RoundToInt(mods[m].amountPerStack * inst.stacks);
                            if (contribution == 0) continue;
                            if (populateSteps)
                            {
                                string label = !string.IsNullOrEmpty(inst.data.displayName)
                                    ? inst.data.displayName
                                    : inst.data.conditionID.ToString();
                                steps.Add(BreakdownStep.MakeAdd(label, contribution));
                            }
                            conditionContribution += contribution;
                        }
                    }
                }
                running += conditionContribution;
            }

            running = Mathf.Max(0, running);

            if (spec.Kind == NumberKind.AttackDamage && ctx.Caster != null && ctx.Caster.conditions != null)
            {
                int weak = ctx.Caster.conditions.GetStacks(ConditionID.Weak);
                if (weak > 0)
                {
                    float mult = Mathf.Pow(0.75f, weak);
                    if (populateSteps)
                        steps.Add(BreakdownStep.MakeMult(weak == 1 ? "Weak" : $"Weak ×{weak}", mult));
                    running = Mathf.FloorToInt(running * mult);
                }
            }

            if (spec.Kind == NumberKind.AttackDamage && ctx.HasTarget)
            {
                if (DamageCalculator.TryGetIncomingMultiplier(ctx.Target, out float tMult, out string tLabel))
                {
                    if (populateSteps)
                        steps.Add(BreakdownStep.MakeMult(tLabel, tMult));
                    running = Mathf.FloorToInt(running * tMult);
                }
            }

            return Mathf.Max(0, running);
        }

        private static StatKind StatKindFor(DamageStat ds) => ds switch
        {
            DamageStat.POW => StatKind.POW,
            DamageStat.DEX => StatKind.DEX,
            DamageStat.WIL => StatKind.WIL,
            _              => StatKind.POW,
        };
    }
}
