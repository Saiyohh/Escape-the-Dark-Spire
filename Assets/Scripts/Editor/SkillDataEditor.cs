// SkillDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for SkillData. Renders fields in logical sections and —
// critically — draws each effect in the effects[] array with ONLY the fields
// relevant to its SkillEffectType. Designers authoring an Attack skill don't
// see Orb / Osty / Forge / Shiv fields they'll never use.
//
// Also shows a live "generated description" preview computed from the effect
// list using SkillData.BuildDescription(). A "Copy to Description" button
// copies the generated text into the human-authored description field so
// designers can hand-polish after templating.
//
// Effects are rendered in collapsible boxes grouped by SkillEffectCategory
// (Core / SelfCost / ActionEconomy / Item / Orb / Companion / Resource /
// Triggered). Placeholder effect types include a blue help box saying
// "Runtime not implemented — inspector fields describe intent only."
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(SkillData))]
    public class SkillDataEditor : Editor
    {
        private SerializedProperty skillNameProp, descriptionProp, artBannerProp;
        private SerializedProperty bannerFocalYProp, bannerFocalXProp, bannerGrayscaleProp;
        private SerializedProperty bwRedsProp, bwYellowsProp, bwGreensProp, bwCyansProp, bwBluesProp, bwMagentasProp;
        private SerializedProperty spCostProp, altCostTypeProp, altCostAmountProp;
        private SerializedProperty actionCostTypeProp, diceRuleProp, tagsProp;
        private SerializedProperty primaryTargetModeProp, effectsProp, targetPickCountProp;
        private SerializedProperty rangeMinProp, rangeMaxProp, rangeDisplayProp;
        private SerializedProperty alignmentProp, rarityProp;
        private SerializedProperty upgradedVersionProp, masteryVersionProp;
        private SerializedProperty masteryThresholdProp, maxUpgradeTierProp, shopCostProp;

        private bool showMetaFoldout = false;

        private static readonly Color[] CategoryColors =
        {
            new Color(0.78f, 0.82f, 0.95f), // Core — cool blue
            new Color(0.98f, 0.75f, 0.65f), // SelfCost — warm red
            new Color(0.80f, 0.95f, 0.82f), // ActionEconomy — green
            new Color(0.95f, 0.90f, 0.65f), // Item — yellow
            new Color(0.78f, 0.92f, 0.98f), // Orb — cyan
            new Color(0.90f, 0.80f, 0.95f), // Companion — purple
            new Color(0.95f, 0.85f, 0.70f), // Resource — tan (Forge/Stars)
            new Color(0.95f, 0.80f, 0.85f), // Triggered — pink
        };

        private void OnEnable()
        {
            skillNameProp        = serializedObject.FindProperty("skillName");
            descriptionProp      = serializedObject.FindProperty("description");
            artBannerProp        = serializedObject.FindProperty("artBanner");
            bannerFocalYProp     = serializedObject.FindProperty("bannerFocalY");
            bannerFocalXProp     = serializedObject.FindProperty("bannerFocalX");
            bannerGrayscaleProp  = serializedObject.FindProperty("bannerGrayscale");
            bwRedsProp           = serializedObject.FindProperty("bwReds");
            bwYellowsProp        = serializedObject.FindProperty("bwYellows");
            bwGreensProp         = serializedObject.FindProperty("bwGreens");
            bwCyansProp          = serializedObject.FindProperty("bwCyans");
            bwBluesProp          = serializedObject.FindProperty("bwBlues");
            bwMagentasProp       = serializedObject.FindProperty("bwMagentas");
            spCostProp           = serializedObject.FindProperty("spCost");
            altCostTypeProp      = serializedObject.FindProperty("altCostType");
            altCostAmountProp    = serializedObject.FindProperty("altCostAmount");
            actionCostTypeProp   = serializedObject.FindProperty("actionCostType");
            diceRuleProp         = serializedObject.FindProperty("diceRule");
            tagsProp             = serializedObject.FindProperty("tags");
            primaryTargetModeProp = serializedObject.FindProperty("primaryTargetMode");
            targetPickCountProp   = serializedObject.FindProperty("targetPickCount");
            effectsProp          = serializedObject.FindProperty("effects");
            rangeMinProp         = serializedObject.FindProperty("rangeMin");
            rangeMaxProp         = serializedObject.FindProperty("rangeMax");
            rangeDisplayProp     = serializedObject.FindProperty("rangeDisplay");
            alignmentProp        = serializedObject.FindProperty("alignment");
            rarityProp           = serializedObject.FindProperty("rarity");
            upgradedVersionProp  = serializedObject.FindProperty("upgradedVersion");
            masteryVersionProp   = serializedObject.FindProperty("masteryVersion");
            masteryThresholdProp = serializedObject.FindProperty("masteryThreshold");
            maxUpgradeTierProp   = serializedObject.FindProperty("maxUpgradeTier");
            shopCostProp         = serializedObject.FindProperty("shopCost");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();
            DrawIdentitySection();
            DrawCostSection();
            DrawCoreMechanicSection();
            DrawRangeSection();
            DrawTagsSection();
            DrawEffectsSection();
            DrawGeneratedDescriptionSection();
            DrawMetaSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Top summary (character / tier / dice rule / cost / range chips) ──
        private void DrawTopSummary()
        {
            var skill = (SkillData)target;

            EditorGUILayout.Space(4);
            EditorStyleKit.BeginBadgeRow();
            EditorStyleKit.DrawBadge(skill.alignment.ToString(),
                EditorStyleKit.CharacterSignature(skill.alignment));
            EditorStyleKit.DrawBadge(skill.rarity.ToString(),
                EditorStyleKit.TierColor(skill.rarity));
            EditorStyleKit.DrawBadge(PrettyDiceRule(skill.diceRule),
                EditorStyleKit.DiceRuleColor(skill.diceRule));
            EditorStyleKit.DrawBadge(skill.actionCostType == ActionCostType.FreeAction ? "Free Action" : "Action",
                EditorStyleKit.ActionCostColor(skill.actionCostType));
            EditorStyleKit.DrawBadge(
                string.IsNullOrEmpty(skill.rangeDisplay) ? $"{skill.rangeMin}-{skill.rangeMax}" : skill.rangeDisplay,
                new Color(0.45f, 0.45f, 0.45f));
            EditorStyleKit.DrawBadge($"SP {skill.spCost}", new Color(0.30f, 0.50f, 0.80f));
            EditorStyleKit.EndBadgeRow();
        }

        private static string PrettyDiceRule(SkillDiceRule r) => r switch
        {
            SkillDiceRule.AttackRoll => "Attack Roll",
            SkillDiceRule.AutoHit    => "Auto-Hit",
            SkillDiceRule.WilSave    => "WIL Save",
            SkillDiceRule.Passive    => "Passive",
            _ => r.ToString(),
        };

        // ── Identity ──────────────────────────────────────────────────────────
        private void DrawIdentitySection()
        {
            var skill = (SkillData)target;
            EditorStyleKit.DrawCharacterSectionHeader("Identity", skill.alignment);
            EditorGUILayout.PropertyField(skillNameProp);
            EditorGUILayout.PropertyField(artBannerProp,
                new GUIContent("Art Banner", "Wide artwork panel shown on the skill card. " +
                               "Leave empty to fall back to the global placeholder."));

            DrawBannerFocalPreview(skill);

            EditorGUILayout.PropertyField(descriptionProp);
        }

        /// <summary>
        /// Preview the banner masked to the same 5:1 aspect the runtime
        /// SkillInfoPanel uses, with a slider that drives bannerFocalY. Lets
        /// designers dial each skill's focal point without entering Play Mode.
        /// </summary>
        private void DrawBannerFocalPreview(SkillData skill)
        {
            var banner = skill != null ? skill.GetArtBanner() : null;
            if (banner == null || banner.texture == null) return;

            EditorGUILayout.Space(2);

            // Preview box — matches the runtime info-panel mask aspect.
            // 240:230 ≈ 1.04 (the user's current SkillInfoPanel mask).
            const float previewW = 240f;
            const float previewH = 230f;
            var rect = GUILayoutUtility.GetRect(previewW, previewH,
                GUILayout.Width(previewW), GUILayout.Height(previewH));

            // Background + border.
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
            var border = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), border);

            // Compute UV window using cover-fit math: sprite scales to fully
            // fill the mask (cropping the larger axis), focalX / focalY pick
            // which slice shows when an axis overflows.
            float spriteW = banner.rect.width;
            float spriteH = banner.rect.height;
            float spriteAspect = spriteW / spriteH;  // w/h, > 1 = wide sprite
            float maskAspect   = previewW / previewH;
            float focalX = bannerFocalXProp != null ? bannerFocalXProp.floatValue : 0f;
            float focalY = bannerFocalYProp != null ? bannerFocalYProp.floatValue : 0f;

            float uvW01, uvH01;
            if (spriteAspect > maskAspect)
            {
                // Sprite is wider than mask — fill height, crop width.
                uvH01 = 1f;
                uvW01 = maskAspect / spriteAspect;
            }
            else
            {
                // Sprite is squarer/taller than mask — fill width, crop height.
                uvW01 = 1f;
                uvH01 = spriteAspect / maskAspect;
            }

            // Normalized center inside the sprite [0..1] for each axis.
            float centerXN = 0.5f + Mathf.Clamp(focalX, -1f, 1f) * (0.5f - uvW01 * 0.5f);
            float centerYN = 0.5f + Mathf.Clamp(focalY, -1f, 1f) * (0.5f - uvH01 * 0.5f);

            float uvX = centerXN - uvW01 * 0.5f;
            float uvY = centerYN - uvH01 * 0.5f;

            // Convert sprite-space UV to texture-space UV (handles atlased sprites).
            var tex = banner.texture;
            var spriteRect = banner.rect;
            float texW = tex.width;
            float texH = tex.height;
            float texX = (spriteRect.x + uvX * spriteRect.width)  / texW;
            float texY = (spriteRect.y + uvY * spriteRect.height) / texH;
            float texCW = uvW01 * spriteRect.width  / texW;
            float texCH = uvH01 * spriteRect.height / texH;

            var texCoords = new Rect(texX, texY, texCW, texCH);

            // B&W preview path: Graphics.DrawTexture supports a material
            // override, but must run during a Repaint event. Fall back to the
            // normal colored path when the flag is off or the material can't
            // be found (missing shader, etc.).
            bool wantBW = bannerGrayscaleProp != null && bannerGrayscaleProp.boolValue;
            var bwMat = wantBW ? GetBlackAndWhiteMaterial() : null;

            if (wantBW && bwMat != null)
            {
                // Push the current slider values into the preview material every
                // repaint so dragging a slider updates the preview live.
                bwMat.SetFloat(PropWR, bwRedsProp    != null ? bwRedsProp.floatValue    : 0.40f);
                bwMat.SetFloat(PropWY, bwYellowsProp != null ? bwYellowsProp.floatValue : 0.60f);
                bwMat.SetFloat(PropWG, bwGreensProp  != null ? bwGreensProp.floatValue  : 0.40f);
                bwMat.SetFloat(PropWC, bwCyansProp   != null ? bwCyansProp.floatValue   : 0.60f);
                bwMat.SetFloat(PropWB, bwBluesProp   != null ? bwBluesProp.floatValue   : 0.20f);
                bwMat.SetFloat(PropWM, bwMagentasProp!= null ? bwMagentasProp.floatValue: 0.80f);
            }

            if (wantBW && bwMat != null && Event.current.type == EventType.Repaint)
            {
                Graphics.DrawTexture(
                    rect, tex, texCoords,
                    leftBorder: 0, rightBorder: 0, topBorder: 0, bottomBorder: 0,
                    color: Color.white, mat: bwMat);
            }
            else
            {
                GUI.DrawTextureWithTexCoords(rect, tex, texCoords, alphaBlend: true);
            }

            // Focal sliders — only the axis with overflow does anything, but
            // we expose both since either may apply depending on mask aspect.
            if (bannerFocalXProp != null)
            {
                EditorGUILayout.Slider(bannerFocalXProp, -1f, 1f,
                    new GUIContent("Banner Focal X",
                        "Horizontal crop focus when the sprite is wider than the " +
                        "mask after cover-fit. +1 = right edge, 0 = center, -1 = left."));
            }
            if (bannerFocalYProp != null)
            {
                EditorGUILayout.Slider(bannerFocalYProp, -1f, 1f,
                    new GUIContent("Banner Focal Y",
                        "Vertical crop focus when the sprite is taller than the " +
                        "mask after cover-fit. +1 = top, 0 = center, -1 = bottom."));
            }

            // Black & White toggle + per-channel weights.
            if (bannerGrayscaleProp != null)
            {
                EditorGUILayout.PropertyField(bannerGrayscaleProp,
                    new GUIContent("Black & White",
                        "Render the banner in B&W. Weights below control how " +
                        "each hue family contributes to the grayscale output — " +
                        "matches Photoshop's Black & White adjustment."));
            }

            if (wantBW)
            {
                EditorGUI.indentLevel++;
                DrawBWSlider(bwRedsProp,     "Reds");
                DrawBWSlider(bwYellowsProp,  "Yellows");
                DrawBWSlider(bwGreensProp,   "Greens");
                DrawBWSlider(bwCyansProp,    "Cyans");
                DrawBWSlider(bwBluesProp,    "Blues");
                DrawBWSlider(bwMagentasProp, "Magentas");
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>Slider that shows percentage to mirror Photoshop's UI (1.00 = 100%).</summary>
        private static void DrawBWSlider(SerializedProperty prop, string label)
        {
            if (prop == null) return;
            EditorGUILayout.Slider(prop, 0f, 3f, new GUIContent(label));
        }

        // Shader.PropertyToID lookups — cached per-editor-instance. (Static so the
        // preview material can be re-bound after domain reload without re-querying.)
        private static readonly int PropWR = Shader.PropertyToID("_WR");
        private static readonly int PropWY = Shader.PropertyToID("_WY");
        private static readonly int PropWG = Shader.PropertyToID("_WG");
        private static readonly int PropWC = Shader.PropertyToID("_WC");
        private static readonly int PropWB = Shader.PropertyToID("_WB");
        private static readonly int PropWM = Shader.PropertyToID("_WM");

        /// <summary>
        /// Lazy-cached B&W material for the editor preview. Created once per
        /// domain reload and kept hidden from the project. Returns null if the
        /// shader file isn't in the project (preview falls back to full-color).
        /// </summary>
        private static Material _bwPreviewMaterial;
        private static Material GetBlackAndWhiteMaterial()
        {
            if (_bwPreviewMaterial != null) return _bwPreviewMaterial;

            var shader = Shader.Find("DarkSpire/UI/BlackAndWhite");
            if (shader == null) return null;

            _bwPreviewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _bwPreviewMaterial;
        }

        // ── Cost ──────────────────────────────────────────────────────────────
        private void DrawCostSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Cost",
                EditorStyleKit.ActionCostColor((ActionCostType)actionCostTypeProp.enumValueIndex));
            EditorGUILayout.PropertyField(spCostProp);
            EditorGUILayout.PropertyField(actionCostTypeProp);
            EditorGUILayout.PropertyField(altCostTypeProp);
            if (altCostTypeProp.enumValueIndex != (int)AltCostType.None)
                EditorGUILayout.PropertyField(altCostAmountProp);
        }

        // ── Core mechanic ─────────────────────────────────────────────────────
        // Order: Target first, then Dice Rule. If Target = Self, the Dice Rule
        // is force-set to AutoHit (no point in rolling to hit yourself) and
        // disabled in the UI. Range is similarly force-set to 0–0 and disabled
        // (self-targeting doesn't use range).
        private void DrawCoreMechanicSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Dice Rule & Targeting",
                EditorStyleKit.DiceRuleColor((SkillDiceRule)diceRuleProp.enumValueIndex));

            // Target first.
            EditorGUILayout.PropertyField(primaryTargetModeProp, new GUIContent("Target"));
            var target = (TargetMode)primaryTargetModeProp.enumValueIndex;
            bool isSelf = target == TargetMode.Self;
            bool isPickable = target == TargetMode.SingleEnemy || target == TargetMode.SingleAlly;

            // Pick count — only meaningful for modes the player actually clicks on.
            if (isPickable && targetPickCountProp != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetPickCountProp,
                    new GUIContent("Picks",
                        "How many separate targets the player picks when casting this skill. " +
                        "1 (default) = all SingleEnemy/SingleAlly effects share one pick. " +
                        ">1 = each effect can claim a different pick via its Pick # field " +
                        "(e.g. Mortar: 3 attacks on 3 enemies in range)."));
                if (targetPickCountProp.intValue < 1) targetPickCountProp.intValue = 1;
                EditorGUI.indentLevel--;
            }
            else if (targetPickCountProp != null && targetPickCountProp.intValue != 1)
            {
                // Normalize — only SingleEnemy/SingleAlly use the multi-pick flow.
                targetPickCountProp.intValue = 1;
            }

            // Auto-coerce Dice Rule on Self — must be AutoHit.
            if (isSelf && (SkillDiceRule)diceRuleProp.enumValueIndex != SkillDiceRule.AutoHit)
                diceRuleProp.enumValueIndex = (int)SkillDiceRule.AutoHit;

            using (new EditorGUI.DisabledScope(isSelf))
            {
                EditorGUILayout.PropertyField(diceRuleProp);
            }

            var rule = (SkillDiceRule)diceRuleProp.enumValueIndex;
            if (isSelf)
            {
                EditorGUILayout.HelpBox(
                    "Self-targeted skills auto-use AutoHit (no attack roll against yourself) " +
                    "and ignore Range (always 0). Both fields are locked while Target = Self.",
                    MessageType.None);
            }
            else if (rule == SkillDiceRule.Passive)
            {
                EditorGUILayout.HelpBox(
                    "Passive skills don't execute on Play. They subscribe to events " +
                    "(future). SkillResolver currently logs a warning and does nothing.",
                    MessageType.Info);
            }
        }

        // ── Range ─────────────────────────────────────────────────────────────
        private void DrawRangeSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Range", new Color(0.45f, 0.45f, 0.45f));

            bool isSelf = (TargetMode)primaryTargetModeProp.enumValueIndex == TargetMode.Self;

            // Self-targeted skills have no range — force it to 0–0.
            if (isSelf)
            {
                if (rangeMinProp.intValue != 0) rangeMinProp.intValue = 0;
                if (rangeMaxProp.intValue != 0) rangeMaxProp.intValue = 0;
                if (rangeDisplayProp.stringValue != "—") rangeDisplayProp.stringValue = "—";
            }

            const float Gap = 6f;
            float totalWidth = EditorGUIUtility.currentViewWidth - 32f;
            float colWidth = Mathf.Max(120f, (totalWidth - Gap) * 0.5f);

            using (new EditorGUI.DisabledScope(isSelf))
            {
                EditorGUILayout.BeginHorizontal();
                DrawIntPairHalf(rangeMinProp, "Min", colWidth);
                GUILayout.Space(Gap);
                DrawIntPairHalf(rangeMaxProp, "Max", colWidth);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(rangeDisplayProp, new GUIContent("Display"));
            }
        }

        /// <summary>
        /// Draws a single int PropertyField inside a fixed-width horizontal cell
        /// with a narrow label, so two of these placed side-by-side line up.
        /// </summary>
        private static void DrawIntPairHalf(SerializedProperty prop, string label, float width)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            float prev = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 38f;
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            EditorGUIUtility.labelWidth = prev;
            EditorGUILayout.EndHorizontal();
        }

        // ── Tags ──────────────────────────────────────────────────────────────
        private void DrawTagsSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Tags", new Color(0.5f, 0.55f, 0.45f));

            // PropertyField on a [System.Flags] enum renders as a single-select
            // dropdown — a long-standing IMGUI quirk. EnumFlagsField gives the
            // proper multi-pick mask popup so designers can combine tags.
            var currentTags = (SkillTag)tagsProp.intValue;
            EditorGUI.BeginChangeCheck();
            var newTags = (SkillTag)EditorGUILayout.EnumFlagsField(
                new GUIContent("Mechanical Tags"), currentTags);
            if (EditorGUI.EndChangeCheck())
                tagsProp.intValue = (int)newTags;

            DrawTagChipsRow();
        }

        /// <summary>Renders the currently-selected tags as colored chips for quick scanning.</summary>
        private void DrawTagChipsRow()
        {
            var tags = (SkillTag)tagsProp.intValue;
            if (tags == SkillTag.None) return;

            EditorGUILayout.Space(2);
            EditorStyleKit.BeginBadgeRow();
            foreach (SkillTag t in System.Enum.GetValues(typeof(SkillTag)))
            {
                if (t == SkillTag.None) continue;
                if ((tags & t) == 0) continue;
                EditorStyleKit.DrawBadge(t.ToString(), TagColor(t));
            }
            EditorStyleKit.EndBadgeRow();
        }

        private static Color TagColor(SkillTag t) => t switch
        {
            SkillTag.Strike        => new Color(0.82f, 0.32f, 0.30f),
            SkillTag.Defend        => new Color(0.32f, 0.58f, 0.86f),
            SkillTag.Exhaust       => new Color(0.55f, 0.45f, 0.38f),
            SkillTag.Forge         => new Color(0.90f, 0.55f, 0.25f),
            SkillTag.StarGenerator => new Color(0.92f, 0.72f, 0.30f),
            SkillTag.OstyAttack    => new Color(0.62f, 0.41f, 0.78f),
            SkillTag.Orb           => new Color(0.35f, 0.78f, 0.92f),
            SkillTag.OnHit         => new Color(0.90f, 0.50f, 0.45f),
            SkillTag.Positional    => new Color(0.50f, 0.62f, 0.45f),
            SkillTag.WilSave       => new Color(0.62f, 0.41f, 0.78f),
            _                      => new Color(0.55f, 0.55f, 0.55f),
        };

        // ── Effects list with conditional fields ──────────────────────────────
        private void DrawEffectsSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Effects", new Color(0.40f, 0.60f, 0.45f));
            EffectsListDrawer.Draw(effectsProp, targetPickCountProp);
        }


        // ── Generated description preview ─────────────────────────────────────
        private void DrawGeneratedDescriptionSection()
        {
            var skill = (SkillData)target;
            EditorStyleKit.DrawColoredSectionHeader("Generated Description (preview)",
                new Color(0.55f, 0.55f, 0.75f));

            string generated = skill.BuildDescription();

            EditorGUILayout.BeginHorizontal();
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(generated) ? "(no effects yet)" : generated,
                style, GUILayout.MinHeight(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to Description", GUILayout.Height(22)))
            {
                descriptionProp.stringValue = generated;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(22), GUILayout.Width(160)))
            {
                EditorGUIUtility.systemCopyBuffer = generated;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Meta (collapsible) ────────────────────────────────────────────────
        private void DrawMetaSection()
        {
            EditorGUILayout.Space(6);
            showMetaFoldout = EditorGUILayout.Foldout(showMetaFoldout, "Meta / Progression", true);
            if (!showMetaFoldout) return;

            EditorGUILayout.PropertyField(alignmentProp);
            EditorGUILayout.PropertyField(rarityProp);
            EditorGUILayout.PropertyField(masteryThresholdProp);
            EditorGUILayout.PropertyField(maxUpgradeTierProp);
            EditorGUILayout.PropertyField(shopCostProp);
            EditorGUILayout.PropertyField(upgradedVersionProp);
            EditorGUILayout.PropertyField(masteryVersionProp);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SectionHeader(string text)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            var r = GUILayoutUtility.GetLastRect();
            r.y += r.height - 1; r.height = 1;
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }
    }
}
