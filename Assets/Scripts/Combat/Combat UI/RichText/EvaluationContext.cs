// EvaluationContext.cs
// -----------------------------------------------------------------------------
// Inputs the NumberEvaluator needs to compute Display + BaseExpected for any
// ComputedNumber token. Construct one of these per render pass.
//
// Caster may be null (inventory/menu screens with no live combatant) — the
// evaluator falls back to BaseMagnitude with no stat scaling. Target may be
// null (no hover or non-targeted skill) — target-side modifiers (Vulnerable,
// future Bruise rules) just don't apply.
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    public struct EvaluationContext
    {
        public Unit Caster;
        public Unit Target;

        public bool HasCaster => Caster != null;
        public bool HasTarget => Target != null;

        public static EvaluationContext ForCaster(Unit caster) =>
            new EvaluationContext { Caster = caster };

        public static EvaluationContext ForCasterAndTarget(Unit caster, Unit target) =>
            new EvaluationContext { Caster = caster, Target = target };
    }
}
