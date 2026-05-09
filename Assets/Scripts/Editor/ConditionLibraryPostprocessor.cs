// ConditionLibraryPostprocessor.cs
// -----------------------------------------------------------------------------
// Auto-registers every ConditionData SO in the project into the
// ConditionLibrary asset. When the designer creates a new ConditionData
// anywhere in the project (or imports one), Unity fires OnPostprocessAllAssets;
// this hook loads the library (if it exists) and adds the new entry.
//
// Deletions are handled lazily — the library's serialized list will contain
// null entries which ConditionLibrary filters out on read. If the designer
// wants a hard-clean, they can use DarkSpire → Conditions → Refresh Library
// (see ConditionLibraryMenu).
//
// If the library asset doesn't exist yet, this postprocessor silently skips —
// no-op until the library is created.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public class ConditionLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Skip work if we're the ones that triggered the import (common
            // during library edits — avoids recursion).
            bool dirty = false;
            ConditionLibrary library = null;

            // Lazy-load library only if there's anything to do
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (!path.EndsWith(".asset")) continue;
                var c = AssetDatabase.LoadAssetAtPath<ConditionData>(path);
                if (c == null) continue;

                if (library == null) library = LoadLibrary();
                if (library == null) return; // no library yet — nothing to do

                library.Register(c);
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssetIfDirty(library);
            }
        }

        private static ConditionLibrary LoadLibrary()
        {
            // Check via AssetDatabase so we don't care whether Resources folder
            // has loaded the singleton yet.
            var lib = AssetDatabase.LoadAssetAtPath<ConditionLibrary>(ConditionLibrary.AssetPath);
            return lib;
        }
    }
}
