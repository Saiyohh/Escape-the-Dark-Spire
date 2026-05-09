// ItemDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for ItemData. Mirrors SkillDataEditor structure: logical
// sections, conditional field reveal, the shared EffectsListDrawer for the
// effects array, a generated-effect-text preview with copy-to-field button,
// and library validation warnings.
//
// Sections:
//   • Top summary (badges: category / action cost / target / charges-mode)
//   • Identity      (itemID, itemName, icon, banner, focal, effectText)
//   • Combat        (actionCostType, primaryTargetMode, targetPickCount, diceRule)
//   • Effects       (shared EffectsListDrawer)
//   • Generated     (BuildEffectText preview + copy buttons)
//   • Inventory     (category, duration, stackable, consumedOnUse, ownerCharacter)
//   • Meta          (generatedBy, notes — collapsible)
//
// Validation: warns when itemID == None (unassigned) or when ItemLibrary
// already contains another ItemData with the same ID (duplicate).
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : Editor
    {
        private SerializedProperty itemIDProp, itemNameProp, effectTextProp;
        private SerializedProperty iconProp, artBannerProp, bannerFocalXProp, bannerFocalYProp;
        private SerializedProperty actionCostTypeProp, primaryTargetModeProp;
        private SerializedProperty diceRuleProp, effectsProp, targetPickCountProp;
        private SerializedProperty categoryProp, durationProp, stackableProp;
        private SerializedProperty consumedOnUseProp, ownerCharacterProp;
        private SerializedProperty generatedByProp, notesProp;

        private bool showMetaFoldout = false;

        private void OnEnable()
        {
            itemIDProp           = serializedObject.FindProperty("itemID");
            itemNameProp         = serializedObject.FindProperty("itemName");
            effectTextProp       = serializedObject.FindProperty("effectText");
            iconProp             = serializedObject.FindProperty("icon");
            artBannerProp        = serializedObject.FindProperty("artBanner");
            bannerFocalXProp     = serializedObject.FindProperty("bannerFocalX");
            bannerFocalYProp     = serializedObject.FindProperty("bannerFocalY");
            actionCostTypeProp   = serializedObject.FindProperty("actionCostType");
            primaryTargetModeProp = serializedObject.FindProperty("primaryTargetMode");
            diceRuleProp         = serializedObject.FindProperty("diceRule");
            effectsProp          = serializedObject.FindProperty("effects");
            targetPickCountProp  = serializedObject.FindProperty("targetPickCount");
            categoryProp         = serializedObject.FindProperty("category");
            durationProp         = serializedObject.FindProperty("duration");
            stackableProp        = serializedObject.FindProperty("stackable");
            consumedOnUseProp    = serializedObject.FindProperty("consumedOnUse");
            ownerCharacterProp   = serializedObject.FindProperty("ownerCharacter");
            generatedByProp      = serializedObject.FindProperty("generatedBy");
            notesProp            = serializedObject.FindProperty("notes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();
            DrawValidationWarnings();
            DrawIdentitySection();
            DrawCombatSection();
            DrawEffectsSection();
            DrawGeneratedEffectTextSection();
            DrawInventorySection();
            DrawMetaSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Top summary ──────────────────────────────────────────────────────
        private void DrawTopSummary()
        {
            var item = (ItemData)target;
            EditorGUILayout.Space(4);
            EditorStyleKit.BeginBadgeRow();
            EditorStyleKit.DrawBadge(item.category.ToString(), CategoryBadgeColor(item.category));
            EditorStyleKit.DrawBadge(ActionCostName(item.actionCostType),
                ActionCostBadgeColor(item.actionCostType));
            EditorStyleKit.DrawBadge(item.primaryTargetMode.ToString(),
                new Color(0.45f, 0.45f, 0.45f));
            if (item.stackable)
                EditorStyleKit.DrawBadge("Stackable", new Color(0.40f, 0.60f, 0.80f));
            if (!item.consumedOnUse)
                EditorStyleKit.DrawBadge("Reusable", new Color(0.30f, 0.60f, 0.40f));
            EditorStyleKit.EndBadgeRow();
        }

        private void DrawValidationWarnings()
        {
            var item = (ItemData)target;

            if (item.itemID == ItemID.None)
            {
                EditorGUILayout.HelpBox(
                    "Item ID is None — set it to a non-None value before runtime " +
                    "(ItemInstance can't reference an unassigned ID, so the item " +
                    "won't be grantable via GenerateItem or persisted across runs).",
                    MessageType.Warning);
            }
            else
            {
                var lib = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibrary.AssetPath);
                if (lib != null && lib.CountWithID(item.itemID) > 1)
                {
                    EditorGUILayout.HelpBox(
                        $"Another ItemData asset uses the same ItemID ({item.itemID}). " +
                        "Each ItemData must have a unique ID — only the first-registered " +
                        "wins via ItemLibrary.Get. Pick a different ID or delete the duplicate.",
                        MessageType.Error);
                }
                if (lib == null)
                {
                    EditorGUILayout.HelpBox(
                        $"ItemLibrary asset not found at {ItemLibrary.AssetPath}. " +
                        "Create one via DarkSpire → Items → Create Library.",
                        MessageType.Info);
                }
            }
        }

        // ── Identity ─────────────────────────────────────────────────────────
        private void DrawIdentitySection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Identity",
                new Color(0.55f, 0.65f, 0.75f));

            EditorGUILayout.PropertyField(itemIDProp);
            EditorGUILayout.PropertyField(itemNameProp);
            EditorGUILayout.PropertyField(iconProp,
                new GUIContent("Icon",
                    "Small icon shown on the item button in the action submenu. " +
                    "Skills don't have icons; items do."));
            EditorGUILayout.PropertyField(artBannerProp,
                new GUIContent("Art Banner",
                    "Optional wide artwork shown on the info panel banner. " +
                    "Falls back to Resources/UI/DefaultItemArtBanner if null."));

            if (artBannerProp.objectReferenceValue != null)
            {
                EditorGUILayout.Slider(bannerFocalXProp, -1f, 1f,
                    new GUIContent("Banner Focal X"));
                EditorGUILayout.Slider(bannerFocalYProp, -1f, 1f,
                    new GUIContent("Banner Focal Y"));
            }

            EditorGUILayout.PropertyField(effectTextProp,
                new GUIContent("Effect Text",
                    "Plain-English description shown in the info panel tooltip. " +
                    "Leave empty to fall back to the auto-generated text below."));
        }

        // ── Combat ───────────────────────────────────────────────────────────
        private void DrawCombatSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Combat",
                ActionCostBadgeColor((ItemActionCostType)actionCostTypeProp.enumValueIndex));

            EditorGUILayout.PropertyField(actionCostTypeProp);

            EditorGUILayout.PropertyField(primaryTargetModeProp, new GUIContent("Target"));
            var target = (TargetMode)primaryTargetModeProp.enumValueIndex;
            bool isPickable = target == TargetMode.SingleEnemy || target == TargetMode.SingleAlly;

            if (isPickable)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetPickCountProp,
                    new GUIContent("Picks",
                        "How many separate targets the player picks per use. " +
                        "Default 1 = all SingleEnemy/SingleAlly effects share " +
                        "one pick. >1 = multi-pick."));
                if (targetPickCountProp.intValue < 1) targetPickCountProp.intValue = 1;
                EditorGUI.indentLevel--;
            }
            else if (targetPickCountProp.intValue != 1)
            {
                targetPickCountProp.intValue = 1;
            }

            EditorGUILayout.PropertyField(diceRuleProp,
                new GUIContent("Dice Rule",
                    "AutoHit (default) = no roll. AttackRoll = d20+ATK vs DEF. " +
                    "WilSave = target rolls a save (success resists)."));
        }

        // ── Effects (reuses shared drawer) ───────────────────────────────────
        private void DrawEffectsSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Effects",
                new Color(0.40f, 0.60f, 0.45f));
            EffectsListDrawer.Draw(effectsProp, targetPickCountProp);
        }

        // ── Generated effect text ────────────────────────────────────────────
        private void DrawGeneratedEffectTextSection()
        {
            var item = (ItemData)target;
            EditorStyleKit.DrawColoredSectionHeader("Generated Effect Text (preview)",
                new Color(0.55f, 0.55f, 0.75f));

            string generated = item.BuildEffectText();

            EditorGUILayout.BeginHorizontal();
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(generated) ? "(no effects yet)" : generated,
                style, GUILayout.MinHeight(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to Effect Text", GUILayout.Height(22)))
            {
                effectTextProp.stringValue = generated;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(22), GUILayout.Width(160)))
            {
                EditorGUIUtility.systemCopyBuffer = generated;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Inventory ────────────────────────────────────────────────────────
        private void DrawInventorySection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Inventory",
                CategoryBadgeColor((ItemCategory)categoryProp.enumValueIndex));

            EditorGUILayout.PropertyField(categoryProp);
            EditorGUILayout.PropertyField(durationProp);
            EditorGUILayout.PropertyField(stackableProp);
            EditorGUILayout.PropertyField(consumedOnUseProp);

            // ownerCharacter only matters for Pouch items.
            var category = (ItemCategory)categoryProp.enumValueIndex;
            if (category == ItemCategory.Pouch)
            {
                EditorGUILayout.PropertyField(ownerCharacterProp,
                    new GUIContent("Owner",
                        "Pouch items are character-locked. Set this to the " +
                        "character who can use this item."));
                if (ownerCharacterProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "Pouch items must have an Owner. Without one, the item " +
                        "won't be granted to anyone's pouch by default startup.",
                        MessageType.Warning);
                }
            }
            else if (ownerCharacterProp.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox(
                    $"Owner is set but Category is {category}. Owner is only " +
                    "honored for Pouch items — clear it or change the category.",
                    MessageType.Info);
            }
        }

        // ── Meta (collapsible) ───────────────────────────────────────────────
        private void DrawMetaSection()
        {
            EditorGUILayout.Space(6);
            showMetaFoldout = EditorGUILayout.Foldout(showMetaFoldout, "Design Meta", true);
            if (!showMetaFoldout) return;

            EditorGUILayout.PropertyField(generatedByProp);
            EditorGUILayout.PropertyField(notesProp);
        }

        // ── Display helpers ──────────────────────────────────────────────────
        private static Color CategoryBadgeColor(ItemCategory c) => c switch
        {
            ItemCategory.Pouch          => new Color(0.85f, 0.55f, 0.30f),
            ItemCategory.PartyInventory => new Color(0.30f, 0.55f, 0.85f),
            ItemCategory.BrewedPotion   => new Color(0.40f, 0.75f, 0.45f),
            _ => Color.gray,
        };

        private static Color ActionCostBadgeColor(ItemActionCostType c) => c switch
        {
            ItemActionCostType.Action     => new Color(0.85f, 0.30f, 0.30f),
            ItemActionCostType.FreeAction => new Color(0.85f, 0.75f, 0.30f),
            ItemActionCostType.ZeroCost   => new Color(0.55f, 0.40f, 0.75f),
            _ => Color.gray,
        };

        private static string ActionCostName(ItemActionCostType c) => c switch
        {
            ItemActionCostType.Action     => "Action",
            ItemActionCostType.FreeAction => "Free Action",
            ItemActionCostType.ZeroCost   => "Zero Cost",
            _ => c.ToString(),
        };
    }
}
