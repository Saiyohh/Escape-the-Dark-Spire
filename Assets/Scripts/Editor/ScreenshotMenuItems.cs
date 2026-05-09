// ScreenshotMenuItems.cs
// -----------------------------------------------------------------------------
// Top-menu helpers for the screenshot tool. Lets designers open the
// screenshot folder in Explorer or wipe its contents without hunting for the
// path.
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace DarkSpire.EditorTools
{
    public static class ScreenshotMenuItems
    {
        private const string FolderName = "Screenshots";

        [MenuItem("DarkSpire/Screenshots/Open Folder")]
        public static void OpenFolder()
        {
            string folder = GetFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem("DarkSpire/Screenshots/Clear Folder")]
        public static void ClearFolder()
        {
            string folder = GetFolder();
            if (!Directory.Exists(folder))
            {
                UnityEngine.Debug.Log($"[Screenshots] Folder doesn't exist yet: {folder}");
                return;
            }

            int count = 0;
            foreach (var file in Directory.GetFiles(folder, "*.png"))
            {
                File.Delete(file);
                count++;
            }
            UnityEngine.Debug.Log($"[Screenshots] Deleted {count} PNG(s) from {folder}");
        }

        private static string GetFolder()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.Combine(projectRoot, FolderName);
        }
    }
}
