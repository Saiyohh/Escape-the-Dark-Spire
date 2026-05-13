namespace DarkSpire
{
    public enum DescriptionTokenKind
    {
        Plain,           // Literal text — no markup, no hover.
        LineBreak,       // Hard newline. Renderer emits "\n".
        Keyword,         // Glossary word ("Attack", "Seal", "Bruised") — pale yellow + tooltip on hover.
        ComputedNumber,  // A number derived from a NumberSpec — colored vs baseline + calc box on hover.
    }

    public enum NumberKind
    {
        AttackDamage,       // d20+ATK vs DEF damage (Vulnerable applies, Weak applies)
        DirectDamage,       // No-roll damage (Apply+Damage / DirectDamage). No Vulnerable.
        Heal,
        RestoreSP,
        ConditionStacks,    // Number of stacks of a condition being applied
        Flat,               // Magnitude with no caster/target context (e.g. Stars gained)
    }

    [System.Serializable]
    public struct NumberSpec
    {
        public int BaseMagnitude;

        public DamageStat Stat;

        public NumberKind Kind;

        public ConditionID Condition;

        public static NumberSpec ForAttack(int magnitude, DamageStat stat) =>
            new NumberSpec { BaseMagnitude = magnitude, Stat = stat, Kind = NumberKind.AttackDamage };

        public static NumberSpec ForDirect(int magnitude, DamageStat stat) =>
            new NumberSpec { BaseMagnitude = magnitude, Stat = stat, Kind = NumberKind.DirectDamage };

        public static NumberSpec ForHeal(int magnitude) =>
            new NumberSpec { BaseMagnitude = magnitude, Kind = NumberKind.Heal };

        public static NumberSpec ForRestoreSP(int magnitude) =>
            new NumberSpec { BaseMagnitude = magnitude, Kind = NumberKind.RestoreSP };

        public static NumberSpec ForFlat(int magnitude) =>
            new NumberSpec { BaseMagnitude = magnitude, Kind = NumberKind.Flat };

        public static NumberSpec ForConditionStacks(int stacks, ConditionID condition) =>
            new NumberSpec { BaseMagnitude = stacks, Condition = condition, Kind = NumberKind.ConditionStacks };
    }

    [System.Serializable]
    public struct DescriptionToken
    {
        public DescriptionTokenKind Kind;

        public string Text;

        public string KeywordKey;

        public ConditionID ConditionKey;

        public bool HasConditionKey;

        public NumberSpec Number;

        public static DescriptionToken Plain(string text) =>
            new DescriptionToken { Kind = DescriptionTokenKind.Plain, Text = text };

        public static readonly DescriptionToken Break =
            new DescriptionToken { Kind = DescriptionTokenKind.LineBreak };

        public static DescriptionToken Keyword(string key, string display = null) =>
            new DescriptionToken
            {
                Kind = DescriptionTokenKind.Keyword,
                KeywordKey = key,
                Text = display,
            };

        public static DescriptionToken KeywordCondition(ConditionID id, string display = null) =>
            new DescriptionToken
            {
                Kind = DescriptionTokenKind.Keyword,
                KeywordKey = id.ToString(),
                ConditionKey = id,
                HasConditionKey = true,
                Text = display,
            };

        public static DescriptionToken MakeNumber(NumberSpec spec) =>
            new DescriptionToken { Kind = DescriptionTokenKind.ComputedNumber, Number = spec };
    }
}
