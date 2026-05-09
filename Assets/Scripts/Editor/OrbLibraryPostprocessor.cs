// OrbLibraryPostprocessor.cs
// -----------------------------------------------------------------------------
// Auto-registers every OrbDataSO in the project into the OrbLibrary asset.
// Mirrors ConditionLibraryPostprocessor's pattern. When the designer creates
// or imports an OrbDataSO, this hook adds it to the library on the next asset
// import sweep.
//
// Deletions are handled lazily — the library's serialized list filters null
// entries on read. Use DarkSpire → Orbs → Refresh Library for a hard-clean.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public class OrbLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool dirty = false;
            OrbLibrary library = null;

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (!path.EndsWith(".asset")) continue;
                var o = AssetDatabase.LoadAssetAtPath<OrbDataSO>(path);
                if (o == null) continue;

                if (library == null) library = LoadLibrary();
                if (library == null) return;

                library.Register(o);
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssetIfDirty(library);
            }
        }

        private static OrbLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<OrbLibrary>(OrbLibrary.AssetPath);
        }
    }
}
