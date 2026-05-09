// ColorLibraryEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for ColorLibrary. Two-tier authoring:
//
//   [+ New Category] at the top
//   ▼ Category Name                                              [✕]
//      ┌─ Color Name | swatch ✕
//      ┌─ Color Name | swatch ✕
//      [+ Add Color]
//
// Foldout state persists across selection changes and domain reloads via
// SessionState. Edits go directly to target fields wrapped in
// Undo.RecordObject (instead of through SerializedProperty / ColorField,
// which had a race condition that bound the picker to the wrong row).
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(ColorLibrary))]
    public class ColorLibraryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var lib = (ColorLibrary)target;

            EditorStyleKit.DrawColoredSectionHeader(
                "Color Library",
                new Color(0.55f, 0.55f, 0.85f));

            EditorGUILayout.LabelField(
                "Every color belongs to a Category. Runtime lookup uses " +
                "ColorLibrary.Get(\"Category\", \"Name\").",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);

            // ── Top-level "Add Category" button ─────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ New Category", GUILayout.Height(22)))
                {
                    Undo.RecordObject(lib, "Add Category");
                    var cat = lib.AddCategory("New Category");
                    SetFold(lib, cat.name, true);
                }
                if (GUILayout.Button("Open Asset Path", GUILayout.Width(140), GUILayout.Height(22)))
                    EditorUtility.RevealInFinder(ColorLibrary.AssetPath);
            }

            EditorGUILayout.Space(4);

            // ── Iterate categories ──────────────────────────────────────────
            int categoryToDelete = -1;
            for (int ci = 0; ci < lib.Categories.Count; ci++)
            {
                if (DrawCategory(lib, ci))
                    categoryToDelete = ci;
                EditorGUILayout.Space(2);
            }

            // Defer deletion until after the loop so we don't mutate while iterating.
            if (categoryToDelete >= 0)
            {
                Undo.RecordObject(lib, "Remove Category");
                lib.RemoveCategory(categoryToDelete);
                Repaint();
            }
        }

        // ─── Category panel ────────────────────────────────────────────────

        /// <summary>Returns true if the category should be deleted.</summary>
        private bool DrawCategory(ColorLibrary lib, int categoryIndex)
        {
            var cat = lib.Categories[categoryIndex];
            if (cat == null) return false;

            bool wantsDelete = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header: foldout + rename + delete category
            EditorGUILayout.BeginHorizontal();

            bool open = GetFold(lib, cat.name, defaultValue: true);
            // Stable foldout key — invalidate when the name changes by re-keying below.
            bool newOpen = EditorGUILayout.Foldout(open,
                $"   {cat.colors.Count}", toggleOnLabelClick: true);
            if (newOpen != open) SetFold(lib, cat.name, newOpen);
            open = newOpen;

            // Inline rename. We do this AFTER the foldout so its width is what's left.
            string newName = EditorGUILayout.DelayedTextField(cat.name);
            if (newName != cat.name && !string.IsNullOrWhiteSpace(newName))
            {
                Undo.RecordObject(lib, "Rename Category");
                // Move the saved foldout state to the new key so it doesn't reset.
                bool wasOpen = GetFold(lib, cat.name, true);
                ClearFold(lib, cat.name);
                cat.name = newName;
                SetFold(lib, cat.name, wasOpen);
                lib.NotifyMutated();
            }

            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                wantsDelete = true;

            EditorGUILayout.EndHorizontal();

            // Body — colors list + add row
            if (open)
            {
                EditorGUI.indentLevel++;

                int colorToDelete = -1;
                for (int ki = 0; ki < cat.colors.Count; ki++)
                {
                    if (DrawColorRow(lib, cat, ki))
                        colorToDelete = ki;
                }
                if (colorToDelete >= 0)
                {
                    Undo.RecordObject(lib, "Remove Color");
                    lib.RemoveColor(categoryIndex, colorToDelete);
                }

                EditorGUILayout.Space(2);

                if (GUILayout.Button("+ Add Color", GUILayout.Height(20)))
                {
                    Undo.RecordObject(lib, "Add Color");
                    lib.AddColor(categoryIndex, "New Color", Color.white);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            return wantsDelete;
        }

        // ─── Color row ──────────────────────────────────────────────────────

        /// <summary>Returns true if this row should be deleted.</summary>
        private bool DrawColorRow(ColorLibrary lib, ColorLibrary.Category cat, int colorIndex)
        {
            var nc = cat.colors[colorIndex];
            if (nc == null) return false;

            bool wantsDelete = false;

            EditorGUILayout.BeginHorizontal();

            // Name field — DelayedTextField so we don't dirty on every keystroke.
            string newName = EditorGUILayout.DelayedTextField(nc.name);
            if (newName != nc.name)
            {
                Undo.RecordObject(lib, "Rename Color");
                nc.name = string.IsNullOrWhiteSpace(newName) ? "Unnamed" : newName;
                lib.NotifyMutated();
            }

            // Color picker — wrap in change-check so we only commit one undo
            // step per drag. ColorField returns the new color directly.
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(GUIContent.none, nc.color,
                showEyedropper: true, showAlpha: true, hdr: false,
                GUILayout.Width(60));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(lib, "Edit Color");
                nc.color = newColor;
                lib.NotifyMutated();
            }

            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                wantsDelete = true;

            EditorGUILayout.EndHorizontal();

            return wantsDelete;
        }

        // ─── Foldout state (persisted via SessionState) ────────────────────

        private static string FoldKey(ColorLibrary lib, string categoryName) =>
            $"DarkSpire.ColorLibrary.{lib.GetEntityId()}.{categoryName}";

        private static bool GetFold(ColorLibrary lib, string categoryName, bool defaultValue)
            => SessionState.GetBool(FoldKey(lib, categoryName), defaultValue);

        private static void SetFold(ColorLibrary lib, string categoryName, bool value)
            => SessionState.SetBool(FoldKey(lib, categoryName), value);

        private static void ClearFold(ColorLibrary lib, string categoryName)
            => SessionState.EraseBool(FoldKey(lib, categoryName));
    }
}
