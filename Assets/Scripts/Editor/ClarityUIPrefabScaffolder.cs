// ClarityUIPrefabScaffolder.cs
// -----------------------------------------------------------------------------
// Materializes each runtime-fallback canvas (RestMenu, FirstRunIntroCard,
// IconLegendModal, LegendButton, MapHoverTooltip) as an editable prefab under
// Assets/Prefabs/ClarityUI/, then wires every prefab into the
// ClarityUIPrefabLibrary SO at Assets/ScriptableObjects/ClarityUIPrefabLibrary.asset
// and registers that library into PlayerSettings → Preloaded Assets.
//
// Runtime path after scaffolding:
//   FirstRunIntroCard.GetOrCreate()
//   → ClarityUIPrefabLibrary.Instance.firstRunIntro
//   → Instantiate(prefab)
//
// If the library or its prefab field is unset, GetOrCreate falls back to the
// code-built runtime fallback. So nothing breaks if the scaffolder hasn't been
// run yet.
//
// Re-running prompts before overwriting any existing prefab.
// -----------------------------------------------------------------------------
#if UNITY_EDITOR
using System;
using System.IO;
using DarkSpire.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DarkSpire
{
    public static class ClarityUIPrefabScaffolder
    {
        private const string PrefabDir       = "Assets/Prefabs/ClarityUI";
        private const string ScriptableDir   = "Assets/ScriptableObjects";

        // Default TMP font auto-resolved on library creation if found at one of
        // these paths. The user can swap it out from the library asset later.
        private static readonly string[] DefaultFontCandidatePaths =
        {
            "Assets/Fonts/Kreon Regular/kreon_regular SDF.asset",
            "Assets/Fonts/Kreon_Regular_SDF.asset",
        };

        [MenuItem("Tools/DarkSpire/Clarity UI/Scaffold All Layouts as Prefabs")]
        public static void ScaffoldAll()
        {
            EnsureDir(PrefabDir);
            EnsureDir(ScriptableDir);

            var lib = ResolveOrCreateLibrary();

            int created = 0;
            created += ScaffoldOne(lib, PrefabField.RestMenu)       ? 1 : 0;
            created += ScaffoldOne(lib, PrefabField.FirstRunIntro)  ? 1 : 0;
            created += ScaffoldOne(lib, PrefabField.IconLegend)     ? 1 : 0;
            created += ScaffoldOne(lib, PrefabField.MapHoverTooltip)? 1 : 0;

            EditorUtility.SetDirty(lib);
            LibraryPreloadRegistrar.Ensure(lib);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Clarity UI Prefabs",
                $"Done. {created} prefab(s) processed under {PrefabDir}/.\n\n" +
                $"Library: {ClarityUIPrefabLibrary.AssetPath}\n" +
                "Registered into Project Settings → Player → Preloaded Assets.\n\n" +
                "Open any prefab in the Project window and edit layouts in the " +
                "Inspector. Next play session loads your authored versions " +
                "instead of the runtime fallbacks.",
                "OK");
        }

        [MenuItem("Tools/DarkSpire/Clarity UI/Scaffold RestMenu Prefab")]
        public static void ScaffoldRest() => ScaffoldSingle(PrefabField.RestMenu);

        [MenuItem("Tools/DarkSpire/Clarity UI/Scaffold FirstRunIntro Prefab")]
        public static void ScaffoldIntro() => ScaffoldSingle(PrefabField.FirstRunIntro);

        [MenuItem("Tools/DarkSpire/Clarity UI/Scaffold IconLegend Prefab")]
        public static void ScaffoldLegend() => ScaffoldSingle(PrefabField.IconLegend);

        [MenuItem("Tools/DarkSpire/Clarity UI/Scaffold MapHoverTooltip Prefab")]
        public static void ScaffoldHoverTooltip() => ScaffoldSingle(PrefabField.MapHoverTooltip);

        [MenuItem("Tools/DarkSpire/Clarity UI/Wire Default Font (Kreon)")]
        public static void WireDefaultFont()
        {
            EnsureDir(ScriptableDir);
            var lib = ResolveOrCreateLibrary();
            LibraryPreloadRegistrar.Ensure(lib);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string status = lib.defaultFont != null
                ? $"Default font set to '{lib.defaultFont.name}'."
                : "No font asset found at the candidate paths. Drop a " +
                  "TMP_FontAsset into the library's Default Font field.";

            EditorUtility.DisplayDialog("Default UI Font",
                $"{status}\n\nLibrary: {ClarityUIPrefabLibrary.AssetPath}\n\n" +
                "Runtime-built UI text (flash messages, [E] prompts, modal " +
                "labels, etc.) calls UIFonts.Apply, which reads this field.",
                "OK");
        }

        // ─── Per-prefab metadata ───────────────────────────────────────────

        private enum PrefabField
        {
            RestMenu,
            FirstRunIntro,
            IconLegend,
            MapHoverTooltip,
        }

        private static (string fileName, Type controllerType) Meta(PrefabField f) => f switch
        {
            PrefabField.RestMenu        => ("RestMenu",        typeof(RestMenu)),
            PrefabField.FirstRunIntro   => ("FirstRunIntro",   typeof(FirstRunIntroCard)),
            PrefabField.IconLegend      => ("IconLegend",      typeof(IconLegendModal)),
            PrefabField.MapHoverTooltip => ("MapHoverTooltip", typeof(MapHoverTooltip)),
            _ => default,
        };

        // ─── Core helpers ──────────────────────────────────────────────────

        private static void ScaffoldSingle(PrefabField field)
        {
            EnsureDir(PrefabDir);
            EnsureDir(ScriptableDir);

            var lib = ResolveOrCreateLibrary();
            bool created = ScaffoldOne(lib, field);

            EditorUtility.SetDirty(lib);
            LibraryPreloadRegistrar.Ensure(lib);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (created)
            {
                var meta = Meta(field);
                EditorUtility.DisplayDialog("Clarity UI Prefab",
                    $"Created {PrefabDir}/{meta.fileName}.prefab and wired " +
                    $"it into {ClarityUIPrefabLibrary.AssetPath}.\n\n" +
                    "Open the prefab in the Project window and edit in the " +
                    "Inspector.",
                    "OK");
            }
        }

        /// <summary>
        /// Returns true if a prefab was created/updated, false if the user
        /// cancelled or the builder failed.
        /// </summary>
        private static bool ScaffoldOne(ClarityUIPrefabLibrary lib, PrefabField field)
        {
            var meta = Meta(field);
            string assetPath = $"{PrefabDir}/{meta.fileName}.prefab";

            if (File.Exists(assetPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite existing prefab?",
                    $"{assetPath} already exists. Overwrite it with a fresh " +
                    "runtime fallback layout? Your previous Inspector tweaks " +
                    "WILL BE LOST.",
                    "Overwrite", "Skip"))
                {
                    // Even if skipped, make sure the library still references
                    // the existing prefab — fixes cases where a prefab was
                    // moved/restored without re-running the scaffolder.
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (existing != null) AssignToLibrary(lib, field, existing);
                    return false;
                }
            }

            GameObject runtimeInstance = null;
            try
            {
                runtimeInstance = InvokeStaticBuilder(meta.controllerType);
                if (runtimeInstance == null)
                {
                    Debug.LogError($"[ClarityUIPrefabScaffolder] Builder for '{meta.fileName}' returned null.");
                    return false;
                }
                runtimeInstance.name = meta.fileName;

                var saved = PrefabUtility.SaveAsPrefabAsset(runtimeInstance, assetPath, out bool success);
                if (!success || saved == null)
                {
                    Debug.LogError($"[ClarityUIPrefabScaffolder] Failed to save prefab at {assetPath}.");
                    return false;
                }

                AssignToLibrary(lib, field, saved);
                Debug.Log($"[ClarityUIPrefabScaffolder] Wrote {assetPath} and wired into library.");
                return true;
            }
            finally
            {
                if (runtimeInstance != null) UnityEngine.Object.DestroyImmediate(runtimeInstance);
            }
        }

        private static void AssignToLibrary(ClarityUIPrefabLibrary lib, PrefabField field, GameObject prefab)
        {
            switch (field)
            {
                case PrefabField.RestMenu:        lib.restMenu        = prefab; break;
                case PrefabField.FirstRunIntro:   lib.firstRunIntro   = prefab; break;
                case PrefabField.IconLegend:      lib.iconLegend      = prefab; break;
                case PrefabField.MapHoverTooltip: lib.mapHoverTooltip = prefab; break;
            }
        }

        private static ClarityUIPrefabLibrary ResolveOrCreateLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ClarityUIPrefabLibrary>(
                ClarityUIPrefabLibrary.AssetPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ClarityUIPrefabLibrary>();
                AssetDatabase.CreateAsset(lib, ClarityUIPrefabLibrary.AssetPath);
                Debug.Log($"[ClarityUIPrefabScaffolder] Created {ClarityUIPrefabLibrary.AssetPath}.");
            }

            // Auto-resolve the default font if the user hasn't picked one yet.
            // Tried once per scaffolder run; harmless to retry if the candidate
            // assets move.
            if (lib.defaultFont == null)
            {
                foreach (var path in DefaultFontCandidatePaths)
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    if (font != null)
                    {
                        lib.defaultFont = font;
                        EditorUtility.SetDirty(lib);
                        Debug.Log($"[ClarityUIPrefabScaffolder] Wired default font from {path}.");
                        break;
                    }
                }
                if (lib.defaultFont == null)
                {
                    Debug.LogWarning("[ClarityUIPrefabScaffolder] Default font not " +
                        "found at any candidate path. Drop a TMP_FontAsset into " +
                        $"{ClarityUIPrefabLibrary.AssetPath} → Default Font.");
                }
            }

            return lib;
        }

        /// <summary>
        /// Each controller's runtime fallback lives in a private static
        /// `BuildRuntimeFallback()` method on the controller type. Reflection
        /// is the lightest way to expose it to the editor tool without a
        /// breaking-change refactor of every controller's public API.
        /// </summary>
        private static GameObject InvokeStaticBuilder(Type controllerType)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public;

            var method = controllerType.GetMethod("BuildRuntimeFallback", flags);
            if (method == null)
            {
                Debug.LogError($"[ClarityUIPrefabScaffolder] No BuildRuntimeFallback on {controllerType.Name}.");
                return null;
            }
            return method.Invoke(null, null) as GameObject;
        }

        private static void EnsureDir(string path)
        {
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
#endif
