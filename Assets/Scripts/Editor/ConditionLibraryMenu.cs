// ConditionLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the condition library:
//
//   DarkSpire → Conditions → Create Library    — creates the canonical asset
//                                                 at Assets/ScriptableObjects/ConditionLibrary.asset,
//                                                 registers it in PlayerSettings
//                                                 preloaded assets, and scans the
//                                                 project for existing conditions
//   DarkSpire → Conditions → Refresh Library   — rebuild the list by scanning
//                                                 every ConditionData asset
//   DarkSpire → Conditions → Select Library    — ping the asset in the Project
//                                                 window + show in Inspector
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class ConditionLibraryMenu
    {
        [MenuItem("DarkSpire/Conditions/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ConditionLibrary>(ConditionLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"ConditionLibrary already lives at {ConditionLibrary.AssetPath}. " +
                    "Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            // Ensure target folder exists (non-Resources, root ScriptableObjects).
            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<ConditionLibrary>();
            AssetDatabase.CreateAsset(lib, ConditionLibrary.AssetPath);
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Without a Resources folder, runtime access needs the library in
            // PlayerSettings' preloaded assets list — add it now.
            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[ConditionLibrary] Created at {ConditionLibrary.AssetPath} — " +
                      $"registered {lib.All.Count} existing condition(s) and added to Preloaded Assets.");
        }

        [MenuItem("DarkSpire/Conditions/Refresh Library")]
        public static void RefreshLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ConditionLibrary>(ConditionLibrary.AssetPath);
            if (lib == null)
            {
                if (EditorUtility.DisplayDialog(
                    "Library missing",
                    "No ConditionLibrary at the canonical path. Create one now?",
                    "Create", "Cancel"))
                {
                    CreateLibrary();
                }
                return;
            }
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ConditionLibrary] Refreshed — {lib.All.Count} condition(s) registered.");
        }

        [MenuItem("DarkSpire/Conditions/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ConditionLibrary>(ConditionLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {ConditionLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
