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
