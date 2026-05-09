// ItemLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the item library:
//
//   DarkSpire → Items → Create Library    — creates the canonical asset at
//                                            Assets/ScriptableObjects/ItemLibrary.asset,
//                                            registers it in PlayerSettings
//                                            preloaded assets, and scans for
//                                            existing items
//   DarkSpire → Items → Refresh Library   — rebuild list by scanning every
//                                            ItemData asset
//   DarkSpire → Items → Select Library    — ping the asset
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class ItemLibraryMenu
    {
        [MenuItem("DarkSpire/Items/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"ItemLibrary already lives at {ItemLibrary.AssetPath}. Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<ItemLibrary>();
            AssetDatabase.CreateAsset(lib, ItemLibrary.AssetPath);
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[ItemLibrary] Created at {ItemLibrary.AssetPath} — " +
                      $"registered {lib.All.Count} existing item(s) and added to Preloaded Assets.");
        }

        [MenuItem("DarkSpire/Items/Refresh Library")]
        public static void RefreshLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibrary.AssetPath);
            if (lib == null)
            {
                if (EditorUtility.DisplayDialog(
                    "Library missing",
                    "No ItemLibrary at the canonical path. Create one now?",
                    "Create", "Cancel"))
                {
                    CreateLibrary();
                }
                return;
            }
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemLibrary] Refreshed — {lib.All.Count} item(s) registered.");
        }

        [MenuItem("DarkSpire/Items/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {ItemLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
