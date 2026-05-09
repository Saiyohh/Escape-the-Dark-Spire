// ColorLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the color library:
//
//   DarkSpire → Colors → Create Library     — creates the canonical asset
//                                              at Assets/ScriptableObjects/ColorLibrary.asset
//                                              and registers it as a Preloaded Asset
//   DarkSpire → Colors → Select Library     — ping the asset in the Project
//                                              window + show in Inspector
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class ColorLibraryMenu
    {
        [MenuItem("DarkSpire/Colors/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ColorLibrary>(ColorLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"ColorLibrary already lives at {ColorLibrary.AssetPath}. Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<ColorLibrary>();
            AssetDatabase.CreateAsset(lib, ColorLibrary.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[ColorLibrary] Created at {ColorLibrary.AssetPath} and added to Preloaded Assets.");
        }

        [MenuItem("DarkSpire/Colors/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ColorLibrary>(ColorLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {ColorLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
