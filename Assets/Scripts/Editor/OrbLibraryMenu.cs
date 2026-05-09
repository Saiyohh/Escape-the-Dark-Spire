// OrbLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the orb library, mirroring DarkSpire/Conditions:
//
//   DarkSpire → Orbs → Create Library    — creates the asset at the canonical
//                                          path, registers in PlayerSettings
//                                          preloaded assets, and scans the
//                                          project for existing OrbDataSO.
//   DarkSpire → Orbs → Refresh Library   — rebuild the list from project.
//   DarkSpire → Orbs → Select Library    — ping the asset in Project window.
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class OrbLibraryMenu
    {
        [MenuItem("DarkSpire/Orbs/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<OrbLibrary>(OrbLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"OrbLibrary already lives at {OrbLibrary.AssetPath}. Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<OrbLibrary>();
            AssetDatabase.CreateAsset(lib, OrbLibrary.AssetPath);
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[OrbLibrary] Created at {OrbLibrary.AssetPath} — " +
                      $"registered {lib.All.Count} existing orb(s) and added to Preloaded Assets.");
        }

        [MenuItem("DarkSpire/Orbs/Refresh Library")]
        public static void RefreshLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<OrbLibrary>(OrbLibrary.AssetPath);
            if (lib == null)
            {
                if (EditorUtility.DisplayDialog(
                    "Library missing",
                    "No OrbLibrary at the canonical path. Create one now?",
                    "Create", "Cancel"))
                {
                    CreateLibrary();
                }
                return;
            }
            lib.RefreshFromProject();
            AssetDatabase.SaveAssets();
            Debug.Log($"[OrbLibrary] Refreshed — {lib.All.Count} orb(s) registered.");
        }

        [MenuItem("DarkSpire/Orbs/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<OrbLibrary>(OrbLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {OrbLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
