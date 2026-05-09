// ScreenshotCapture.cs
// -----------------------------------------------------------------------------
// Hotkey-driven screenshot tool. Add this component anywhere in the scene and
// press F12 (configurable) to save a timestamped PNG. Works in both edit mode
// and play mode.
//
// Screenshots go to `<ProjectRoot>/Screenshots/` (outside Assets, so Unity
// doesn't import them). The companion ScreenshotMenuItems editor script adds
// a top-menu command to open the folder and another to clear it.
//
// Super-resolution: set `superSize > 1` to render at N× the current screen
// resolution. Useful for captures you want to crop or upscale for docs.
// -----------------------------------------------------------------------------
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tools/Screenshot Capture")]
    public class ScreenshotCapture : MonoBehaviour
    {
        [Header("Hotkey (New Input System)")]
        [Tooltip("Key that triggers a capture. Default F12.")]
        public Key hotkey = Key.F12;

        [Tooltip("Require Ctrl to be held while pressing the hotkey.")]
        public bool requireCtrl = false;

        [Header("Output")]
        [Tooltip("Subfolder under the project root (where Assets/ lives). " +
                 "Created on first capture.")]
        public string subfolder = "Screenshots";

        [Tooltip("Filename prefix. The timestamp is appended.")]
        public string prefix = "DarkSpire";

        [Tooltip("Super-resolution multiplier. 1 = screen size, 2 = 2× height+width.")]
        [Range(1, 8)] public int superSize = 1;

        [Tooltip("Print the saved path to the console.")]
        public bool logSavedPath = true;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb[hotkey].wasPressedThisFrame) return;
            if (requireCtrl && !(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) return;
            Capture();
        }

        [ContextMenu("Capture Screenshot Now")]
        public void Capture()
        {
            string folder = ResolveFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string filename = $"{prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
            string fullPath = Path.Combine(folder, filename);

            ScreenCapture.CaptureScreenshot(fullPath, superSize);
            if (logSavedPath)
                Debug.Log($"[Screenshot] Queued → {fullPath} (super={superSize})");
        }

        /// <summary>Returns the absolute path to the screenshot folder.</summary>
        public string ResolveFolder()
        {
            // Application.dataPath ends in /Assets at runtime; go up one level.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, subfolder);
        }
    }
}
