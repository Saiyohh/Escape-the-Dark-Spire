// DescriptionToken.cs
// -----------------------------------------------------------------------------
// Canonical intermediate form for any rich description (skill, weapon, item,
// condition body, etc.). The rich-text string a player sees is *derived* from
// a list of these tokens at render time, so the same description can be
// re-evaluated against different evaluation contexts (e.g. a different hover
// target) without re-tokenizing.
//
// See: DescriptionTokenizer (builds tokens), NumberEvaluator (evaluates
// ComputedNumber tokens), DescriptionRenderer (turns tokens → TMP rich text +
// <link> spans), KeywordGlossary (resolves Keyword tokens to display + body).
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    public enum DescriptionTokenKind
    {
        Plain,           // Literal text — no markup, no hover.
        LineBreak,       // Hard newline. Renderer emits "\n".
        Keyword,         // Glossary word ("Attack", "Seal", "Bruised") — pale yellow + tooltip on hover.
        ComputedNumber,  // A number derived from a NumberSpec — colored vs baseline + calc box on hover.
    }

    /// <summary>
    /// What flavor of computed number this is. Drives both the breakdown labels
    /// inside the NumberCalcBox and which target-side modifiers apply.
    /// </summary>
    public enum NumberKind
    {
        AttackDamage,       // d20+ATK vs DEF damage (Vulnerable applies, Weak applies)
        DirectDamage,       // No-roll damage (Apply+Damage / DirectDamage). No Vulnerable.
        Heal,
        RestoreSP,
        ConditionStacks,    // Number of stacks of a condition being applied
        Flat,               // Magnitude with no caster/target context (e.g. Stars gained)
    }

    /// <summary>
    /// Recipe describing how to compute a number. Self-contained — does NOT
    /// reference the SkillEffectData object, so the token list survives a
    /// SkillData edit/recompile.
    /// </summary>
    [System.Serializable]
    public struct NumberSpec
    {
        /// <summary>Flat base in the formula (the "2" in "2+POW").</summary>
        public int BaseMagnitude;

        /// <summary>Which caster stat scales this number (None = no scaling).</summary>
        public DamageStat Stat;

        /// <summary>Drives breakdown semantics + which target-side modifiers fire.</summary>
        public NumberKind Kind;

        /// <summary>For ConditionStacks: which condition is being applied (drives "X per Y stacks of Z" breakdowns and tooltip resolution).</summary>
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

        /// <summary>Plain text content (Plain) or display label override (Keyword — usually empty so glossary's displayName is used).</summary>
        public string Text;

        /// <summary>Glossary lookup key for Keyword tokens (case-insensitive).</summary>
        public string KeywordKey;

        /// <summary>If the keyword is backed by a condition, this lets the renderer pull tooltip body from ConditionData directly.</summary>
        public ConditionID ConditionKey;

        /// <summary>True when ConditionKey should be used for keyword resolution (since enum default is a real value).</summary>
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
