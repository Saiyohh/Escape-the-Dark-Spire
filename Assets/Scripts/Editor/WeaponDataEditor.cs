// WeaponDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for WeaponData. Groups into sections and only exposes the
// on-hit/on-miss condition sub-fields when a ConditionData is actually
// assigned — keeps the "Stacks" and "Chance" fields out of the designer's
// face when there's no condition to apply.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(WeaponData))]
    public class WeaponDataEditor : Editor
    {
        private SerializedProperty weaponNameProp, descriptionProp, iconProp;
        private SerializedProperty baseDamageProp, attackBonusProp, hitCountProp, critThresholdProp;
        private SerializedProperty targetModeProp, rangeMinProp, rangeMaxProp;
        private SerializedProperty hasOnHitProp, onHitConditionIDProp, onHitConditionStacksProp, onHitConditionChanceProp;
        private SerializedProperty hasOnMissProp, onMissConditionIDProp, onMissConditionStacksProp;
        private SerializedProperty alignmentProp, rarityProp;
        private SerializedProperty upgradedVersionProp, maxUpgradeTierProp, shopCostProp;

        private bool metaFoldout = false;

        private void OnEnable()
        {
            weaponNameProp              = serializedObject.FindProperty("weaponName");
            descriptionProp             = serializedObject.FindProperty("description");
            iconProp                    = serializedObject.FindProperty("icon");
            baseDamageProp              = serializedObject.FindProperty("baseDamage");
            attackBonusProp             = serializedObject.FindProperty("attackBonus");
            hitCountProp                = serializedObject.FindProperty("hitCount");
            critThresholdProp           = serializedObject.FindProperty("critThreshold");
            targetModeProp              = serializedObject.FindProperty("targetMode");
            rangeMinProp                = serializedObject.FindProperty("rangeMin");
            rangeMaxProp                = serializedObject.FindProperty("rangeMax");
            hasOnHitProp                = serializedObject.FindProperty("hasOnHit");
            onHitConditionIDProp        = serializedObject.FindProperty("onHitConditionID");
            onHitConditionStacksProp    = serializedObject.FindProperty("onHitConditionStacks");
            onHitConditionChanceProp    = serializedObject.FindProperty("onHitConditionChance");
            hasOnMissProp               = serializedObject.FindProperty("hasOnMiss");
            onMissConditionIDProp       = serializedObject.FindProperty("onMissConditionID");
            onMissConditionStacksProp   = serializedObject.FindProperty("onMissConditionStacks");
            alignmentProp               = serializedObject.FindProperty("alignment");
            rarityProp                  = serializedObject.FindProperty("rarity");
            upgradedVersionProp         = serializedObject.FindProperty("upgradedVersion");
            maxUpgradeTierProp          = serializedObject.FindProperty("maxUpgradeTier");
            shopCostProp                = serializedObject.FindProperty("shopCost");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();

            EditorStyleKit.DrawCharacterSectionHeader("Identity",
                (Alignment)alignmentProp.enumValueIndex);
            EditorGUILayout.PropertyField(weaponNameProp);
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(descriptionProp);

            EditorStyleKit.DrawColoredSectionHeader("Combat Stats",
                new Color(0.82f, 0.38f, 0.35f));
            EditorGUILayout.PropertyField(baseDamageProp,
                new GUIContent("Base Damage", "Raw damage before POW and conditions."));
            EditorGUILayout.PropertyField(attackBonusProp,
                new GUIContent("Attack Bonus", "Added to d20 when rolling to hit."));
            EditorGUILayout.PropertyField(hitCountProp,
                new GUIContent("Hit Count", "How many times the attack rolls (multi-hit weapons)."));
            EditorGUILayout.PropertyField(critThresholdProp,
                new GUIContent("Crit Threshold", "Raw d20 >= this triggers crit (2× base dmg). 20 = crit on nat-20 only."));
            EditorGUILayout.PropertyField(targetModeProp,
                new GUIContent("Basic Attack Target Mode"));

            EditorStyleKit.DrawColoredSectionHeader("Range", new Color(0.45f, 0.45f, 0.45f));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rangeMinProp, new GUIContent("Min"));
            EditorGUILayout.PropertyField(rangeMaxProp, new GUIContent("Max"));
            EditorGUILayout.EndHorizontal();

            // ── On-Hit: hide all fields unless hasOnHit is checked ─────────
            EditorStyleKit.DrawColoredSectionHeader("On Hit", new Color(0.90f, 0.50f, 0.45f));
            EditorGUILayout.PropertyField(hasOnHitProp, new GUIContent("Has On-Hit Effect"));
            if (hasOnHitProp.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawConditionIDField(onHitConditionIDProp);
                EditorGUILayout.PropertyField(onHitConditionStacksProp, new GUIContent("Stacks"));
                EditorGUILayout.Slider(onHitConditionChanceProp, 0f, 1f,
                    new GUIContent("Chance", "0-1 probability of applying on a successful hit."));
                EditorGUI.indentLevel--;
            }

            EditorStyleKit.DrawColoredSectionHeader("On Miss", new Color(0.55f, 0.45f, 0.50f));
            EditorGUILayout.PropertyField(hasOnMissProp, new GUIContent("Has On-Miss Effect"));
            if (hasOnMissProp.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawConditionIDField(onMissConditionIDProp);
                EditorGUILayout.PropertyField(onMissConditionStacksProp, new GUIContent("Stacks"));
                EditorGUI.indentLevel--;
            }

            // ── Meta (collapsible) ───────────────────────────────────────────
            EditorGUILayout.Space(6);
            metaFoldout = EditorGUILayout.Foldout(metaFoldout, "Meta / Progression", true);
            if (metaFoldout)
            {
                EditorGUILayout.PropertyField(alignmentProp);
                EditorGUILayout.PropertyField(rarityProp);
                EditorGUILayout.PropertyField(maxUpgradeTierProp);
                EditorGUILayout.PropertyField(shopCostProp);
                EditorGUILayout.PropertyField(upgradedVersionProp);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws a ConditionID dropdown for the given property + a tiny preview
        /// line showing the SO the library resolves that ID to. Shares behavior
        /// with SkillDataEditor's version.
        /// </summary>
        private static void DrawConditionIDField(SerializedProperty idProp)
        {
            EditorStyleKit.DrawSortedEnumPopup<ConditionID>(idProp, "Condition");
            var id = (ConditionID)idProp.enumValueIndex;
            var lib = ConditionLibrary.Instance;
            if (lib == null)
            {
                EditorGUILayout.HelpBox(
                    "ConditionLibrary missing. Use DarkSpire → Conditions → Create Library.",
                    MessageType.Warning);
                return;
            }
            var data = lib.Get(id);
            if (data != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    $"→ {data.name}  ·  {data.stackType}  ·  {(data.isDebuff ? "debuff" : "buff")}",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"No ConditionData in the library has conditionID = {id}.",
                    MessageType.Warning);
            }
        }

        private static void Section(string text) => EditorStyleKit.DrawSectionHeader(text);

        /// <summary>
        /// Top-of-inspector stat-badge row: character color, damage, atk bonus,
        /// crit threshold, hit count, range. Lets the designer skim a weapon's
        /// feel in one glance before diving into sections.
        /// </summary>
        private void DrawTopSummary()
        {
            var weapon = (WeaponData)target;

            EditorGUILayout.Space(4);
            EditorStyleKit.BeginBadgeRow();

            EditorStyleKit.DrawBadge(weapon.alignment.ToString(),
                EditorStyleKit.CharacterSignature(weapon.alignment));

            EditorStyleKit.DrawBadge(weapon.rarity.ToString(),
                EditorStyleKit.TierColor(weapon.rarity));

            EditorStyleKit.DrawBadge($"DMG {weapon.baseDamage}",
                new Color(0.82f, 0.32f, 0.30f));

            EditorStyleKit.DrawBadge($"ATK +{weapon.attackBonus}",
                new Color(0.92f, 0.58f, 0.26f));

            if (weapon.hitCount > 1)
                EditorStyleKit.DrawBadge($"×{weapon.hitCount}",
                    new Color(0.88f, 0.72f, 0.30f));

            EditorStyleKit.DrawBadge($"crit {weapon.critThreshold}+",
                new Color(0.95f, 0.75f, 0.25f));

            EditorStyleKit.DrawBadge($"R {weapon.rangeMin}-{weapon.rangeMax}",
                new Color(0.45f, 0.55f, 0.45f));

            if (weapon.hasOnHit)
                EditorStyleKit.DrawBadge($"OnHit {weapon.onHitConditionID}",
                    new Color(0.62f, 0.41f, 0.78f));

            EditorStyleKit.EndBadgeRow();
        }
    }
}
