// EditorStyleKit.cs
// -----------------------------------------------------------------------------
// Shared color palette + drawing helpers for the custom inspectors on Skills,
// Conditions, and Weapons. Keeps the look consistent: same tier badge colors
// across skills, same buff/debuff pill style across conditions, same section
// header treatment everywhere.
//
// Runs editor-only — guarded by #if UNITY_EDITOR in the enclosing Editor asmdef.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class EditorStyleKit
    {
        // ═════════════════════════════════════════════════════════════════════
        //  Palette
        // ═════════════════════════════════════════════════════════════════════

        // Character colors are now authored per-CharacterData SO, resolved
        // through CharacterLibrary at runtime and edit time. See
        // CharacterSignature / CharacterHighlight below. CharacterPalette
        // holds the defaults when no SO is registered for an alignment.
        public static readonly Color Neutral     = new(0.60f, 0.60f, 0.60f);  // gray

        // Rarity / tier. Matches the Rarity enum:
        // Starter, Common, Uncommon, Rare, Shop, Legendary.
        public static readonly Color TierStarter   = new(0.60f, 0.60f, 0.60f); // gray
        public static readonly Color TierCommon    = new(0.80f, 0.80f, 0.80f); // light gray
        public static readonly Color TierUncommon  = new(0.45f, 0.75f, 0.50f); // green
        public static readonly Color TierRare      = new(0.32f, 0.58f, 0.86f); // blue
        public static readonly Color TierShop      = new(0.35f, 0.72f, 0.72f); // teal
        public static readonly Color TierLegendary = new(0.95f, 0.78f, 0.28f); // gold

        // Dice rule semantics.
        public static readonly Color DiceAttackRoll = new(0.80f, 0.30f, 0.28f);  // aggressive red
        public static readonly Color DiceAutoHit    = new(0.35f, 0.70f, 0.42f);  // reliable green
        public static readonly Color DiceWilSave    = new(0.62f, 0.41f, 0.78f);  // contested purple
        public static readonly Color DicePassive    = new(0.55f, 0.55f, 0.55f);  // inactive gray

        // Action-cost.
        public static readonly Color CostAction     = new(0.40f, 0.56f, 0.86f);  // blue (main action)
        public static readonly Color CostFreeAction = new(0.95f, 0.62f, 0.26f);  // orange (free action)

        // Buff/debuff pills for conditions.
        public static readonly Color Buff   = new(0.30f, 0.70f, 0.40f);
        public static readonly Color Debuff = new(0.82f, 0.30f, 0.30f);

        // Clear timing (when a condition auto-clears).
        public static readonly Color ClearNever         = new(0.45f, 0.45f, 0.45f);
        public static readonly Color ClearOwnerTurn     = new(0.95f, 0.62f, 0.26f); // orange — personal reset
        public static readonly Color ClearRoundStart    = new(0.32f, 0.58f, 0.86f); // blue — round-scoped
        public static readonly Color ClearRoundEnd      = new(0.45f, 0.72f, 0.85f); // lighter blue

        // Stack type.
        public static readonly Color StackCounter  = new(0.30f, 0.58f, 0.90f);
        public static readonly Color StackDuration = new(0.62f, 0.41f, 0.78f);
        public static readonly Color StackSingle   = new(0.95f, 0.62f, 0.26f);

        // Effect categories (Skills) — matches SkillEffectCategory.
        public static readonly Color CatCore           = new(0.45f, 0.58f, 0.88f);
        public static readonly Color CatSelfCost       = new(0.88f, 0.42f, 0.38f);
        public static readonly Color CatActionEconomy  = new(0.48f, 0.78f, 0.50f);
        public static readonly Color CatItem           = new(0.90f, 0.80f, 0.35f);
        public static readonly Color CatOrb            = new(0.35f, 0.78f, 0.92f);
        public static readonly Color CatCompanion      = new(0.70f, 0.48f, 0.88f);
        public static readonly Color CatResource       = new(0.85f, 0.62f, 0.35f);
        public static readonly Color CatTriggered      = new(0.88f, 0.52f, 0.72f);

        // Trigger-event groupings (Conditions).
        public static readonly Color EvTurnRound   = new(0.62f, 0.41f, 0.78f); // purple
        public static readonly Color EvDamage      = new(0.82f, 0.32f, 0.30f); // red
        public static readonly Color EvAttack      = new(0.92f, 0.58f, 0.26f); // orange
        public static readonly Color EvCondition   = new(0.85f, 0.75f, 0.30f); // yellow
        public static readonly Color EvLifecycle   = new(0.55f, 0.45f, 0.38f); // brown
        public static readonly Color EvSkill       = new(0.40f, 0.72f, 0.50f); // green

        // Trigger-action groupings (within Conditions).
        public static readonly Color ActDamage      = new(0.82f, 0.32f, 0.30f);
        public static readonly Color ActHeal        = new(0.40f, 0.78f, 0.50f);
        public static readonly Color ActDefense     = new(0.32f, 0.58f, 0.86f);
        public static readonly Color ActModify      = new(0.35f, 0.78f, 0.92f);
        public static readonly Color ActConditionOp = new(0.62f, 0.41f, 0.78f);
        public static readonly Color ActKill        = new(0.35f, 0.20f, 0.22f);

        // ═════════════════════════════════════════════════════════════════════
        //  Drawing helpers
        // ═════════════════════════════════════════════════════════════════════

        private static GUIStyle _badgeStyle;
        private static GUIStyle BadgeStyle => _badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 1, 1),
        };

        // Section-header sizing. Bumped globally so every "[Header]" strip
        // reads as a definite block boundary rather than another bold label.
        private const float SectionHeaderHeight = 30f;
        private const int   SectionHeaderFontSize = 15;
        private const float SectionHeaderBarWidth = 5f;
        private const float SectionHeaderSpaceAbove = 10f;

        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle SectionHeaderStyle => _sectionHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = SectionHeaderFontSize,
            padding = new RectOffset(8, 8, 4, 4),
            alignment = TextAnchor.MiddleLeft,
        };

        /// <summary>
        /// Draws a small colored pill/badge with centered bold text. Inline with
        /// whatever horizontal layout it's placed in.
        /// </summary>
        public static void DrawBadge(string text, Color bg, float minWidth = 0f, Color? textColor = null)
        {
            var content = new GUIContent(text);
            var size = BadgeStyle.CalcSize(content);
            float w = Mathf.Max(size.x + 4f, minWidth);
            var rect = GUILayoutUtility.GetRect(w, 18f, GUILayout.Width(w), GUILayout.Height(18f));

            // Subtle inset so badges don't jam together
            rect.x += 2; rect.width -= 2;

            EditorGUI.DrawRect(rect, bg);
            // Thin darker outline for legibility on light inspector themes
            var edge = new Color(0f, 0f, 0f, 0.25f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), edge);

            var fg = textColor ?? IdealTextFor(bg);
            var s = new GUIStyle(BadgeStyle) { normal = { textColor = fg } };
            GUI.Label(rect, content, s);
        }

        /// <summary>Space-separated row of badges, left-aligned.</summary>
        public static void BeginBadgeRow() => EditorGUILayout.BeginHorizontal();
        public static void EndBadgeRow()   { GUILayout.FlexibleSpace(); EditorGUILayout.EndHorizontal(); }

        /// <summary>Full-width colored header strip (thicker than a plain bold label).</summary>
        public static void DrawColoredSectionHeader(string text, Color stripColor)
        {
            EditorGUILayout.Space(SectionHeaderSpaceAbove);
            DrawHeaderStripInternal(text, stripColor, stripColor, 0.14f);
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// Shared internal — draws the header strip at the current layout
        /// position WITHOUT any leading/trailing Space calls. Used by both the
        /// standalone DrawColoredSectionHeader/DrawCharacterSectionHeader AND
        /// the BeginBoxSection helpers (which handle spacing themselves).
        /// </summary>
        private static void DrawHeaderStripInternal(string text, Color barColor, Color tintColor, float tintAlpha)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, SectionHeaderStyle,
                GUILayout.Height(SectionHeaderHeight));

            // Left color bar
            var bar = new Rect(rect.x, rect.y, SectionHeaderBarWidth, rect.height);
            EditorGUI.DrawRect(bar, barColor);

            // Strip background tint
            var tint = new Color(tintColor.r, tintColor.g, tintColor.b, tintAlpha);
            EditorGUI.DrawRect(
                new Rect(rect.x + SectionHeaderBarWidth, rect.y,
                         rect.width - SectionHeaderBarWidth, rect.height),
                tint);

            // Label
            var textRect = new Rect(rect.x + SectionHeaderBarWidth + 8f, rect.y,
                                    rect.width - SectionHeaderBarWidth - 10f, rect.height);
            GUI.Label(textRect, text, SectionHeaderStyle);

            // Top + bottom hairlines
            var edge = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), edge);
        }

        /// <summary>
        /// Character-flavored section header: signature color drives the left
        /// bar (strong), highlight color drives the strip background tint
        /// (soft). Uses both authored colors from CharacterData, giving each
        /// one a visibly distinct role in the inspector.
        /// </summary>
        public static void DrawCharacterSectionHeader(string text, Alignment a)
            => DrawCharacterSectionHeader(text, CharacterSignature(a), CharacterHighlight(a));

        /// <summary>
        /// Same look as the alignment-driven overload, but takes colors
        /// directly. Call this from CharacterDataEditor so the banner follows
        /// the SO's live signatureColor / highlightColor instead of going
        /// through CharacterLibrary (which caches and which may resolve to a
        /// *different* CharacterData sharing the alignment).
        /// </summary>
        public static void DrawCharacterSectionHeader(string text, Color signature, Color highlight)
        {
            EditorGUILayout.Space(SectionHeaderSpaceAbove);
            DrawHeaderStripInternal(text, signature, highlight, 0.32f);
            EditorGUILayout.Space(2);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Boxed sections — each section is a framed container with a header
        //  strip flush to the top and inset body content below. Use instead
        //  of DrawColoredSectionHeader when you want visually distinct blocks.
        //
        //  Usage:
        //    EditorStyleKit.BeginBoxSection("Base Stats", headerColor);
        //    EditorGUILayout.PropertyField(...);
        //    EditorStyleKit.EndBoxSection();
        // ═════════════════════════════════════════════════════════════════════

        private static GUIStyle _boxOuterStyle;
        private static GUIStyle BoxOuterStyle => _boxOuterStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(0, 0, 0, 0),  // header spans the whole box edge-to-edge
            margin  = new RectOffset(0, 0, 2, 2),
        };

        private static GUIStyle _boxBodyStyle;
        private static GUIStyle BoxBodyStyle => _boxBodyStyle ??= new GUIStyle
        {
            padding = new RectOffset(8, 8, 6, 8),  // breathing room around fields
            margin  = new RectOffset(0, 0, 0, 0),
        };

        /// <summary>
        /// Opens a boxed section. Pair with <see cref="EndBoxSection"/>. The
        /// header strip is drawn flush to the top edge of the box and the
        /// section's fields live inside a padded body below.
        /// </summary>
        public static void BeginBoxSection(string headerText, Color stripColor)
        {
            EditorGUILayout.Space(SectionHeaderSpaceAbove);
            EditorGUILayout.BeginVertical(BoxOuterStyle);
            DrawHeaderStripInternal(headerText, stripColor, stripColor, 0.14f);
            EditorGUILayout.BeginVertical(BoxBodyStyle);
        }

        /// <summary>
        /// Character-flavored boxed section: signature paints the left bar,
        /// highlight paints the strip tint. Pair with <see cref="EndBoxSection"/>.
        /// </summary>
        public static void BeginCharacterBoxSection(string headerText, Color signature, Color highlight)
        {
            EditorGUILayout.Space(SectionHeaderSpaceAbove);
            EditorGUILayout.BeginVertical(BoxOuterStyle);
            DrawHeaderStripInternal(headerText, signature, highlight, 0.32f);
            EditorGUILayout.BeginVertical(BoxBodyStyle);
        }

        /// <summary>Closes the current boxed section.</summary>
        public static void EndBoxSection()
        {
            EditorGUILayout.EndVertical(); // body
            EditorGUILayout.EndVertical(); // outer box
        }

        /// <summary>
        /// Draws an enum popup for a SerializedProperty with entries sorted
        /// alphabetically by name. Replaces PropertyField / EnumPopup when the
        /// declaration order isn't useful (ConditionID, etc.).
        /// </summary>
        public static void DrawSortedEnumPopup<TEnum>(SerializedProperty prop, string label)
            where TEnum : struct, System.Enum
        {
            var all = (TEnum[])System.Enum.GetValues(typeof(TEnum));

            // Sort alphabetically by enum member name.
            System.Array.Sort(all, (a, b) =>
                string.Compare(a.ToString(), b.ToString(), System.StringComparison.OrdinalIgnoreCase));

            var labels = new string[all.Length];
            for (int i = 0; i < all.Length; i++) labels[i] = all[i].ToString();

            int currentRawIndex = prop.enumValueIndex;
            var currentValue = (TEnum)(object)currentRawIndex;
            int sortedIndex = System.Array.IndexOf(all, currentValue);
            if (sortedIndex < 0) sortedIndex = 0;

            int newSortedIndex = EditorGUILayout.Popup(label, sortedIndex, labels);
            if (newSortedIndex != sortedIndex)
            {
                var chosen = all[newSortedIndex];
                prop.enumValueIndex = (int)(object)chosen;
            }
        }

        /// <summary>Plain bold label + thin horizontal rule (used when color isn't needed).</summary>
        public static void DrawSectionHeader(string text)
        {
            EditorGUILayout.Space(SectionHeaderSpaceAbove);
            EditorGUILayout.LabelField(text, SectionHeaderStyle,
                GUILayout.Height(SectionHeaderHeight));
            var r = GUILayoutUtility.GetLastRect();
            var edge = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            EditorGUI.DrawRect(new Rect(r.x, r.y,                 r.width, 1), edge);
            EditorGUI.DrawRect(new Rect(r.x, r.y + r.height - 1,  r.width, 1), edge);
            EditorGUILayout.Space(2);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Lookups: enum → color
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Signature color for an Alignment. Resolves authored CharacterData
        /// via CharacterLibrary first, falling back to CharacterPalette
        /// defaults when no SO exists. Use for strong surfaces: badges,
        /// section-header strips, name plates.
        /// </summary>
        public static Color CharacterSignature(Alignment a) => CharacterLibrary.Signature(a);

        /// <summary>
        /// Highlight color for an Alignment — the lighter variant of the
        /// signature. Use for soft surfaces: card backgrounds, selection
        /// glows, row tints.
        /// </summary>
        public static Color CharacterHighlight(Alignment a) => CharacterLibrary.Highlight(a);

        /// <summary>
        /// Back-compat alias. Existing inspectors that called CharacterColor
        /// wanted the strong/saturated version, so this maps to Signature.
        /// Prefer CharacterSignature in new code.
        /// </summary>
        public static Color CharacterColor(Alignment a) => CharacterSignature(a);

        public static Color TierColor(Rarity t) => t switch
        {
            Rarity.Starter   => TierStarter,
            Rarity.Common    => TierCommon,
            Rarity.Uncommon  => TierUncommon,
            Rarity.Rare      => TierRare,
            Rarity.Shop      => TierShop,
            Rarity.Legendary => TierLegendary,
            _                => Neutral,
        };

        public static Color DiceRuleColor(SkillDiceRule r) => r switch
        {
            SkillDiceRule.AttackRoll => DiceAttackRoll,
            SkillDiceRule.AutoHit    => DiceAutoHit,
            SkillDiceRule.WilSave    => DiceWilSave,
            SkillDiceRule.Passive    => DicePassive,
            _                        => Neutral,
        };

        public static Color ActionCostColor(ActionCostType c) => c switch
        {
            ActionCostType.Action     => CostAction,
            ActionCostType.FreeAction => CostFreeAction,
            _                         => Neutral,
        };

        public static Color StackTypeColor(ConditionStackType s) => s switch
        {
            ConditionStackType.Counter  => StackCounter,
            ConditionStackType.Duration => StackDuration,
            ConditionStackType.Single   => StackSingle,
            _                           => Neutral,
        };

        public static Color ClearTimingColor(ClearTiming t) => t switch
        {
            ClearTiming.Never          => ClearNever,
            ClearTiming.OwnerTurnStart => ClearOwnerTurn,
            ClearTiming.RoundStart     => ClearRoundStart,
            ClearTiming.RoundEnd       => ClearRoundEnd,
            _                          => Neutral,
        };

        public static Color EffectCategoryColor(SkillEffectCategory c) => c switch
        {
            SkillEffectCategory.Core          => CatCore,
            SkillEffectCategory.SelfCost      => CatSelfCost,
            SkillEffectCategory.ActionEconomy => CatActionEconomy,
            SkillEffectCategory.Item          => CatItem,
            SkillEffectCategory.Orb           => CatOrb,
            SkillEffectCategory.Companion     => CatCompanion,
            SkillEffectCategory.Resource      => CatResource,
            SkillEffectCategory.Triggered     => CatTriggered,
            _ => Neutral,
        };

        public static Color TriggerEventColor(TriggerEvent e) => e switch
        {
            TriggerEvent.OnTurnStart or TriggerEvent.OnTurnEnd
                or TriggerEvent.OnRoundStart or TriggerEvent.OnRoundEnd
                or TriggerEvent.OnPlayerPhaseStart or TriggerEvent.OnPlayerPhaseEnd
                or TriggerEvent.OnEnemyPhaseStart or TriggerEvent.OnEnemyPhaseEnd
                or TriggerEvent.OnCleanup                         => EvTurnRound,
            TriggerEvent.OnTakeDamagePre or TriggerEvent.OnTakeDamagePost
                or TriggerEvent.OnDealDamage                      => EvDamage,
            TriggerEvent.OnAttackRoll or TriggerEvent.OnHit
                or TriggerEvent.OnMiss                            => EvAttack,
            TriggerEvent.OnDebuffApplied or TriggerEvent.OnBuffApplied
                or TriggerEvent.OnConditionApplied                => EvCondition,
            TriggerEvent.OnCombatStart or TriggerEvent.OnCombatEnd
                or TriggerEvent.OnKill or TriggerEvent.OnDeath    => EvLifecycle,
            TriggerEvent.OnSkillPlayed                            => EvSkill,
            _ => Neutral,
        };

        public static Color TriggerActionColor(TriggerActionKind k)
        {
            switch (k)
            {
                case TriggerActionKind.DealDamage:
                case TriggerActionKind.DealDamagePerStack:
                case TriggerActionKind.DamageSource:
                case TriggerActionKind.DamageSourcePerStack:
                case TriggerActionKind.ModifyIncomingDamageFlat:
                case TriggerActionKind.ModifyIncomingDamagePercent:
                case TriggerActionKind.ModifyIncomingDamagePerStack:
                case TriggerActionKind.ModifyIncomingDamagePercentPerStack:
                case TriggerActionKind.ModifyOutgoingDamageFlat:
                case TriggerActionKind.ModifyOutgoingDamagePerStack:
                case TriggerActionKind.ModifyOutgoingDamagePercent:
                case TriggerActionKind.ModifyOutgoingDamagePercentPerStack:
                    return ActDamage;

                case TriggerActionKind.HealTarget:
                case TriggerActionKind.HealTargetPerStack:
                    return ActHeal;

                case TriggerActionKind.AbsorbDamage:
                case TriggerActionKind.AbsorbDamagePerStack:
                case TriggerActionKind.NegateIncomingEffect:
                case TriggerActionKind.GrantGuard:
                case TriggerActionKind.GrantGuardPerStack:
                    return ActDefense;

                case TriggerActionKind.ModifyAttackRollBy:
                case TriggerActionKind.ModifyAttackRollByPerStack:
                case TriggerActionKind.ModifyStat:
                    return ActModify;

                case TriggerActionKind.ApplyCondition:
                case TriggerActionKind.ApplyConditionPerStack:
                case TriggerActionKind.RemoveCondition:
                    return ActConditionOp;

                case TriggerActionKind.KillTarget:
                    return ActKill;
            }
            return Neutral;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Readability
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pick black or white text based on the background's perceptual luminance.
        /// Keeps badges readable regardless of the palette choice.
        /// </summary>
        public static Color IdealTextFor(Color bg)
        {
            // Rec.709 luma
            float luma = 0.2126f * bg.r + 0.7152f * bg.g + 0.0722f * bg.b;
            return luma > 0.55f ? Color.black : Color.white;
        }
    }
}
