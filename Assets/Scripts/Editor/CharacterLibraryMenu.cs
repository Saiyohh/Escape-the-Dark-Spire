// CharacterLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the character library. Mirrors ConditionLibraryMenu:
//
//   DarkSpire → Characters → Create Library    — creates the canonical asset
//                                                 at Assets/ScriptableObjects/CharacterLibrary.asset,
//                                                 registers it in PlayerSettings
//                                                 preloaded assets, and scans the
//                                                 project for existing CharacterData SOs
//   DarkSpire → Characters → Refresh Library   — rebuild the list by scanning
//                                                 every CharacterData asset
//   DarkSpire → Characters → Select Library    — ping the asset in the Project
//                                                 window + show in Inspector
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class CharacterLibraryMenu
    {
        [MenuItem("DarkSpire/Characters/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(CharacterLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"CharacterLibrary already lives at {CharacterLibrary.AssetPath}. " +
                    "Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<CharacterLibrary>();
            AssetDatabase.CreateAsset(lib, CharacterLibrary.AssetPath);
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[CharacterLibrary] Created at {CharacterLibrary.AssetPath} — " +
                      $"registered {lib.All.Count} existing character(s) and added to Preloaded Assets.");
        }

        [MenuItem("DarkSpire/Characters/Refresh Library")]
        public static void RefreshLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(CharacterLibrary.AssetPath);
            if (lib == null)
            {
                if (EditorUtility.DisplayDialog(
                    "Library missing",
                    "No CharacterLibrary at the canonical path. Create one now?",
                    "Create", "Cancel"))
                {
                    CreateLibrary();
                }
                return;
            }
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterLibrary] Refreshed — {lib.All.Count} character(s) registered.");
        }

        [MenuItem("DarkSpire/Characters/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(CharacterLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {CharacterLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
