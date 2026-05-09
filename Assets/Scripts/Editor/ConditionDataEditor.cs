// ConditionDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for ConditionData — the compositional condition model.
// Renders:
//   [Identity]           name, ID, display, description, icon, tint
//   [Stacking]           stackType, maxStacks, isDebuff, timing
//   [Presets]            one-click templates that seed common trigger patterns
//                        (Poison DoT, Plating fade, Shields absorb, Artifact
//                         negate, Dodge chance, Thorns retaliate, Regen heal)
//   [Passive Modifiers]  list of (stat, amountPerStack)
//   [Triggers]           list of (when, conditionals[], actions[], stackOp)
//                        where each trigger is a foldable card and each
//                        conditional/action only surfaces the fields that
//                        match its kind
//   [Structural Flags]   preventsAction / clearsAtTurnStart / defensePersists /
//                        bypassesShields / bypassesDEF
//
// Designers author new conditions entirely here. Unless the condition needs
// genuinely novel runtime logic (Burning's d20 doubling, Focus's orb scaling),
// no code change is required.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(ConditionData))]
    public class ConditionDataEditor : Editor
    {
        // Backing serialized props
        private SerializedProperty conditionIDProp, displayNameProp, descriptionProp;
        private SerializedProperty iconProp, tintColorProp;
        private SerializedProperty iconGrayscaleProp;
        private SerializedProperty iconBwRedsProp, iconBwYellowsProp, iconBwGreensProp,
                                   iconBwCyansProp, iconBwBluesProp, iconBwMagentasProp;
        private SerializedProperty stackTypeProp, timingProp, isDebuffProp, ticksDownProp, maxStacksProp;
        private SerializedProperty passiveModifiersProp, triggersProp;
        private SerializedProperty preventsActionProp, clearTimingProp;
        private SerializedProperty defensePersistsProp, bypassesShieldsProp, bypassesDEFProp;

        private bool foldPresets = true;
        private bool foldPassive = true;
        private bool foldTriggers = true;
        private bool foldFlags = false;

        private void OnEnable()
        {
            conditionIDProp       = serializedObject.FindProperty("conditionID");
            displayNameProp       = serializedObject.FindProperty("displayName");
            descriptionProp       = serializedObject.FindProperty("description");
            iconProp              = serializedObject.FindProperty("icon");
            tintColorProp         = serializedObject.FindProperty("tintColor");
            iconGrayscaleProp     = serializedObject.FindProperty("iconGrayscale");
            iconBwRedsProp        = serializedObject.FindProperty("iconBwReds");
            iconBwYellowsProp     = serializedObject.FindProperty("iconBwYellows");
            iconBwGreensProp      = serializedObject.FindProperty("iconBwGreens");
            iconBwCyansProp       = serializedObject.FindProperty("iconBwCyans");
            iconBwBluesProp       = serializedObject.FindProperty("iconBwBlues");
            iconBwMagentasProp    = serializedObject.FindProperty("iconBwMagentas");
            stackTypeProp         = serializedObject.FindProperty("stackType");
            timingProp            = serializedObject.FindProperty("timing");
            isDebuffProp          = serializedObject.FindProperty("isDebuff");
            ticksDownProp         = serializedObject.FindProperty("ticksDown");
            maxStacksProp         = serializedObject.FindProperty("maxStacks");
            passiveModifiersProp  = serializedObject.FindProperty("passiveModifiers");
            triggersProp          = serializedObject.FindProperty("triggers");
            preventsActionProp    = serializedObject.FindProperty("preventsAction");
            clearTimingProp       = serializedObject.FindProperty("clearTiming");
            defensePersistsProp   = serializedObject.FindProperty("defensePersists");
            bypassesShieldsProp   = serializedObject.FindProperty("bypassesShields");
            bypassesDEFProp       = serializedObject.FindProperty("bypassesDEF");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();
            DrawIdentity();
            DrawStacking();
            DrawPresets();
            DrawPassiveModifiers();
            DrawTriggers();
            DrawFlags();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Top summary row: polarity + stacking + clear timing ──────────────
        private void DrawTopSummary()
        {
            var cond = (ConditionData)target;

            EditorGUILayout.Space(4);
            EditorStyleKit.BeginBadgeRow();

            EditorStyleKit.DrawBadge(cond.isDebuff ? "DEBUFF" : "BUFF",
                cond.isDebuff ? EditorStyleKit.Debuff : EditorStyleKit.Buff, minWidth: 70);

            EditorStyleKit.DrawBadge(cond.stackType.ToString(),
                EditorStyleKit.StackTypeColor(cond.stackType));

            EditorStyleKit.DrawBadge(PrettyClearTiming(cond.clearTiming),
                EditorStyleKit.ClearTimingColor(cond.clearTiming));

            if (cond.bypassesShields)
                EditorStyleKit.DrawBadge("bypass Shields", new Color(0.70f, 0.45f, 0.50f));
            if (cond.bypassesDEF)
                EditorStyleKit.DrawBadge("bypass DEF", new Color(0.70f, 0.50f, 0.40f));
            if (cond.preventsAction)
                EditorStyleKit.DrawBadge("PREVENTS ACTION", new Color(0.82f, 0.32f, 0.30f));

            EditorStyleKit.EndBadgeRow();
        }

        private static string PrettyClearTiming(ClearTiming t) => t switch
        {
            ClearTiming.Never          => "persists",
            ClearTiming.OwnerTurnStart => "clear @ owner turn",
            ClearTiming.RoundStart     => "clear @ round start",
            ClearTiming.RoundEnd       => "clear @ round end",
            _ => t.ToString(),
        };

        // ═══ Identity ═══════════════════════════════════════════════════════════
        private void DrawIdentity()
        {
            var cond = (ConditionData)target;
            EditorStyleKit.DrawColoredSectionHeader("Identity",
                cond.isDebuff ? EditorStyleKit.Debuff : EditorStyleKit.Buff);
            // Condition ID picker + "+ New..." shortcut for adding an entirely
            // new enum value to CombatEnums.cs without leaving the inspector.
            EditorGUILayout.BeginHorizontal();
            EditorStyleKit.DrawSortedEnumPopup<ConditionID>(conditionIDProp, "Condition ID");
            if (GUILayout.Button(
                new GUIContent("+ New...", "Add a new value to the ConditionID enum."),
                GUILayout.Width(78)))
            {
                NewConditionIDWindow.Open();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(displayNameProp);
            EditorGUILayout.PropertyField(descriptionProp);
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(tintColorProp);

            DrawIconGrayscalePreview();
        }

        /// <summary>
        /// Mirrors SkillDataEditor's banner preview: shows the condition icon
        /// cropped to its natural square, runs it through the BlackAndWhite
        /// material when iconGrayscale is on, and exposes the six per-hue
        /// weights as sliders underneath.
        /// </summary>
        private void DrawIconGrayscalePreview()
        {
            var sprite = iconProp != null ? iconProp.objectReferenceValue as Sprite : null;
            if (sprite == null || sprite.texture == null) return;

            EditorGUILayout.Space(2);

            // Square preview sized to match a HUD icon (~64 px on screen).
            const float previewSide = 64f;
            var rect = GUILayoutUtility.GetRect(previewSide, previewSide,
                GUILayout.Width(previewSide), GUILayout.Height(previewSide));

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
            var border = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), border);

            // Sprite-rect UV in texture space (handles atlased sprites).
            var tex = sprite.texture;
            var sr = sprite.rect;
            var texCoords = new Rect(sr.x / tex.width, sr.y / tex.height,
                                     sr.width / tex.width, sr.height / tex.height);

            bool wantBW = iconGrayscaleProp != null && iconGrayscaleProp.boolValue;
            var bwMat = wantBW ? GetBlackAndWhiteIconMaterial() : null;

            if (wantBW && bwMat != null)
            {
                bwMat.SetFloat(PropWR, iconBwRedsProp    != null ? iconBwRedsProp.floatValue    : 0.40f);
                bwMat.SetFloat(PropWY, iconBwYellowsProp != null ? iconBwYellowsProp.floatValue : 0.60f);
                bwMat.SetFloat(PropWG, iconBwGreensProp  != null ? iconBwGreensProp.floatValue  : 0.40f);
                bwMat.SetFloat(PropWC, iconBwCyansProp   != null ? iconBwCyansProp.floatValue   : 0.60f);
                bwMat.SetFloat(PropWB, iconBwBluesProp   != null ? iconBwBluesProp.floatValue   : 0.20f);
                bwMat.SetFloat(PropWM, iconBwMagentasProp!= null ? iconBwMagentasProp.floatValue: 0.80f);
            }

            if (wantBW && bwMat != null && Event.current.type == EventType.Repaint)
            {
                Graphics.DrawTexture(rect, tex, texCoords,
                    0, 0, 0, 0, Color.white, bwMat);
            }
            else
            {
                GUI.DrawTextureWithTexCoords(rect, tex, texCoords, alphaBlend: true);
            }

            // Toggle + sliders underneath.
            if (iconGrayscaleProp != null)
            {
                EditorGUILayout.PropertyField(iconGrayscaleProp,
                    new GUIContent("Black & White",
                        "Render the icon in B&W via DarkSpire/UI/BlackAndWhite. " +
                        "Useful for placeholder art that should match the mono HUD."));
            }

            if (wantBW)
            {
                EditorGUI.indentLevel++;
                if (iconBwRedsProp != null)     EditorGUILayout.Slider(iconBwRedsProp,     0f, 3f, new GUIContent("Reds"));
                if (iconBwYellowsProp != null)  EditorGUILayout.Slider(iconBwYellowsProp,  0f, 3f, new GUIContent("Yellows"));
                if (iconBwGreensProp != null)   EditorGUILayout.Slider(iconBwGreensProp,   0f, 3f, new GUIContent("Greens"));
                if (iconBwCyansProp != null)    EditorGUILayout.Slider(iconBwCyansProp,    0f, 3f, new GUIContent("Cyans"));
                if (iconBwBluesProp != null)    EditorGUILayout.Slider(iconBwBluesProp,    0f, 3f, new GUIContent("Blues"));
                if (iconBwMagentasProp != null) EditorGUILayout.Slider(iconBwMagentasProp, 0f, 3f, new GUIContent("Magentas"));
                EditorGUI.indentLevel--;
            }
        }

        // Shader property IDs for the B&W material — same as SkillDataEditor uses.
        private static readonly int PropWR = Shader.PropertyToID("_WR");
        private static readonly int PropWY = Shader.PropertyToID("_WY");
        private static readonly int PropWG = Shader.PropertyToID("_WG");
        private static readonly int PropWC = Shader.PropertyToID("_WC");
        private static readonly int PropWB = Shader.PropertyToID("_WB");
        private static readonly int PropWM = Shader.PropertyToID("_WM");

        /// <summary>
        /// Lazy-cached B&W preview material. Shared across ConditionData
        /// inspectors — the per-hue weights are pushed in every repaint so a
        /// single hidden material serves every open inspector without issues.
        /// </summary>
        private static Material _bwPreviewMaterial;
        private static Material GetBlackAndWhiteIconMaterial()
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

        // ═══ Stacking ═══════════════════════════════════════════════════════════
        private void DrawStacking()
        {
            var cond = (ConditionData)target;
            EditorStyleKit.DrawColoredSectionHeader("Stacking & Timing",
                EditorStyleKit.StackTypeColor(cond.stackType));
            EditorGUILayout.PropertyField(isDebuffProp);
            EditorGUILayout.PropertyField(stackTypeProp);
            var stackType = (ConditionStackType)stackTypeProp.enumValueIndex;
            if (stackType == ConditionStackType.Counter)
            {
                EditorGUILayout.PropertyField(maxStacksProp,
                    new GUIContent("Max Stacks", "0 = unlimited"));
                EditorGUILayout.PropertyField(ticksDownProp,
                    new GUIContent("Ticks Down", "Legacy flag — stack auto-decrement used by old tick system."));
            }
            EditorGUILayout.PropertyField(timingProp,
                new GUIContent("Timing (legacy)", "Deprecated — prefer triggers[]. Only read by legacy fallbacks."));
        }

        // ═══ Presets ════════════════════════════════════════════════════════════
        private void DrawPresets()
        {
            EditorGUILayout.Space(4);
            foldPresets = EditorGUILayout.Foldout(foldPresets, "Presets (click to seed triggers)", true, EditorStyles.foldoutHeader);
            if (!foldPresets) return;

            EditorGUILayout.HelpBox(
                "Presets append to the existing triggers. Clear triggers first if you " +
                "want a fresh template. Passive-modifier presets append to passiveModifiers similarly.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Triggered patterns", EditorStyles.boldLabel);
                if (GUILayout.Button("Poison-style DoT (TurnStart → 1 dmg/stack, decrement)")) Preset.AddDoT(triggersProp);
                if (GUILayout.Button("Regen-style HoT (Cleanup → 1 heal/stack, decrement)")) Preset.AddRegen(triggersProp);
                if (GUILayout.Button("Plating-style fade (TurnStart → grant Guard/stack, decrement)")) Preset.AddPlating(triggersProp);
                if (GUILayout.Button("Shields-style absorb (TakeDamagePre → 1 absorb/stack)")) Preset.AddShields(triggersProp, clearTimingProp);
                if (GUILayout.Button("Artifact-style negate (OnDebuffApplied → Negate, consume 1)")) Preset.AddArtifact(triggersProp);
                if (GUILayout.Button("Dodge-style chance (TakeDamagePre → 15%/stack negate, consume)")) Preset.AddDodge(triggersProp);
                if (GUILayout.Button("Thorns-style retaliate (TakeDamagePre → damage source N/stack)")) Preset.AddThorns(triggersProp);
                if (GUILayout.Button("Doom-style execute (TurnEnd → if stacks ≥ HP, kill)")) Preset.AddDoom(triggersProp);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Passive-modifier patterns", EditorStyles.boldLabel);
                if (GUILayout.Button("Strength-style POW buff (+1 per stack)")) Preset.AddPassive(passiveModifiersProp, StatKind.POW, 1f);
                if (GUILayout.Button("Weak-style POW debuff (-2 per stack)")) Preset.AddPassive(passiveModifiersProp, StatKind.POW, -2f);
                if (GUILayout.Button("Guarding-style DEF buff (+4, clears at owner's turn)")) { Preset.AddPassive(passiveModifiersProp, StatKind.DEF, 4f); clearTimingProp.enumValueIndex = (int)ClearTiming.OwnerTurnStart; }
                if (GUILayout.Button("Vulnerable-style damage amp (+50% taken)")) Preset.AddDamageAmp(triggersProp, 0.5f);
            }
        }

        // ═══ Passive modifiers ══════════════════════════════════════════════════
        private void DrawPassiveModifiers()
        {
            EditorGUILayout.Space(4);
            foldPassive = EditorGUILayout.Foldout(foldPassive,
                $"Passive Modifiers ({passiveModifiersProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (!foldPassive) return;

            EditorGUILayout.HelpBox(
                "Static stat bonuses while the condition is active. amountPerStack × currentStacks " +
                "gets summed by Unit.Effective* properties each read.",
                MessageType.None);

            for (int i = 0; i < passiveModifiersProp.arraySize; i++)
                DrawPassiveModifier(i);

            if (GUILayout.Button("＋ Add Passive Modifier"))
            {
                passiveModifiersProp.InsertArrayElementAtIndex(passiveModifiersProp.arraySize);
            }
        }

        private void DrawPassiveModifier(int idx)
        {
            var el = passiveModifiersProp.GetArrayElementAtIndex(idx);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var statProp = el.FindPropertyRelative("stat");
                var amtProp  = el.FindPropertyRelative("amountPerStack");
                EditorGUILayout.PropertyField(statProp, GUIContent.none, GUILayout.Width(160));
                EditorGUILayout.PropertyField(amtProp, new GUIContent("× stack"), GUILayout.MinWidth(120));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    passiveModifiersProp.DeleteArrayElementAtIndex(idx);
            }
        }

        // ═══ Triggers ═══════════════════════════════════════════════════════════
        private void DrawTriggers()
        {
            EditorGUILayout.Space(4);
            foldTriggers = EditorGUILayout.Foldout(foldTriggers,
                $"Triggers ({triggersProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (!foldTriggers) return;

            for (int i = 0; i < triggersProp.arraySize; i++)
                DrawTrigger(i);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Trigger", GUILayout.Height(22)))
                triggersProp.InsertArrayElementAtIndex(triggersProp.arraySize);
            if (triggersProp.arraySize > 0 && GUILayout.Button("Clear All", GUILayout.Height(22), GUILayout.Width(90)))
                triggersProp.ClearArray();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTrigger(int idx)
        {
            var trig = triggersProp.GetArrayElementAtIndex(idx);
            var whenProp = trig.FindPropertyRelative("when");
            var conditionalsProp = trig.FindPropertyRelative("onlyIf");
            var actionsProp = trig.FindPropertyRelative("actions");
            var stackOpProp = trig.FindPropertyRelative("afterFiring");
            var stackOpAmtProp = trig.FindPropertyRelative("stackOpAmount");

            var eventKind = (TriggerEvent)whenProp.enumValueIndex;
            var eventColor = EditorStyleKit.TriggerEventColor(eventKind);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, eventColor, 0.25f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                // Header row: index + event badge + when dropdown + controls
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Trigger #{idx + 1}", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorStyleKit.DrawBadge(eventKind.ToString(), eventColor);
                GUILayout.Space(4);
                EditorGUILayout.PropertyField(whenProp, GUIContent.none);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▲", GUILayout.Width(24)) && idx > 0)
                { triggersProp.MoveArrayElement(idx, idx - 1); return; }
                if (GUILayout.Button("▼", GUILayout.Width(24)) && idx < triggersProp.arraySize - 1)
                { triggersProp.MoveArrayElement(idx, idx + 1); return; }
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                { triggersProp.DeleteArrayElementAtIndex(idx); return; }
                EditorGUILayout.EndHorizontal();

                // ── Conditionals sub-list ──
                EditorGUILayout.LabelField("IF (filters, AND)", EditorStyles.miniBoldLabel);
                for (int c = 0; c < conditionalsProp.arraySize; c++)
                    DrawConditional(conditionalsProp, c);
                if (GUILayout.Button("＋ Add Filter", EditorStyles.miniButton))
                    conditionalsProp.InsertArrayElementAtIndex(conditionalsProp.arraySize);

                // ── Actions sub-list ──
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("DO (actions, in order)", EditorStyles.miniBoldLabel);
                for (int a = 0; a < actionsProp.arraySize; a++)
                    DrawAction(actionsProp, a);
                if (GUILayout.Button("＋ Add Action", EditorStyles.miniButton))
                    actionsProp.InsertArrayElementAtIndex(actionsProp.arraySize);

                // ── Stack op ──
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("THEN (stack change)", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(stackOpProp, GUIContent.none, GUILayout.MinWidth(160));
                var op = (StackOp)stackOpProp.enumValueIndex;
                if (op == StackOp.DecrementByN || op == StackOp.ConsumeN || op == StackOp.ConsumeIfActionLanded)
                    EditorGUILayout.PropertyField(stackOpAmtProp, new GUIContent("n"), GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawConditional(SerializedProperty list, int idx)
        {
            var el = list.GetArrayElementAtIndex(idx);
            var kindProp = el.FindPropertyRelative("kind");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(kindProp, GUIContent.none, GUILayout.Width(200));
                var kind = (ConditionalKind)kindProp.enumValueIndex;
                switch (kind)
                {
                    case ConditionalKind.StacksAtLeast:
                    case ConditionalKind.StacksAtMost:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("intParam"), GUIContent.none);
                        break;
                    case ConditionalKind.TargetHPBelowPercent:
                    case ConditionalKind.TargetHPAbovePercent:
                    case ConditionalKind.SourceHPBelowPercent:
                        EditorGUILayout.Slider(el.FindPropertyRelative("floatParam"), 0f, 1f, GUIContent.none);
                        break;
                    case ConditionalKind.RollSucceeds:
                    case ConditionalKind.RollSucceedsPerStack:
                        EditorGUILayout.Slider(el.FindPropertyRelative("floatParam"), 0f, 1f, GUIContent.none);
                        break;
                    case ConditionalKind.IncomingConditionIs:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("conditionParam"), GUIContent.none);
                        break;
                    case ConditionalKind.SourceSkillHasTag:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("tagFilter"), GUIContent.none);
                        break;
                }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    list.DeleteArrayElementAtIndex(idx);
            }
        }

        private void DrawAction(SerializedProperty list, int idx)
        {
            var el = list.GetArrayElementAtIndex(idx);
            var kindProp = el.FindPropertyRelative("kind");
            var targetProp = el.FindPropertyRelative("target");

            var actionKind = (TriggerActionKind)kindProp.enumValueIndex;
            var actColor = EditorStyleKit.TriggerActionColor(actionKind);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, actColor, 0.25f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                EditorGUILayout.BeginHorizontal();
                EditorStyleKit.DrawBadge(PrettyAction(actionKind), actColor);
                GUILayout.Space(4);
                EditorGUILayout.PropertyField(kindProp, GUIContent.none, GUILayout.Width(220));
                EditorGUILayout.PropertyField(targetProp, GUIContent.none, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                { list.DeleteArrayElementAtIndex(idx); return; }
                EditorGUILayout.EndHorizontal();

                var kind = (TriggerActionKind)kindProp.enumValueIndex;
                switch (kind)
                {
                    case TriggerActionKind.DealDamage:
                    case TriggerActionKind.HealTarget:
                    case TriggerActionKind.DamageSource:
                    case TriggerActionKind.AbsorbDamage:
                    case TriggerActionKind.ModifyIncomingDamageFlat:
                    case TriggerActionKind.ModifyOutgoingDamageFlat:
                    case TriggerActionKind.ModifyAttackRollBy:
                    case TriggerActionKind.GrantGuard:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("amount"));
                        break;
                    case TriggerActionKind.DealDamagePerStack:
                    case TriggerActionKind.HealTargetPerStack:
                    case TriggerActionKind.DamageSourcePerStack:
                    case TriggerActionKind.AbsorbDamagePerStack:
                    case TriggerActionKind.ModifyOutgoingDamagePerStack:
                    case TriggerActionKind.ModifyAttackRollByPerStack:
                    case TriggerActionKind.GrantGuardPerStack:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("amountPerStack"));
                        break;
                    case TriggerActionKind.ModifyIncomingDamagePercent:
                    case TriggerActionKind.ModifyOutgoingDamagePercent:
                        EditorGUILayout.Slider(el.FindPropertyRelative("percentValue"), -1f, 2f,
                            new GUIContent("±% (0.5 = +50%, -0.25 = −25%)"));
                        break;
                    case TriggerActionKind.ApplyCondition:
                    case TriggerActionKind.ApplyConditionPerStack:
                    case TriggerActionKind.RemoveCondition:
                        EditorStyleKit.DrawSortedEnumPopup<ConditionID>(
                            el.FindPropertyRelative("conditionID"), "Condition");
                        if (kind != TriggerActionKind.RemoveCondition)
                            EditorGUILayout.PropertyField(el.FindPropertyRelative("conditionStacks"),
                                new GUIContent("Stacks"));
                        break;
                    case TriggerActionKind.ModifyStat:
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("stat"));
                        EditorGUILayout.PropertyField(el.FindPropertyRelative("amount"));
                        break;
                    case TriggerActionKind.NegateIncomingEffect:
                    case TriggerActionKind.KillTarget:
                        // no extra fields
                        break;
                }
            }
        }

        // ═══ Structural Flags ═══════════════════════════════════════════════════
        private void DrawFlags()
        {
            EditorGUILayout.Space(4);
            foldFlags = EditorGUILayout.Foldout(foldFlags, "Structural Flags", true, EditorStyles.foldoutHeader);
            if (!foldFlags) return;

            EditorGUILayout.PropertyField(preventsActionProp,
                new GUIContent("Prevents Action (Stunned)"));
            EditorGUILayout.PropertyField(clearTimingProp,
                new GUIContent("Clear Timing",
                    "When this condition auto-clears:\n" +
                    "• Never — stays until explicitly removed\n" +
                    "• OwnerTurnStart — at this unit's next turn (Guarding)\n" +
                    "• RoundStart — at start of next round (Shields — protects\n" +
                    "  the whole round regardless of who got it mid-round)\n" +
                    "• RoundEnd — at end of current round"));
            EditorGUILayout.PropertyField(defensePersistsProp,
                new GUIContent("Defense Persists (Barricade)"));
            EditorGUILayout.PropertyField(bypassesShieldsProp,
                new GUIContent("Bypasses Shields (DoTs to HP directly)"));
            EditorGUILayout.PropertyField(bypassesDEFProp,
                new GUIContent("Bypasses DEF"));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static void Section(string text) => EditorStyleKit.DrawSectionHeader(text);

        /// <summary>Short, readable label for an action kind in its badge.</summary>
        private static string PrettyAction(TriggerActionKind k) => k switch
        {
            TriggerActionKind.DealDamage                    => "DMG",
            TriggerActionKind.DealDamagePerStack             => "DMG/stk",
            TriggerActionKind.DamageSource                   => "RETAL",
            TriggerActionKind.DamageSourcePerStack           => "RETAL/stk",
            TriggerActionKind.HealTarget                     => "HEAL",
            TriggerActionKind.HealTargetPerStack             => "HEAL/stk",
            TriggerActionKind.AbsorbDamage                   => "ABSORB",
            TriggerActionKind.AbsorbDamagePerStack           => "ABSORB/stk",
            TriggerActionKind.NegateIncomingEffect           => "NEGATE",
            TriggerActionKind.GrantGuard                     => "GUARD",
            TriggerActionKind.GrantGuardPerStack             => "GUARD/stk",
            TriggerActionKind.ModifyIncomingDamageFlat       => "IN ±",
            TriggerActionKind.ModifyIncomingDamagePercent    => "IN %",
            TriggerActionKind.ModifyOutgoingDamageFlat       => "OUT ±",
            TriggerActionKind.ModifyOutgoingDamagePerStack   => "OUT/stk",
            TriggerActionKind.ModifyOutgoingDamagePercent    => "OUT %",
            TriggerActionKind.ModifyAttackRollBy             => "ROLL ±",
            TriggerActionKind.ModifyAttackRollByPerStack     => "ROLL/stk",
            TriggerActionKind.ModifyStat                     => "STAT",
            TriggerActionKind.ApplyCondition                 => "APPLY",
            TriggerActionKind.ApplyConditionPerStack         => "APPLY/stk",
            TriggerActionKind.RemoveCondition                => "REMOVE",
            TriggerActionKind.KillTarget                     => "KILL",
            _                                                => k.ToString(),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Presets — seed common trigger patterns so designers don't build from scratch
    // ═══════════════════════════════════════════════════════════════════════════
    internal static class Preset
    {
        private static SerializedProperty AddTrigger(SerializedProperty triggersProp,
            TriggerEvent when, StackOp op, int opAmount = 1)
        {
            int i = triggersProp.arraySize;
            triggersProp.InsertArrayElementAtIndex(i);
            var t = triggersProp.GetArrayElementAtIndex(i);
            t.FindPropertyRelative("when").enumValueIndex = (int)when;
            t.FindPropertyRelative("afterFiring").enumValueIndex = (int)op;
            t.FindPropertyRelative("stackOpAmount").intValue = opAmount;
            t.FindPropertyRelative("onlyIf").ClearArray();
            t.FindPropertyRelative("actions").ClearArray();
            return t;
        }

        private static SerializedProperty AddAction(SerializedProperty trigger,
            TriggerActionKind kind, ActionTarget target = ActionTarget.Self)
        {
            var actions = trigger.FindPropertyRelative("actions");
            int i = actions.arraySize;
            actions.InsertArrayElementAtIndex(i);
            var a = actions.GetArrayElementAtIndex(i);
            a.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            a.FindPropertyRelative("target").enumValueIndex = (int)target;
            return a;
        }

        private static SerializedProperty AddConditional(SerializedProperty trigger,
            ConditionalKind kind)
        {
            var conds = trigger.FindPropertyRelative("onlyIf");
            int i = conds.arraySize;
            conds.InsertArrayElementAtIndex(i);
            var c = conds.GetArrayElementAtIndex(i);
            c.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            return c;
        }

        public static void AddDoT(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTurnStart, StackOp.DecrementByOne);
            var a = AddAction(t, TriggerActionKind.DealDamagePerStack);
            a.FindPropertyRelative("amountPerStack").intValue = 1;
        }

        public static void AddRegen(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnCleanup, StackOp.DecrementByOne);
            var a = AddAction(t, TriggerActionKind.HealTargetPerStack);
            a.FindPropertyRelative("amountPerStack").intValue = 1;
        }

        public static void AddPlating(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTurnStart, StackOp.DecrementByOne);
            var a = AddAction(t, TriggerActionKind.GrantGuardPerStack);
            a.FindPropertyRelative("amountPerStack").intValue = 1;
        }

        public static void AddShields(SerializedProperty triggersProp, SerializedProperty clearTimingProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTakeDamagePre, StackOp.NoChange);
            var a = AddAction(t, TriggerActionKind.AbsorbDamagePerStack);
            a.FindPropertyRelative("amountPerStack").intValue = 1;
            // RoundStart clear means shields persist through ALL turns in the round
            // (ally can Shield you mid-round, you still benefit on your turn + enemy phase).
            clearTimingProp.enumValueIndex = (int)ClearTiming.RoundStart;
        }

        public static void AddArtifact(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnDebuffApplied, StackOp.ConsumeN, 1);
            AddConditional(t, ConditionalKind.IncomingIsDebuff);
            AddAction(t, TriggerActionKind.NegateIncomingEffect);
        }

        public static void AddDodge(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTakeDamagePre, StackOp.ConsumeIfActionLanded, 1);
            var c = AddConditional(t, ConditionalKind.RollSucceedsPerStack);
            c.FindPropertyRelative("floatParam").floatValue = 0.15f;
            AddAction(t, TriggerActionKind.NegateIncomingEffect);
        }

        public static void AddThorns(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTakeDamagePre, StackOp.NoChange);
            var a = AddAction(t, TriggerActionKind.DamageSourcePerStack, ActionTarget.Source);
            a.FindPropertyRelative("amountPerStack").intValue = 1;
        }

        public static void AddDoom(SerializedProperty triggersProp)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTurnEnd, StackOp.NoChange);
            var c = AddConditional(t, ConditionalKind.StacksAtLeast);
            c.FindPropertyRelative("intParam").intValue = 1; // designer bumps to HP threshold in practice
            AddAction(t, TriggerActionKind.KillTarget);
        }

        public static void AddDamageAmp(SerializedProperty triggersProp, float percent)
        {
            var t = AddTrigger(triggersProp, TriggerEvent.OnTakeDamagePre, StackOp.NoChange);
            var a = AddAction(t, TriggerActionKind.ModifyIncomingDamagePercent);
            a.FindPropertyRelative("percentValue").floatValue = percent;
        }

        public static void AddPassive(SerializedProperty passiveProp, StatKind stat, float amountPerStack)
        {
            int i = passiveProp.arraySize;
            passiveProp.InsertArrayElementAtIndex(i);
            var el = passiveProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("stat").enumValueIndex = (int)stat;
            el.FindPropertyRelative("amountPerStack").floatValue = amountPerStack;
        }
    }
}
