// CombatTestLauncher.cs
// -----------------------------------------------------------------------------
// One-click combat test launcher. Opens the combat test scene, stamps a
// CombatTestPreset onto the scene's CombatBootstrap, and optionally enters
// Play mode. Avoids the "re-wire the inspector every session" tax.
//
// Menu:
//   DarkSpire/Testing/Load Combat Scene               — load scene only
//   DarkSpire/Testing/Load Combat Scene & Play        — load + press Play
//   DarkSpire/Testing/Select Active Preset…           — pick a different preset
//
// The launcher remembers the last preset used via EditorPrefs, so the two
// quick-launch commands don't need a picker once a preset is set.
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class CombatTestLauncher
    {
        private const string ScenePath = "Assets/Scenes/CombatTest.unity";
        private const string PresetPrefKey = "DarkSpire.CombatTestLauncher.PresetGuid";

        [MenuItem("DarkSpire/Testing/Load Combat Scene %#t")] // Ctrl+Shift+T
        public static void LoadCombatScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Scene missing",
                    $"Couldn't find {ScenePath}. Create the CombatTest scene first " +
                    "(see the Combat Test Setup Runbook).",
                    "OK");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyPresetToScene(out string report);
            Debug.Log($"[CombatTestLauncher] {report}");
        }

        [MenuItem("DarkSpire/Testing/Load Combat Scene && Play %#&t")] // Ctrl+Shift+Alt+T
        public static void LoadCombatSceneAndPlay()
        {
            LoadCombatScene();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("DarkSpire/Testing/Select Active Preset…")]
        public static void SelectActivePreset()
        {
            string pickerTitle = "Pick a CombatTestPreset";
            string pickedPath = EditorUtility.OpenFilePanelWithFilters(
                pickerTitle,
                "Assets",
                new[] { "ScriptableObject", "asset" });

            if (string.IsNullOrEmpty(pickedPath)) return;

            // Convert absolute path to project-relative
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relPath = Path.GetRelativePath(projectRoot, pickedPath).Replace('\\', '/');

            var preset = AssetDatabase.LoadAssetAtPath<CombatTestPreset>(relPath);
            if (preset == null)
            {
                EditorUtility.DisplayDialog("Wrong asset type",
                    "That asset isn't a CombatTestPreset.", "OK");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(relPath);
            EditorPrefs.SetString(PresetPrefKey, guid);
            Debug.Log($"[CombatTestLauncher] Active preset set to {preset.name}");
        }

        /// <summary>Finds CombatBootstrap in the active scene and copies preset fields onto it.</summary>
        private static void ApplyPresetToScene(out string report)
        {
            var preset = ResolveActivePreset();
            if (preset == null)
            {
                report = "Scene loaded, but no active preset set. Use 'Select Active Preset…' " +
                         "or drag a CombatTestPreset onto the CombatBootstrap manually.";
                return;
            }

            var bootstrap = Object.FindAnyObjectByType<CombatBootstrap>();
            if (bootstrap == null)
            {
                report = $"Scene loaded, but no CombatBootstrap found to apply preset '{preset.name}'.";
                return;
            }

            Undo.RecordObject(bootstrap, "Apply Combat Test Preset");
            var so = new SerializedObject(bootstrap);
            SetArray(so, "party", preset.party);
            SetObject(so, "encounter", preset.encounter);
            SetInt(so, "startingGold", preset.startingGold);
            SetInt(so, "currentFloor", preset.currentFloor);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);

            report = $"Applied preset '{preset.name}' → party={preset.party?.Length ?? 0}, " +
                     $"encounter={(preset.encounter ? preset.encounter.name : "null")}. " +
                     $"Conditions resolved via ConditionLibrary.Instance.";
        }

        private static CombatTestPreset ResolveActivePreset()
        {
            string guid = EditorPrefs.GetString(PresetPrefKey, "");
            if (string.IsNullOrEmpty(guid)) return FindFirstPresetInProject();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var preset = AssetDatabase.LoadAssetAtPath<CombatTestPreset>(path);
            return preset != null ? preset : FindFirstPresetInProject();
        }

        private static CombatTestPreset FindFirstPresetInProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:CombatTestPreset");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<CombatTestPreset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // -- small SerializedProperty helpers --------------------------------------

        private static void SetArray<T>(SerializedObject so, string propName, T[] source)
            where T : Object
        {
            var prop = so.FindProperty(propName);
            if (prop == null || !prop.isArray) return;
            int len = source?.Length ?? 0;
            prop.arraySize = len;
            for (int i = 0; i < len; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = source[i];
        }

        private static void SetObject(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
        }

        private static void SetInt(SerializedObject so, string propName, int value)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.intValue = value;
        }
    }
}
