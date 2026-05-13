using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DarkSpire
{
    public static class DescriptionTokenizer
    {
        // ═════════════════════════════════════════════════════════════════════
        //  Skill → tokens
        // ═════════════════════════════════════════════════════════════════════

        public static List<DescriptionToken> BuildSkillTokens(SkillData skill)
        {
            var tokens = new List<DescriptionToken>(16);
            if (skill == null) return tokens;

            // Effects[] is the structural source of truth — it produces live
            // computed numbers, glossary-aware keywords, and target-hover updates.
            // Always prefer it when authored. The `description` field is treated
            // as a fallback for skills with no effects (pure flavor / placeholder
            // SOs), and the inline [[Keyword]] markup is honored there.
            //
            // If a designer wants flavor prose alongside the live mechanical
            // text, that's a future enhancement (e.g. a separate flavor field
            // or a flag to append). The existing description content stays in
            // the asset; it's just no longer used when effects[] are present.
            if (skill.effects != null && skill.effects.Length > 0)
            {
                for (int i = 0; i < skill.effects.Length; i++)
                {
                    int beforeCount = tokens.Count;
                    AppendEffect(skill, skill.effects[i], tokens);
                    if (i + 1 < skill.effects.Length && tokens.Count > beforeCount)
                        tokens.Add(DescriptionToken.Plain(" "));
                }
                return tokens;
            }

            if (!string.IsNullOrEmpty(skill.description))
                ParseInlineMarkup(skill.description, tokens);

            return tokens;
        }

        private static void AppendEffect(SkillData skill, SkillEffectData e, List<DescriptionToken> tokens)
        {
            string tgt = TargetPhrase(e.targetMode);
            bool gated = e.gate != ConditionalGate.Always;
            // When the effect is gated on a previous Attack/Afflict outcome AND
            // sameTargetAsGate is true, the target was already established by the
            // gating effect — re-stating it ("Apply X to an enemy") is noise.
            bool gateImpliesTarget = gated && e.sameTargetAsGate;
            bool selfTarget = e.targetMode == TargetMode.Self;

            // Gate prefix applies to any effect type. Capitalized as a
            // sentence-like preamble: "On hit: Apply 1 Vulnerable."
            if (gated)
            {
                string gatePhrase = GatePhrase(e.gate);
                if (!string.IsNullOrEmpty(gatePhrase))
                    tokens.Add(DescriptionToken.Plain(gatePhrase));
            }

            switch (e.effectType)
            {
                case SkillEffectType.Attack:
                    string verb = skill.diceRule == SkillDiceRule.AttackRoll ? "Attack" : "Deal";
                    tokens.Add(DescriptionToken.Keyword(verb));
                    tokens.Add(DescriptionToken.Plain(gateImpliesTarget ? " for " : $" {tgt} for "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForAttack(e.magnitude, e.damageStat)));
                    tokens.Add(DescriptionToken.Plain(" damage."));
                    return;

                case SkillEffectType.Apply:
                    if (e.applyKind == ApplyKind.Damage)
                    {
                        tokens.Add(DescriptionToken.Plain("Deal "));
                        tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForDirect(e.magnitude, e.damageStat)));
                        tokens.Add(DescriptionToken.Plain(gateImpliesTarget
                            ? " direct damage."
                            : $" direct damage to {tgt}."));
                    }
                    else
                    {
                        AppendConditionClause(e, tgt, afflict: false, gateImpliesTarget, selfTarget, tokens);
                    }
                    return;

                case SkillEffectType.Afflict:
                    AppendConditionClause(e, tgt, afflict: true, gateImpliesTarget, selfTarget, tokens);
                    return;

                case SkillEffectType.DirectDamage:  // legacy
                    tokens.Add(DescriptionToken.Plain("Deal "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForDirect(e.magnitude, e.damageStat)));
                    tokens.Add(DescriptionToken.Plain(gateImpliesTarget
                        ? " direct damage."
                        : $" direct damage to {tgt}."));
                    return;

                case SkillEffectType.Heal:
                    tokens.Add(DescriptionToken.Keyword("Heal"));
                    if (selfTarget || gateImpliesTarget)
                        tokens.Add(DescriptionToken.Plain(" for "));
                    else
                        tokens.Add(DescriptionToken.Plain($" {tgt} for "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForHeal(e.magnitude)));
                    tokens.Add(DescriptionToken.Plain(" HP."));
                    return;

                case SkillEffectType.RestoreSP:
                    tokens.Add(DescriptionToken.Keyword("Restore"));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForRestoreSP(e.magnitude)));
                    if (selfTarget || gateImpliesTarget)
                        tokens.Add(DescriptionToken.Plain(" SP."));
                    else
                        tokens.Add(DescriptionToken.Plain($" SP to {tgt}."));
                    return;

                case SkillEffectType.ApplyCondition:  // legacy
                    AppendConditionClause(e, tgt, afflict: skill.diceRule == SkillDiceRule.WilSave, gateImpliesTarget, selfTarget, tokens);
                    return;

                case SkillEffectType.RemoveCondition:
                    tokens.Add(DescriptionToken.Plain("Remove "));
                    tokens.Add(DescriptionToken.KeywordCondition(e.conditionID));
                    tokens.Add(DescriptionToken.Plain($" from {tgt}."));
                    return;

                case SkillEffectType.Move:
                    AppendMoveClause(e, tokens);
                    return;

                case SkillEffectType.LoseHP:
                    tokens.Add(DescriptionToken.Plain("Lose "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.magnitude)));
                    tokens.Add(DescriptionToken.Plain(" HP."));
                    return;

                case SkillEffectType.Haste:
                    if (e.extraActionKind == ExtraActionKind.Renew)
                        tokens.Add(DescriptionToken.Keyword("Renew"));
                    else
                        tokens.Add(DescriptionToken.Keyword("Haste"));
                    tokens.Add(DescriptionToken.Plain($" {tgt}."));
                    return;

                case SkillEffectType.Renew:
                    tokens.Add(DescriptionToken.Keyword("Renew"));
                    tokens.Add(DescriptionToken.Plain($" {tgt}."));
                    return;

                case SkillEffectType.SealSkill:
                    AppendSealClause(e, tokens);
                    return;

                case SkillEffectType.GenerateItem:
                {
                    string itemName = e.pouchItemType == PouchItemType.None ? "items" : e.pouchItemType.ToString();
                    tokens.Add(DescriptionToken.Keyword("Generate"));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.itemQuantity)));
                    tokens.Add(DescriptionToken.Plain($" {itemName}."));
                    return;
                }

                case SkillEffectType.ChannelOrb:
                    AppendChannelClause(e, tokens);
                    return;

                case SkillEffectType.EvokeOrb:
                {
                    tokens.Add(DescriptionToken.Keyword("Evoke"));

                    // Names follow on-screen layout: slot 0 renders on the right
                    // (First = rightmost), slot N-1 renders on the left (Leftmost).
                    string orbPhrase = e.evokeKind switch
                    {
                        EvokeKind.All      => " all your Orbs",
                        EvokeKind.Leftmost => " your leftmost Orb",
                        _                  => " your rightmost Orb", // First
                    };
                    tokens.Add(DescriptionToken.Plain(orbPhrase));

                    // Tail: count suffix + period. evokeCount drives Dualcast and
                    // any future multi-fire orb skills.
                    if (e.evokeCount == 2)
                        tokens.Add(DescriptionToken.Plain(" twice."));
                    else if (e.evokeCount > 2)
                        tokens.Add(DescriptionToken.Plain($" {e.evokeCount} times."));
                    else
                        tokens.Add(DescriptionToken.Plain("."));

                    return;
                }

                case SkillEffectType.SummonCompanion:
                    tokens.Add(DescriptionToken.Keyword("Summon"));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.magnitude)));
                    tokens.Add(DescriptionToken.Plain("."));
                    return;

                case SkillEffectType.BindCompanion:
                    tokens.Add(DescriptionToken.Keyword("Bind"));
                    tokens.Add(DescriptionToken.Plain($" Osty to {tgt} for "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.bindDuration)));
                    tokens.Add(DescriptionToken.Plain(" turn(s)."));
                    return;

                case SkillEffectType.GainStars:
                    tokens.Add(DescriptionToken.Plain("Gain "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.resourceAmount)));
                    tokens.Add(DescriptionToken.Plain(" Star(s)."));
                    return;

                case SkillEffectType.Forge:
                    tokens.Add(DescriptionToken.Keyword("Forge"));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.resourceAmount)));
                    tokens.Add(DescriptionToken.Plain("."));
                    return;

                case SkillEffectType.Retaliate:
                    tokens.Add(DescriptionToken.Plain("Until start of your next turn, if an enemy attacks you, they take "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.retaliateDamage)));
                    tokens.Add(DescriptionToken.Plain(" damage."));
                    return;

                case SkillEffectType.OnAllyAttackRider:
                    tokens.Add(DescriptionToken.Plain("Until start of your next turn, gain "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.magnitude)));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.KeywordCondition(ConditionID.Guard));
                    tokens.Add(DescriptionToken.Plain(" whenever an ally attacks an enemy."));
                    return;
            }
        }

        private static void AppendConditionClause(
            SkillEffectData e, string tgt,
            bool afflict, bool gateImpliesTarget, bool selfTarget,
            List<DescriptionToken> tokens)
        {
            int n = e.conditionStacks > 0 ? e.conditionStacks : Mathf.Max(1, e.magnitude);
            int per = Mathf.Max(1, e.stackPer);

            // Verb + (optional) target-bridge prose.
            if (afflict)
            {
                tokens.Add(DescriptionToken.Keyword("Afflict"));
                tokens.Add(DescriptionToken.Plain(gateImpliesTarget ? " to apply " : $" {tgt} to apply "));
            }
            else if (selfTarget)
            {
                // "Gain 2 Strength." reads better than "Apply 2 Strength to yourself."
                tokens.Add(DescriptionToken.Keyword("Gain"));
                tokens.Add(DescriptionToken.Plain(" "));
            }
            else
            {
                tokens.Add(DescriptionToken.Keyword("Apply"));
                tokens.Add(DescriptionToken.Plain(" "));
            }

            // Stack count — Fixed kinds get a single ComputedNumber; derived kinds
            // keep the existing prose ("X per Y damage dealt") and the X is a Number.
            switch (e.stackCountKind)
            {
                case ConditionStackSource.Fixed:
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(n, e.conditionID)));
                    tokens.Add(DescriptionToken.Plain(" "));
                    break;
                case ConditionStackSource.UnblockedDamage:
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(n, e.conditionID)));
                    tokens.Add(DescriptionToken.Plain($" per {per} unblocked damage "));
                    break;
                case ConditionStackSource.DamageDealt:
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(n, e.conditionID)));
                    tokens.Add(DescriptionToken.Plain($" per {per} damage dealt "));
                    break;
                case ConditionStackSource.CasterPOW:
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(n, e.conditionID)));
                    tokens.Add(DescriptionToken.Plain($" per {per} POW "));
                    break;
                case ConditionStackSource.TargetStacks:
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(n, e.conditionID)));
                    tokens.Add(DescriptionToken.Plain($" per {per} "));
                    break;
            }

            tokens.Add(DescriptionToken.KeywordCondition(e.conditionID));

            if (e.stackCountKind == ConditionStackSource.TargetStacks)
                tokens.Add(DescriptionToken.Plain(" on target"));

            // Trailer.
            if (afflict || selfTarget || gateImpliesTarget)
                tokens.Add(DescriptionToken.Plain("."));
            else
                tokens.Add(DescriptionToken.Plain($" to {tgt}."));
        }

        private static string GatePhrase(ConditionalGate g) => g switch
        {
            ConditionalGate.OnHit    => "On hit: ",
            ConditionalGate.OnMiss   => "On miss: ",
            ConditionalGate.OnCrit   => "On crit: ",
            ConditionalGate.OnKill   => "On kill: ",
            ConditionalGate.OnResist => "On resist: ",
            ConditionalGate.OnFail   => "On fail: ",
            _                        => "",
        };

        private static void AppendMoveClause(SkillEffectData e, List<DescriptionToken> tokens)
        {
            string save = e.saveDC > 0 ? $" (WIL DC {e.saveDC} resists)" : "";
            string verb = e.movementKind switch
            {
                MovementKind.Advance   => "Advance",
                MovementKind.Withdraw  => "Withdraw",
                MovementKind.Pull      => "Pull",
                MovementKind.Knockback => "Knockback",
                MovementKind.Shuffle   => "Shuffle",
                _ => null,
            };
            if (verb == null) return;

            tokens.Add(DescriptionToken.Keyword(verb));
            if (e.movementKind == MovementKind.Shuffle)
            {
                tokens.Add(DescriptionToken.Plain(save + "."));
            }
            else
            {
                tokens.Add(DescriptionToken.Plain(" "));
                tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.movementMagnitude)));
                tokens.Add(DescriptionToken.Plain(save + "."));
            }
        }

        private static void AppendSealClause(SkillEffectData e, List<DescriptionToken> tokens)
        {
            tokens.Add(DescriptionToken.Keyword("Seal"));
            switch (e.sealTarget)
            {
                case SealTargetKind.SelfSkill:
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.sealDuration)));
                    tokens.Add(DescriptionToken.Plain("."));
                    break;
                case SealTargetKind.ChosenSkill:
                    tokens.Add(DescriptionToken.Plain(" a skill for "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.sealDuration)));
                    tokens.Add(DescriptionToken.Plain(" turn(s)."));
                    break;
                case SealTargetKind.RandomSkill:
                    tokens.Add(DescriptionToken.Plain(" a random skill for "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.sealDuration)));
                    tokens.Add(DescriptionToken.Plain(" turn(s)."));
                    break;
            }
        }

        private static void AppendChannelClause(SkillEffectData e, List<DescriptionToken> tokens)
        {
            string orbName = e.orbType == OrbType.Random ? "random Orb" : $"{e.orbType} Orb";

            switch (e.orbSource)
            {
                case OrbSource.TargetAlly:
                    // "An ally channels..." — sentence-mid lowercase verb, not the keyword.
                    tokens.Add(DescriptionToken.Plain($"An ally channels {orbName}."));
                    return;
                case OrbSource.PerEnemy:
                    tokens.Add(DescriptionToken.Keyword("Channel"));
                    tokens.Add(DescriptionToken.Plain($" 1 {orbName} for each enemy in combat."));
                    return;
                default:
                    tokens.Add(DescriptionToken.Keyword("Channel"));
                    tokens.Add(DescriptionToken.Plain(" "));
                    tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(e.orbCount)));
                    tokens.Add(DescriptionToken.Plain($" {orbName}{(e.orbCount > 1 ? "s" : "")}."));
                    return;
            }
        }

        private static string TargetPhrase(TargetMode mode) => mode switch
        {
            TargetMode.SingleEnemy => "an enemy",
            TargetMode.AllEnemies  => "all enemies",
            TargetMode.RandomEnemy => "a random enemy",
            TargetMode.Self        => "yourself",
            TargetMode.SingleAlly  => "an ally",
            TargetMode.AllAllies   => "all allies",
            TargetMode.RandomAlly  => "a random ally",
            TargetMode.WholeParty  => "the whole party",
            _ => "a target",
        };

        // ═════════════════════════════════════════════════════════════════════
        //  Weapon → tokens (auto-gen header + hand-flavor)
        // ═════════════════════════════════════════════════════════════════════

        public static List<DescriptionToken> BuildWeaponTokens(WeaponData weapon)
        {
            var tokens = new List<DescriptionToken>(8);
            if (weapon == null) return tokens;

            // Mechanical header — generated from data fields.
            tokens.Add(DescriptionToken.Keyword("Attack"));
            tokens.Add(DescriptionToken.Plain(" for "));
            tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForAttack(weapon.baseDamage, DamageStat.POW)));

            if (weapon.hitCount > 1)
            {
                tokens.Add(DescriptionToken.Plain(" × "));
                tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForFlat(weapon.hitCount)));
                tokens.Add(DescriptionToken.Plain(" hits."));
            }
            else
            {
                tokens.Add(DescriptionToken.Plain(" damage."));
            }

            if (weapon.hasOnHit)
            {
                tokens.Add(DescriptionToken.Plain(" On hit: "));
                tokens.Add(DescriptionToken.Keyword("Apply"));
                tokens.Add(DescriptionToken.Plain(" "));
                tokens.Add(DescriptionToken.MakeNumber(NumberSpec.ForConditionStacks(weapon.onHitConditionStacks, weapon.onHitConditionID)));
                tokens.Add(DescriptionToken.Plain(" "));
                tokens.Add(DescriptionToken.KeywordCondition(weapon.onHitConditionID));
                tokens.Add(DescriptionToken.Plain("."));
            }

            // Hand-written flavor — parsed for [[Keyword]] markup, no numbers.
            if (!string.IsNullOrEmpty(weapon.description))
            {
                tokens.Add(DescriptionToken.Break);
                ParseInlineMarkup(weapon.description, tokens);
            }

            return tokens;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Condition body → tokens
        // ═════════════════════════════════════════════════════════════════════

        public static List<DescriptionToken> BuildConditionTokens(ConditionData condition)
        {
            var tokens = new List<DescriptionToken>(2);
            if (condition == null || string.IsNullOrEmpty(condition.description)) return tokens;
            ParseInlineMarkup(condition.description, tokens);
            return tokens;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Inline markup parser: [[Keyword]] and \n
        // ═════════════════════════════════════════════════════════════════════
        //
        // Lightweight — recognizes only [[…]] keyword spans and converts \n
        // into LineBreak tokens. Anything else is plain text. Numbers in prose
        // stay plain (we never invent computed numbers from markup).

        public static void ParseInlineMarkup(string text, List<DescriptionToken> tokens)
        {
            if (string.IsNullOrEmpty(text)) return;

            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                // [[Keyword]] open
                if (i + 1 < text.Length && text[i] == '[' && text[i + 1] == '[')
                {
                    int end = text.IndexOf("]]", i + 2);
                    if (end > i + 2)
                    {
                        FlushPlain(sb, tokens);
                        string key = text.Substring(i + 2, end - (i + 2));
                        tokens.Add(DescriptionToken.Keyword(key));
                        i = end + 2;
                        continue;
                    }
                }

                // Newline
                if (text[i] == '\n')
                {
                    FlushPlain(sb, tokens);
                    tokens.Add(DescriptionToken.Break);
                    i++;
                    continue;
                }

                sb.Append(text[i]);
                i++;
            }
            FlushPlain(sb, tokens);
        }

        private static void FlushPlain(StringBuilder sb, List<DescriptionToken> tokens)
        {
            if (sb.Length == 0) return;
            tokens.Add(DescriptionToken.Plain(sb.ToString()));
            sb.Clear();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Token list → plain string (editor preview / fallback)
        // ═════════════════════════════════════════════════════════════════════

        public static string TokensToPlainString(IReadOnlyList<DescriptionToken> tokens)
        {
            if (tokens == null) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                switch (t.Kind)
                {
                    case DescriptionTokenKind.Plain:
                        sb.Append(t.Text);
                        break;
                    case DescriptionTokenKind.LineBreak:
                        sb.Append('\n');
                        break;
                    case DescriptionTokenKind.Keyword:
                        sb.Append(ResolveKeywordDisplay(t));
                        break;
                    case DescriptionTokenKind.ComputedNumber:
                        // Editor preview emits the static formula expression.
                        sb.Append(FormulaText(t.Number));
                        break;
                }
            }
            return sb.ToString();
        }

        private static string ResolveKeywordDisplay(DescriptionToken t)
        {
            if (!string.IsNullOrEmpty(t.Text)) return t.Text;
            var glossary = KeywordGlossary.Instance;
            if (glossary != null)
            {
                var entry = t.HasConditionKey
                    ? glossary.ResolveCondition(t.ConditionKey)
                    : glossary.Resolve(t.KeywordKey);
                if (entry != null) return glossary.GetDisplayName(entry);
            }
            return t.HasConditionKey ? t.ConditionKey.ToString() : (t.KeywordKey ?? string.Empty);
        }

        private static string FormulaText(NumberSpec spec)
        {
            return spec.Stat switch
            {
                DamageStat.POW => $"{spec.BaseMagnitude}+POW",
                DamageStat.DEX => $"{spec.BaseMagnitude}+DEX",
                DamageStat.WIL => $"{spec.BaseMagnitude}+WIL",
                _              => spec.BaseMagnitude.ToString(),
            };
        }
    }
}
