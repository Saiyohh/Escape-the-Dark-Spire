// LibraryPreloadRegistrar.cs
// -----------------------------------------------------------------------------
// Editor utility for managing PlayerSettings.preloadedAssets. Libraries
// (ConditionLibrary, CharacterLibrary) live outside any Resources folder, so
// their runtime loading goes through Unity's preloaded-assets list — the
// engine loads them into memory at game start, their OnEnable fires, and
// the static Instance gets wired up.
//
// Call Ensure(lib) after creating or opening a library asset to add it to
// the list if it isn't already there. Idempotent — safe to call repeatedly.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class LibraryPreloadRegistrar
    {
        /// <summary>
        /// Make sure the given asset is in Project Settings → Player →
        /// Preloaded Assets. Does nothing if it's already in the list.
        /// </summary>
        public static void Ensure(Object asset)
        {
            if (asset == null) return;

            var preloaded = PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < preloaded.Length; i++)
            {
                if (preloaded[i] == asset) return; // already registered
            }

            // Append. GetPreloadedAssets returns a copy, so it's safe to grow.
            var updated = new Object[preloaded.Length + 1];
            System.Array.Copy(preloaded, updated, preloaded.Length);
            updated[preloaded.Length] = asset;
            PlayerSettings.SetPreloadedAssets(updated);
        }

        /// <summary>
        /// Remove the given asset from the preloaded list (if present). Used
        /// when a library is deleted so we don't leave a stale null entry.
        /// </summary>
        public static void Remove(Object asset)
        {
            if (asset == null) return;

            var preloaded = PlayerSettings.GetPreloadedAssets();
            var filtered = new System.Collections.Generic.List<Object>(preloaded.Length);
            for (int i = 0; i < preloaded.Length; i++)
            {
                if (preloaded[i] != asset) filtered.Add(preloaded[i]);
            }
            if (filtered.Count != preloaded.Length)
                PlayerSettings.SetPreloadedAssets(filtered.ToArray());
        }
    }
}
