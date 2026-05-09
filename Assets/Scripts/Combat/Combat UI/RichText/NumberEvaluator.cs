// NumberEvaluator.cs
// -----------------------------------------------------------------------------
// Pure (no allocations once the breakdown list is reused) function that takes a
// NumberSpec + EvaluationContext and produces (Display, BaseExpected, breakdown).
//
// Mirrors the existing damage rules in DamageCalculator:
//   • Base + StatBonus(caster, stat)
//   • Weak on caster: × 0.75^stacks (multiplicative, floor, attack only)
//   • Vulnerable on target: × 1.5 (attack only, floor)
//   • Crit: doubles base BEFORE adding stat (attack only)
//
// "BaseExpected" is the formula evaluated against caster's BASE stats (basePOW
// etc.) and NO target — i.e. the number a player would see with zero buffs and
// no debuffs. Display is the live value with all modifiers applied. Coloring
// rule: Display > BaseExpected → green; Display < BaseExpected → red; equal
// → default. See DescriptionRenderer for the actual coloring application.
//
// Per-condition breakdown rows: every condition contributing a passiveModifier
// to the matching stat gets its own row in the breakdown ("Strength +1",
// "Weak -2"), so the NumberCalcBox can show each modifier source. Special-case
// hard-coded multipliers (Weak attack mult, Vulnerable) get their own Mult rows.
// -----------------------------------------------------------------------------
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

        /// <summary>Evaluate a number spec against a context. The breakdown is owned
        /// by the result — caller may reuse the result struct each frame; the
        /// inner List is freshly allocated per call (small) so updating in place
        /// is safe.</summary>
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

        // ─── BaseExpected: formula with caster base stats, no target ───────

        private static int ComputeBaseExpected(NumberSpec spec, EvaluationContext ctx)
        {
            int baseValue = spec.BaseMagnitude;
            int statBonus = BaseStatBonus(ctx.Caster, spec.Stat);
            int total = Mathf.Max(0, baseValue + statBonus);

            // Crits are not part of BaseExpected — descriptions describe normal hits.
            // Vulnerable / Weak are not part of BaseExpected — those are runtime modifiers.
            return total;
        }

        private static int BaseStatBonus(Unit caster, DamageStat stat)
        {
            if (caster == null) return 0;
            return stat switch
            {
                DamageStat.POW => caster.basePOW,
                // DEX has no base stat on Unit (CharacterData.dex feeds baseATK/baseSPD,
                // not a "DEX damage stat"). Match DamageCalculator.StatBonus's use of
                // EffectiveDEX, which is condition-only — so the base contribution is 0.
                DamageStat.DEX => 0,
                DamageStat.WIL => caster.baseWIL,
                _              => 0,
            };
        }

        // ─── Display: full formula with current effective state ────────────

        /// <summary>
        /// Walks the formula and (optionally) appends one BreakdownStep per
        /// contributor: Base, the stat term, each condition that adds to the
        /// stat, then any multiplicative caster (Weak) / target (Vulnerable)
        /// modifiers for attack damage.
        ///
        /// Returns the final integer Display value. When populateSteps is false
        /// (used for the crit recomputation), no rows are appended — we just
        /// want the integer.
        /// </summary>
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

            // Stat contribution — split into base + per-condition rows so the
            // breakdown attributes each source.
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

            // Floor at 0 BEFORE multiplicative steps — matches CalculateDamage.
            running = Mathf.Max(0, running);

            // Caster-side multipliers (attack only).
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

            // Target-side multipliers (attack only).
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
