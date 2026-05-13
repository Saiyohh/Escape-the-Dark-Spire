// EffectsListDrawer.cs
// -----------------------------------------------------------------------------
// Shared inspector drawer for SkillEffectData[] arrays. Used by both
// SkillDataEditor (player skills) and EnemyDataEditor (enemy intents) so the
// effect-authoring UX is identical: per-type field reveal, gates, loops,
// derived stacks, save DCs, condition-library validation, all of it.
//
// The caller draws its own section header / box and then calls Draw(...)
// passing the SerializedProperty for the effects[] array. The optional
// `pickCountProp` powers the per-effect "Pick #" UI for skills with
// targetPickCount > 1; pass null for enemies and the field stays hidden.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class EffectsListDrawer
    {
        /// <summary>
        /// Draw the whole effects array — every effect, plus the
        /// "+ Add Effect" / "Clear All" controls underneath.
        /// </summary>
        public static void Draw(SerializedProperty effectsProp,
                                SerializedProperty pickCountProp = null)
        {
            if (effectsProp == null) return;

            for (int i = 0; i < effectsProp.arraySize; i++)
                DrawEffect(effectsProp, effectsProp.GetArrayElementAtIndex(i), i, pickCountProp);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Effect", GUILayout.Height(22)))
                effectsProp.InsertArrayElementAtIndex(effectsProp.arraySize);
            if (effectsProp.arraySize > 0
                && GUILayout.Button("Clear All", GUILayout.Height(22), GUILayout.Width(80)))
                effectsProp.ClearArray();
            EditorGUILayout.EndHorizontal();
        }

        // ─── Per-effect rendering ────────────────────────────────────────────

        private static void DrawEffect(SerializedProperty effectsProp,
                                       SerializedProperty effectProp, int index,
                                       SerializedProperty pickCountProp)
        {
            var effectTypeProp = effectProp.FindPropertyRelative("effectType");
            var type = (SkillEffectType)effectTypeProp.enumValueIndex;
            var category = SkillEffectData.CategoryOf(type);
            var catColor = EditorStyleKit.EffectCategoryColor(category);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, catColor, 0.35f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                const float RowHeight = 18f;
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

                var idxStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = RowHeight,
                };
                GUILayout.Label($"#{index + 1}", idxStyle, GUILayout.Width(28), GUILayout.Height(RowHeight));

                EditorStyleKit.DrawBadge(category.ToString(), catColor);
                GUILayout.Space(4);

                var typeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 11,
                    fixedHeight = RowHeight,
                };
                GUILayout.Label(PrettyType(type), typeStyle, GUILayout.Height(RowHeight));

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index > 0)
                { effectsProp.MoveArrayElement(index, index - 1); EditorGUILayout.EndHorizontal(); return; }
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index < effectsProp.arraySize - 1)
                { effectsProp.MoveArrayElement(index, index + 1); EditorGUILayout.EndHorizontal(); return; }
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(RowHeight)))
                { effectsProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); return; }
                EditorGUILayout.EndHorizontal();

                // Type + gate + gate-target + target + pick-index + loop link (always shown).
                DrawFilteredTypeField(effectTypeProp);
                DrawGateField(effectProp, index);
                DrawGateTargetModeField(effectProp);
                EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("targetMode"));
                DrawPickIndexField(effectProp, pickCountProp);
                DrawLoopLinkField(effectProp, effectsProp, index);

                // Per-type field reveal — only show fields the effect actually reads.
                switch (type)
                {
                    case SkillEffectType.Attack:
                        PropField(effectProp, "magnitude", "Damage");
                        PropField(effectProp, "damageStat", "Stat Bonus");
                        PropField(effectProp, "hitCount", "Hits");
                        break;

                    case SkillEffectType.Apply:
                        PropField(effectProp, "applyKind", "Apply");
                        var applyKind = (ApplyKind)effectProp.FindPropertyRelative("applyKind").enumValueIndex;
                        if (applyKind == ApplyKind.Damage)
                        {
                            PropField(effectProp, "magnitude", "Damage");
                            PropField(effectProp, "damageStat", "Stat Bonus");
                        }
                        else
                        {
                            DrawConditionIDField(effectProp);
                            DrawStackCountInline(effectProp);
                        }
                        break;

                    case SkillEffectType.Afflict:
                        PropField(effectProp, "saveDC", "Save DC (0 = 10 + caster WIL)");
                        DrawConditionIDField(effectProp);
                        DrawStackCountInline(effectProp);
                        break;

                    case SkillEffectType.DirectDamage:
                        EditorGUILayout.HelpBox(
                            "Legacy: Direct Damage. Prefer Apply → Damage.",
                            MessageType.Info);
                        PropField(effectProp, "magnitude", "Damage");
                        PropField(effectProp, "damageStat", "Stat Bonus");
                        break;

                    case SkillEffectType.ApplyCondition:
                        EditorGUILayout.HelpBox(
                            "Legacy: Apply Condition. Prefer Apply → Condition (no WIL save) or Afflict (WIL save).",
                            MessageType.Info);
                        DrawConditionIDField(effectProp);
                        DrawStackCountInline(effectProp);
                        break;

                    case SkillEffectType.Heal:
                    case SkillEffectType.RestoreSP:
                    case SkillEffectType.LoseHP:
                        PropField(effectProp, "magnitude");
                        break;

                    case SkillEffectType.RemoveCondition:
                        DrawConditionIDField(effectProp);
                        break;

                    case SkillEffectType.Move:
                        PropField(effectProp, "movementKind");
                        var moveKind = (MovementKind)effectProp.FindPropertyRelative("movementKind").enumValueIndex;
                        if (moveKind != MovementKind.Shuffle)
                            PropField(effectProp, "movementMagnitude");
                        bool moveCanSave = moveKind == MovementKind.Pull
                                        || moveKind == MovementKind.Knockback
                                        || moveKind == MovementKind.Shuffle;
                        if (moveCanSave) PropField(effectProp, "saveDC");
                        break;

                    case SkillEffectType.Haste:
                    case SkillEffectType.Renew:
                        PropField(effectProp, "extraActionKind");
                        break;

                    case SkillEffectType.SealSkill:
                        PropField(effectProp, "sealTarget");
                        PropField(effectProp, "sealDuration");
                        break;

                    case SkillEffectType.GenerateItem:
                        PropField(effectProp, "pouchItemType");
                        PropField(effectProp, "itemQuantity");
                        var itemType = (PouchItemType)effectProp.FindPropertyRelative("pouchItemType").enumValueIndex;
                        if (itemType == PouchItemType.Generic) PropField(effectProp, "itemSO");
                        PlaceholderWarning();
                        break;

                    case SkillEffectType.ChannelOrb:
                        PropField(effectProp, "orbType");
                        PropField(effectProp, "orbCount");
                        PropField(effectProp, "orbSource");
                        break;

                    case SkillEffectType.EvokeOrb:
                        PropField(effectProp, "evokeKind");
                        PropField(effectProp, "evokeCount");
                        break;

                    case SkillEffectType.SummonCompanion:
                        PropField(effectProp, "companionTarget");
                        PropField(effectProp, "magnitude", "HP Amount");
                        PlaceholderWarning();
                        break;

                    case SkillEffectType.BindCompanion:
                        PropField(effectProp, "bindDuration");
                        PlaceholderWarning();
                        break;

                    case SkillEffectType.GainStars:
                        PropField(effectProp, "resourceAmount");
                        break;

                    case SkillEffectType.Forge:
                        PropField(effectProp, "resourceAmount");
                        PlaceholderWarning();
                        break;

                    case SkillEffectType.Retaliate:
                        PropField(effectProp, "retaliateDamage");
                        PropField(effectProp, "triggerDuration");
                        PlaceholderWarning();
                        break;

                    case SkillEffectType.OnAllyAttackRider:
                        PropField(effectProp, "magnitude", "Guard per Trigger");
                        PropField(effectProp, "triggerDuration");
                        PlaceholderWarning();
                        break;
                }
            }
            EditorGUILayout.Space(2);
        }

        // ─── Sub-field helpers ───────────────────────────────────────────────

        public static string PrettyType(SkillEffectType t) => t switch
        {
            SkillEffectType.Attack         => "Attack",
            SkillEffectType.Apply          => "Apply",
            SkillEffectType.Afflict        => "Afflict",
            SkillEffectType.DirectDamage   => "Direct Damage (legacy)",
            SkillEffectType.ApplyCondition => "Apply Condition (legacy)",
            _ => t.ToString(),
        };

        /// <summary>
        /// Effect-type dropdown that hides legacy values from the pick list
        /// (unless already set) and sorts everything alphabetically.
        /// </summary>
        public static void DrawFilteredTypeField(SerializedProperty effectTypeProp)
        {
            var current = (SkillEffectType)effectTypeProp.enumValueIndex;
            var all = (SkillEffectType[])System.Enum.GetValues(typeof(SkillEffectType));

            var visible = new System.Collections.Generic.List<SkillEffectType>(all.Length);
            foreach (var t in all)
            {
                bool isLegacy = t == SkillEffectType.DirectDamage
                             || t == SkillEffectType.ApplyCondition;
                if (isLegacy && current != t) continue;
                visible.Add(t);
            }

            visible.Sort((a, b) => string.Compare(
                PrettyType(a), PrettyType(b), System.StringComparison.OrdinalIgnoreCase));

            var labels = new string[visible.Count];
            for (int i = 0; i < visible.Count; i++) labels[i] = PrettyType(visible[i]);

            int currentIndex = visible.IndexOf(current);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup("Type", currentIndex, labels);
            var chosen = visible[newIndex];
            if (chosen != current)
                effectTypeProp.enumValueIndex = (int)chosen;
        }

        /// <summary>
        /// Conditional gate dropdown. Disabled on effect #0 since there's no
        /// prior Attack/Afflict to gate on.
        /// </summary>
        public static void DrawGateField(SerializedProperty effectProp, int index)
        {
            var gateProp = effectProp.FindPropertyRelative("gate");
            if (gateProp == null) return;

            using (new EditorGUI.DisabledScope(index == 0))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(gateProp,
                    new GUIContent("When", "Gate this effect on the result of the most " +
                                           "recent Attack/Afflict against the same target."));
                EditorGUI.indentLevel--;
            }
            if (index == 0 && (ConditionalGate)gateProp.enumValueIndex != ConditionalGate.Always)
                gateProp.enumValueIndex = (int)ConditionalGate.Always;
        }

        public static void DrawGateTargetModeField(SerializedProperty effectProp)
        {
            var gateProp = effectProp.FindPropertyRelative("gate");
            var sameTargetProp = effectProp.FindPropertyRelative("sameTargetAsGate");
            if (gateProp == null || sameTargetProp == null) return;

            var gate = (ConditionalGate)gateProp.enumValueIndex;
            if (gate == ConditionalGate.Always) return;

            EditorGUI.indentLevel++;
            int currentIdx = sameTargetProp.boolValue ? 0 : 1;
            var options = new[]
            {
                new GUIContent("Same target as gate"),
                new GUIContent("Re-roll (use Target Mode)"),
            };
            int newIdx = EditorGUILayout.Popup(
                new GUIContent("Apply To",
                    "Same = the unit the gate was evaluated against. Re-roll = pick a new " +
                    "target via the effect's targetMode below."),
                currentIdx, options);
            if (newIdx != currentIdx)
                sameTargetProp.boolValue = (newIdx == 0);

            if (gate == ConditionalGate.OnKill && sameTargetProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "OnKill against the same target lands on a dead unit — most effects " +
                    "are no-ops there. Consider 'Re-roll' to pick a fresh target.",
                    MessageType.None);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Pick-index field (skills with targetPickCount > 1 only). Pass
        /// pickCountProp = null to hide the field entirely (enemy intents).
        /// </summary>
        public static void DrawPickIndexField(SerializedProperty effectProp,
                                              SerializedProperty pickCountProp)
        {
            var targetModeProp = effectProp.FindPropertyRelative("targetMode");
            var pickIdxProp = effectProp.FindPropertyRelative("targetPickIndex");
            if (targetModeProp == null || pickIdxProp == null) return;

            var mode = (TargetMode)targetModeProp.enumValueIndex;
            bool isPickable = mode == TargetMode.SingleEnemy || mode == TargetMode.SingleAlly;
            int pickCount = pickCountProp != null ? Mathf.Max(1, pickCountProp.intValue) : 1;

            if (!isPickable || pickCount <= 1)
            {
                if (pickIdxProp.intValue != 0) pickIdxProp.intValue = 0;
                return;
            }

            var labels = new string[pickCount];
            for (int i = 0; i < pickCount; i++) labels[i] = $"Pick #{i + 1}";

            int current = Mathf.Clamp(pickIdxProp.intValue, 0, pickCount - 1);
            EditorGUI.indentLevel++;
            int next = EditorGUILayout.Popup(
                new GUIContent("Pick #",
                    "Which of the skill's picks this effect applies to. Multiple " +
                    "effects can share the same pick (default 0)."),
                current, labels);
            if (next != current) pickIdxProp.intValue = next;
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Optional "do-again" link. Popup of prior effect indices plus a
        /// "None" option. Picking an index makes the resolver jump back after
        /// this effect runs (capped at SkillResolver.MaxLoopIterations).
        /// </summary>
        public static void DrawLoopLinkField(SerializedProperty effectProp,
                                             SerializedProperty effectsProp, int index)
        {
            var linkProp = effectProp.FindPropertyRelative("loopLinkIndex");
            if (linkProp == null) return;

            int maxLinkable = index;
            var labels = new GUIContent[maxLinkable + 2];
            labels[0] = new GUIContent("None",
                "No loop-back. Execution continues to the next effect.");
            for (int i = 0; i <= maxLinkable; i++)
            {
                labels[i + 1] = new GUIContent($"#{i + 1}",
                    i == index ? "Repeat this effect (self-loop)."
                               : $"Jump back to effect #{i + 1} after this one.");
            }

            int current = linkProp.intValue;
            int popupIdx = (current >= 0 && current <= maxLinkable) ? (current + 1) : 0;

            EditorGUI.indentLevel++;
            int newPopupIdx = EditorGUILayout.Popup(
                new GUIContent("Do Again",
                    "Optional 'do-again' link. After this effect resolves, jump back " +
                    $"to the chosen effect index. Capped at {SkillResolver.MaxLoopIterations} " +
                    "iterations per skill cast."),
                popupIdx, labels);
            if (newPopupIdx != popupIdx)
                linkProp.intValue = (newPopupIdx == 0) ? -1 : (newPopupIdx - 1);
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Inline stack-count widget. "Stacks = N" for Fixed; "Stacks = N per
        /// Y [Source]" for derived (so "Apply 1 Vulnerable per 2 damage" reads
        /// as intended).
        /// </summary>
        public static void DrawStackCountInline(SerializedProperty effectProp)
        {
            var kindProp   = effectProp.FindPropertyRelative("stackCountKind");
            var stacksProp = effectProp.FindPropertyRelative("conditionStacks");
            var perProp    = effectProp.FindPropertyRelative("stackPer");
            if (kindProp == null || stacksProp == null) return;

            var kind = (ConditionStackSource)kindProp.enumValueIndex;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Stacks", GUILayout.Width(EditorGUIUtility.labelWidth - 2f));
            GUILayout.Label("=", GUILayout.Width(12));
            stacksProp.intValue = EditorGUILayout.IntField(stacksProp.intValue, GUILayout.Width(48));

            if (kind != ConditionStackSource.Fixed)
            {
                GUILayout.Label("per", GUILayout.Width(26));
                if (perProp != null)
                    perProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(perProp.intValue, GUILayout.Width(48)));
                kindProp.enumValueIndex = (int)(ConditionStackSource)EditorGUILayout.EnumPopup(kind);
            }
            else
            {
                GUILayout.FlexibleSpace();
                kindProp.enumValueIndex = (int)(ConditionStackSource)EditorGUILayout.EnumPopup(kind, GUILayout.Width(140));
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void PropField(SerializedProperty parent, string name, string label = null)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return;
            if (label == null) EditorGUILayout.PropertyField(p);
            else EditorGUILayout.PropertyField(p, new GUIContent(label));
        }

        /// <summary>
        /// ConditionID enum dropdown + a tiny preview line confirming the
        /// library has a matching SO. When unregistered, shows a yellow
        /// warning with a shortcut button to refresh the library.
        /// </summary>
        public static void DrawConditionIDField(SerializedProperty effectProp)
        {
            var idProp = effectProp.FindPropertyRelative("conditionID");
            if (idProp == null) return;

            EditorStyleKit.DrawSortedEnumPopup<ConditionID>(idProp, "Condition");

            var id = (ConditionID)idProp.enumValueIndex;
            var lib = ConditionLibrary.Instance;

            if (lib == null)
            {
                EditorGUILayout.HelpBox(
                    "ConditionLibrary not found at Assets/Resources/ConditionLibrary.asset. " +
                    "Use DarkSpire → Conditions → Create Library.",
                    MessageType.Warning);
                return;
            }

            var data = lib.Get(id);
            if (data != null)
            {
                EditorGUI.indentLevel++;
                string summary = $"→ {data.name}  ·  {data.stackType}  ·  {(data.isDebuff ? "debuff" : "buff")}";
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"No ConditionData in the library has conditionID = {id}. " +
                    "Create a ConditionData SO and set its ID, or refresh the library.",
                    MessageType.Warning);
                if (GUILayout.Button("Refresh Library", GUILayout.Height(20)))
                    ConditionLibraryMenu.RefreshLibrary();
            }
        }

        public static void PlaceholderWarning()
        {
            EditorGUILayout.HelpBox(
                "Placeholder effect — inspector fields are authored, but runtime " +
                "behavior is not implemented yet. Ships with the character-kit pass.",
                MessageType.Info);
        }
    }
}
