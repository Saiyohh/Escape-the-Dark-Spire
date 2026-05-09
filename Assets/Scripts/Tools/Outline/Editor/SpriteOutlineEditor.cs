// SpriteOutlineEditor.cs
// -----------------------------------------------------------------------------
// Inspector for SpriteOutline with a "Reset Override" button that clears any
// per-instance color/width tweaks and re-reads the profile tag style. Style
// is re-applied live on change so designers see the result without entering
// play mode.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(SpriteOutline))]
    [CanEditMultipleObjects]
    public class SpriteOutlineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Override", GUILayout.Height(22)))
                {
                    foreach (var obj in targets)
                    {
                        if (obj is SpriteOutline so)
                        {
                            Undo.RecordObject(so, "Reset Sprite Outline Override");
                            so.ResetOverride();
                            EditorUtility.SetDirty(so);
                        }
                    }
                }

                if (GUILayout.Button("Apply Style Now", GUILayout.Height(22)))
                {
                    foreach (var obj in targets)
                        if (obj is SpriteOutline so) so.ApplyStyle();
                }
            }
        }
    }
}
