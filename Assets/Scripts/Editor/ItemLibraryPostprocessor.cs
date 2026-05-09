// ItemLibraryPostprocessor.cs
// -----------------------------------------------------------------------------
// Auto-registers every ItemData SO in the project into the ItemLibrary asset.
// When the designer creates a new ItemData anywhere in the project (or imports
// one), Unity fires OnPostprocessAllAssets; this hook loads the library (if
// it exists) and adds the new entry.
//
// Mirrors ConditionLibraryPostprocessor — see that file for the rationale on
// lazy library loading and silent skip when the library asset doesn't yet
// exist.
// -----------------------------------------------------------------------------
using UnityEditor;

namespace DarkSpire.EditorTools
{
    public class ItemLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool dirty = false;
            ItemLibrary library = null;

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (!path.EndsWith(".asset")) continue;
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null) continue;

                if (library == null) library = LoadLibrary();
                if (library == null) return;

                library.Register(item);
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssetIfDirty(library);
            }
        }

        private static ItemLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibrary.AssetPath);
        }
    }
}
