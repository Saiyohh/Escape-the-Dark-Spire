// SkillData.cs
// -----------------------------------------------------------------------------
// ScriptableObject describing one active skill. Fields are organized so the
// custom SkillDataEditor can show sections cleanly:
//
// Description rendering (Pass 3):
//   BuildDescriptionTokens() returns the structured token list that the
//   DescriptionRenderer (Combat UI/RichText/) uses to inline live computed
//   numbers, color them vs. the unbuffed baseline, and tooltip glossary
//   keywords. BuildDescription() (legacy plain string) is now a thin wrapper
//   around the tokenizer for editor preview / fallback callers.
//
//   [Identity]    — name, icon, description, generatedDescription (preview)
//   [Cost]        — spCost, altCost (HP/Stars/Focus/Souls), actionCostType
//   [Dice Rule]   — diceRule (AttackRoll / AutoHit / WilSave / Passive)
//   [Range]       — rangeMin, rangeMax, rangeDisplay
//   [Tags]        — [Flags] SkillTag
//   [Effects]     — array of SkillEffectData (custom inspector hides irrelevant fields)
//   [Meta]        — alignment, rarity, mastery, upgrade tiers
//
// SP is the sole use-gate — cooldowns were stripped. Seal (per-skill runtime
// cooldown applied dynamically by effects) lives in its own runtime system;
// not a static SO field.
//
// Description generation (Pass 2 QoL):
//   Call BuildDescription() to produce a GDD-keyword-consistent string from
//   the effects list. The custom editor shows this as a preview and offers a
//   "Copy to description" button so designers can either hand-write or
//   template-generate. Runtime still reads from `description`.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "DarkSpire/Skill")]
    public class SkillData : ScriptableObject
    {
        // NOTE: no [Header(...)] attributes — the custom SkillDataEditor owns
        // section layout. Inline Header attributes misalign paired fields like
        // Range Min / Max inside horizontal rows and render duplicate labels.

        // ── Identity ────────────────────────────────────────────────────────
        public string skillName;
        [TextArea(2, 4)] public string description;

        /// <summary>
        /// Optional wide artwork (banner/header) displayed on the skill card.
        /// Skills do NOT have square icons — this is intentionally a banner
        /// shape. May be left null; when rendering a skill's art, ALWAYS go
        /// through <see cref="GetArtBanner"/> so the global placeholder is
        /// substituted in. Never read <c>artBanner</c> directly at a UI
        /// call site — a null slip-through will produce an empty frame.
        /// </summary>
        [Tooltip("Wide artwork panel shown on the skill card. Leave empty to fall back to the global " +
                 "placeholder banner at Resources/UI/DefaultSkillArtBanner.png. " +
                 "Skills have no square icon — this is the only art slot.")]
        [FormerlySerializedAs("icon")]
        public Sprite artBanner;

        [Tooltip("Vertical focal point inside the banner art. 0 = sprite center, " +
                 "+1 = show the top of the sprite, -1 = show the bottom. Used by " +
                 "SkillInfoPanelUI when the banner overflows vertically (image " +
                 "taller than mask after cover-fit scaling).")]
        [Range(-1f, 1f)]
        public float bannerFocalY = 0f;

        [Tooltip("Horizontal focal point inside the banner art. 0 = center, +1 = " +
                 "show the right edge, -1 = show the left edge. Applied when the " +
                 "banner overflows horizontally (square-ish masks where the image " +
                 "is wider than tall after cover-fit scaling).")]
        [Range(-1f, 1f)]
        public float bannerFocalX = 0f;

        [Tooltip("When true, the banner renders in black & white via the " +
                 "DarkSpire/UI/BlackAndWhite shader, using the six channel " +
                 "weights below. Matches Photoshop's Black & White adjustment " +
                 "so color art can be desaturated in-engine without a PS pass.")]
        public bool bannerGrayscale = false;

        // Photoshop default preset weights (Custom with these starting values).
        // Range 0..3 in the underlying field; 0%..300% in the inspector label.
        [Range(0f, 3f)] public float bwReds     = 0.40f;
        [Range(0f, 3f)] public float bwYellows  = 0.60f;
        [Range(0f, 3f)] public float bwGreens   = 0.40f;
        [Range(0f, 3f)] public float bwCyans    = 0.60f;
        [Range(0f, 3f)] public float bwBlues    = 0.20f;
        [Range(0f, 3f)] public float bwMagentas = 0.80f;

        // ── Cost ────────────────────────────────────────────────────────────
        [Min(0)] public int spCost;

        [Tooltip("Regent-only flat cost — Stars are deducted on play. " +
                 "CanUseSkill blocks the play unless caster.currentStars >= starCost. " +
                 "0 for every non-Regent skill.")]
        [Min(0)] public int starCost;

        public AltCostType altCostType = AltCostType.None;
        [Min(0)] public int altCostAmount;

        public ActionCostType actionCostType = ActionCostType.Action;

        // ── Dice rule ───────────────────────────────────────────────────────
        [Tooltip("How the skill resolves. AttackRoll = d20+ATK vs DEF. " +
                 "AutoHit = no roll. WilSave = target rolls WIL save per cast " +
                 "(success resists whole effect block). Passive = not played " +
                 "directly, fires from events (stub).")]
        public SkillDiceRule diceRule = SkillDiceRule.AttackRoll;

        // ── Tags ────────────────────────────────────────────────────────────
        [Tooltip("Mechanical subsystem markers — mirrors the Notion Skills DB " +
                 "Tags column. Aspect Tree triggers, Forge accumulation, Orb " +
                 "routing, etc. key off these.")]
        public SkillTag tags;

        // ── Effects ─────────────────────────────────────────────────────────
        public TargetMode primaryTargetMode;
        public SkillEffectData[] effects;

        [Tooltip("How many targets the player picks for this skill. Default 1 = " +
                 "one pick, all SingleEnemy/SingleAlly effects share that pick " +
                 "(e.g. 'Afflict 2 Vulnerable + 2 Weak' = 1 pick, both land on " +
                 "the same enemy). >1 = multi-pick (e.g. 'Mortar: 3 attacks on 3 " +
                 "different enemies in range' = 3 picks, each effect's " +
                 "targetPickIndex chooses which pick it uses).")]
        [Min(1)] public int targetPickCount = 1;

        // ── Range (distance units) ──────────────────────────────────────────
        [Min(0)] public int rangeMin = 1;
        [Min(0)] public int rangeMax = 3;
        [Tooltip("Human-readable label shown on the skill card. Purely display.")]
        public string rangeDisplay = "1-3";

        // ── Meta ────────────────────────────────────────────────────────────
        public Alignment alignment;
        public Rarity rarity;
        public SkillData upgradedVersion;
        public SkillData masteryVersion;
        [Min(0)] public int masteryThreshold = 8;
        [Min(0)] public int maxUpgradeTier = 1;
        [Min(0)] public int shopCost;

        // ─────────────────────────────────────────────────────────────────────
        //  Art banner accessor
        // ─────────────────────────────────────────────────────────────────────
        //
        // Resolves the skill's banner art with a placeholder fallback. UI code
        // should ALWAYS call GetArtBanner() instead of reading `artBanner`
        // directly — that guarantees a non-null sprite as long as the
        // placeholder asset exists at Resources/UI/DefaultSkillArtBanner.
        //
        // How to wire the placeholder:
        //   1. Import/author a wide banner sprite.
        //   2. Place it at Assets/Resources/UI/DefaultSkillArtBanner.png (the
        //      filename must match, no extension in the Resources.Load call).
        //   3. Any skill with `artBanner == null` will render this sprite.
        //
        // If the placeholder itself is missing the method returns null — let
        // the UI draw its empty-frame fallback rather than crashing.

        private static Sprite _defaultArtBannerCache;
        private static bool _defaultArtBannerLoaded;

        public const string DefaultArtBannerResourcePath = "UI/DefaultSkillArtBanner";

        /// <summary>
        /// Returns the authored banner, or the global placeholder from
        /// Resources if the slot is empty. May return null if neither exists.
        /// </summary>
        public Sprite GetArtBanner()
        {
            if (artBanner != null) return artBanner;
            if (!_defaultArtBannerLoaded)
            {
                _defaultArtBannerCache = Resources.Load<Sprite>(DefaultArtBannerResourcePath);
                _defaultArtBannerLoaded = true;
            }
            return _defaultArtBannerCache;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Description auto-generation
        // ─────────────────────────────────────────────────────────────────────
        //
        // Walks the effects array and produces a GDD-keyword-consistent
        // description string. Uses the vocabulary from the Skill Keyword Glossary
        // (Attack, Afflict, Apply, Heal, Restore, Generate, Channel, Evoke,
        // Forge, Summon, Advance/Withdraw/Pull/Knockback/Shuffle, Haste, Renew,
        // Seal). Designers can preview + copy into `description` via the
        // SkillDataEditor.
        //
        // Covers real effect types with full fidelity; placeholder subsystem
        // effects (Orb/Osty/Stars/Forge/Shiv/Seal/Haste/Renew/Retaliate) produce
        // the right prose shape so designers can hand-polish after.

        public string BuildDescription()
        {
            if (effects == null || effects.Length == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < effects.Length; i++)
            {
                string clause = BuildEffectClause(effects[i], isFirst: i == 0);
                if (string.IsNullOrEmpty(clause)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(clause);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Structured-token description used by DescriptionRenderer to produce
        /// rich text with hoverable keywords + live-evaluated numbers. Returns
        /// the same prose as BuildDescription() but tokenized — designers can
        /// keep authoring effects[]; runtime UI binds via the renderer instead
        /// of reading the plain string.
        /// </summary>
        public List<DescriptionToken> BuildDescriptionTokens()
        {
            return DescriptionTokenizer.BuildSkillTokens(this);
        }

        private string BuildEffectClause(SkillEffectData e, bool isFirst)
        {
            string tgt = TargetPhrase(e.targetMode);

            switch (e.effectType)
            {
                case SkillEffectType.Attack:
                    string verb = diceRule == SkillDiceRule.AttackRoll ? "Attack" : "Deal";
                    return $"{verb} {tgt} for {DamageExpr(e)} damage.";

                case SkillEffectType.Apply:
                    return e.applyKind == ApplyKind.Damage
                        ? $"Deal {DamageExpr(e)} direct damage to {tgt}."
                        : BuildConditionClause(e, tgt, afflict: false);

                case SkillEffectType.Afflict:
                    return BuildConditionClause(e, tgt, afflict: true);

                case SkillEffectType.DirectDamage:  // legacy
                    return $"Deal {DamageExpr(e)} direct damage to {tgt}.";

                case SkillEffectType.Heal:
                    return $"Heal {tgt} for {e.magnitude} HP.";

                case SkillEffectType.RestoreSP:
                    return $"Restore {e.magnitude} SP to {tgt}.";

                case SkillEffectType.ApplyCondition:  // legacy
                    return BuildConditionClause(e, tgt, afflict: diceRule == SkillDiceRule.WilSave);

                case SkillEffectType.RemoveCondition:
                    return $"Remove {e.conditionID} from {tgt}.";

                case SkillEffectType.Move:
                    return BuildMoveClause(e);

                case SkillEffectType.LoseHP:
                    return $"Lose {e.magnitude} HP.";

                case SkillEffectType.Haste:
                    return e.extraActionKind == ExtraActionKind.Renew
                        ? $"Renew {tgt}."
                        : $"Haste {tgt}.";

                case SkillEffectType.Renew:
                    return $"Renew {tgt}.";

                case SkillEffectType.SealSkill:
                    return e.sealTarget switch
                    {
                        SealTargetKind.SelfSkill   => $"Seal {e.sealDuration}.",
                        SealTargetKind.ChosenSkill => $"Seal a skill for {e.sealDuration} turn(s).",
                        SealTargetKind.RandomSkill => $"Seal a random skill for {e.sealDuration} turn(s).",
                        _ => $"Seal a skill for {e.sealDuration} turn(s).",
                    };

                case SkillEffectType.GenerateItem:
                    string itemName = e.pouchItemType == PouchItemType.None ? "items" : e.pouchItemType.ToString();
                    return $"Generate {e.itemQuantity} {itemName}.";

                case SkillEffectType.ChannelOrb:
                    return BuildChannelClause(e);

                case SkillEffectType.EvokeOrb:
                    return e.evokeKind switch
                    {
                        EvokeKind.First    => "Evoke your rightmost Orb.",
                        EvokeKind.Leftmost => "Evoke your leftmost Orb.",
                        EvokeKind.All      => "Evoke all your Orbs.",
                        _ => "Evoke an Orb.",
                    };

                case SkillEffectType.SummonCompanion:
                    return $"Summon {e.magnitude}.";

                case SkillEffectType.BindCompanion:
                    return $"Bind Osty to {tgt} for {e.bindDuration} turn(s).";

                case SkillEffectType.GainStars:
                    return $"Gain {e.resourceAmount} Star(s).";

                case SkillEffectType.Forge:
                    return $"Forge {e.resourceAmount}.";

                case SkillEffectType.Retaliate:
                    return $"Until start of your next turn, if an enemy attacks you, " +
                           $"they take {e.retaliateDamage} damage.";

                case SkillEffectType.OnAllyAttackRider:
                    return $"Until start of your next turn, gain {e.magnitude} Guard " +
                           $"whenever an ally attacks an enemy.";

                default: return "";
            }
        }

        /// <summary>Formats damage magnitude with its stat bonus suffix ("8+POW", "4", "3+DEX").</summary>
        private static string DamageExpr(SkillEffectData e) => e.damageStat switch
        {
            DamageStat.POW => $"{e.magnitude}+POW",
            DamageStat.DEX => $"{e.magnitude}+DEX",
            DamageStat.WIL => $"{e.magnitude}+WIL",
            _              => e.magnitude.ToString(),
        };

        private string BuildConditionClause(SkillEffectData e, string tgt, bool afflict)
        {
            string cond = e.conditionID.ToString();
            int n = e.conditionStacks > 0 ? e.conditionStacks : Mathf.Max(1, e.magnitude);
            int per = Mathf.Max(1, e.stackPer);

            string amount = e.stackCountKind switch
            {
                ConditionStackSource.Fixed => n.ToString(),
                ConditionStackSource.UnblockedDamage => $"{n} per {per} unblocked damage",
                ConditionStackSource.DamageDealt     => $"{n} per {per} damage dealt",
                ConditionStackSource.CasterPOW       => $"{n} per {per} POW",
                ConditionStackSource.TargetStacks    => $"{n} per {per} {cond} on target",
                _ => n.ToString(),
            };

            if (afflict)
                return $"Afflict {tgt} to apply {amount} {cond}.";
            return $"Apply {amount} {cond} to {tgt}.";
        }

        private string BuildMoveClause(SkillEffectData e)
        {
            string save = e.saveDC > 0 ? $" (WIL DC {e.saveDC} resists)" : "";
            return e.movementKind switch
            {
                MovementKind.Advance   => $"Advance {e.movementMagnitude}.",
                MovementKind.Withdraw  => $"Withdraw {e.movementMagnitude}.",
                MovementKind.Pull      => $"Pull {e.movementMagnitude}{save}.",
                MovementKind.Knockback => $"Knockback {e.movementMagnitude}{save}.",
                MovementKind.Shuffle   => $"Shuffle{save}.",
                _ => "",
            };
        }

        private string BuildChannelClause(SkillEffectData e)
        {
            string orbName = e.orbType == OrbType.Random
                ? "random Orb"
                : $"{e.orbType} Orb";
            string source = e.orbSource switch
            {
                OrbSource.TargetAlly => "An ally channels",
                OrbSource.PerEnemy   => $"Channel 1 {orbName} for each enemy in combat",
                _                    => $"Channel {e.orbCount} {orbName}{(e.orbCount > 1 ? "s" : "")}",
            };
            return source.EndsWith(".") ? source : source + ".";
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
    }
}
