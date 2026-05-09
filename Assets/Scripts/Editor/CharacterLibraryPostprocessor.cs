// CharacterLibraryPostprocessor.cs
// -----------------------------------------------------------------------------
// Auto-registers every CharacterData SO in the project into the
// CharacterLibrary asset. Mirrors ConditionLibraryPostprocessor.
//
// When a designer creates a new CharacterData anywhere in the project (or
// imports one), Unity fires OnPostprocessAllAssets; this hook loads the
// library (if it exists) and adds the new entry.
//
// Deletions are handled lazily — the library's serialized list will contain
// null entries which CharacterLibrary filters out on read. For a hard-clean
// the designer can use DarkSpire → Characters → Refresh Library.
//
// If the library asset doesn't exist yet, this postprocessor silently skips —
// no-op until the library is created.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public class CharacterLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool dirty = false;
            CharacterLibrary library = null;

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (!path.EndsWith(".asset")) continue;
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
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

        private static CharacterLibrary LoadLibrary()
        {
            // Use AssetDatabase so we don't depend on Resources having loaded
            // the singleton already.
            return AssetDatabase.LoadAssetAtPath<CharacterLibrary>(CharacterLibrary.AssetPath);
        }
    }
}
