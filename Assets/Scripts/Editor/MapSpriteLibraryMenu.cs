// MapSpriteLibraryMenu.cs
// -----------------------------------------------------------------------------
// Menu shortcuts for the map-entity sprite library, mirroring DarkSpire/Conditions
// and DarkSpire/Orbs:
//
//   DarkSpire → Map Sprites → Create Library    — creates the canonical asset
//                                                  at Assets/ScriptableObjects/MapEntitySpriteLibrary.asset,
//                                                  registers it in PlayerSettings
//                                                  preloaded assets so runtime
//                                                  loading works without a
//                                                  Resources folder.
//   DarkSpire → Map Sprites → Select Library    — ping the asset in the Project
//                                                  window + show in Inspector.
//
// No "Refresh" action — unlike conditions/orbs, this library is a single
// flat record of named slots rather than a registry of N child SOs. Designers
// edit the slots directly in the inspector.
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class MapSpriteLibraryMenu
    {
        [MenuItem("DarkSpire/Map Sprites/Create Library")]
        public static void CreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MapEntitySpriteLibrary>(MapEntitySpriteLibrary.AssetPath);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Library already exists",
                    $"MapEntitySpriteLibrary already lives at {MapEntitySpriteLibrary.AssetPath}. " +
                    "Opening it.",
                    "OK");
                LibraryPreloadRegistrar.Ensure(existing);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!Directory.Exists("Assets/ScriptableObjects"))
                Directory.CreateDirectory("Assets/ScriptableObjects");

            var lib = ScriptableObject.CreateInstance<MapEntitySpriteLibrary>();
            AssetDatabase.CreateAsset(lib, MapEntitySpriteLibrary.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Without a Resources folder, runtime access goes through
            // PlayerSettings.preloadedAssets. Same registrar used by the
            // condition / orb libraries.
            LibraryPreloadRegistrar.Ensure(lib);

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[MapEntitySpriteLibrary] Created at {MapEntitySpriteLibrary.AssetPath} " +
                      "and added to Preloaded Assets. Drag your dungeon sprites " +
                      "into the slots in the Inspector.");
        }

        [MenuItem("DarkSpire/Map Sprites/Select Library")]
        public static void SelectLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<MapEntitySpriteLibrary>(MapEntitySpriteLibrary.AssetPath);
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Library missing",
                    $"No library at {MapEntitySpriteLibrary.AssetPath}. Use 'Create Library' first.",
                    "OK");
                return;
            }
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
    }
}
