// UIOutlineEditor.cs
// -----------------------------------------------------------------------------
// Inspector for UIOutline — same reset-override / apply-now affordances as
// SpriteOutlineEditor.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(UIOutline))]
    [CanEditMultipleObjects]
    public class UIOutlineEditor : Editor
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
                        if (obj is UIOutline uo)
                        {
                            Undo.RecordObject(uo, "Reset UI Outline Override");
                            uo.ResetOverride();
                            EditorUtility.SetDirty(uo);
                        }
                    }
                }

                if (GUILayout.Button("Apply Style Now", GUILayout.Height(22)))
                {
                    foreach (var obj in targets)
                        if (obj is UIOutline uo) uo.ApplyStyle();
                }
            }
        }
    }
}
