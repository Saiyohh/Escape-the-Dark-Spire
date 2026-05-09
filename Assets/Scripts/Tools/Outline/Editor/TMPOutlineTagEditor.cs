// TMPOutlineTagEditor.cs
// -----------------------------------------------------------------------------
// Inspector for TMPOutlineTag with Reset Override + Apply Style Now actions.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(TMPOutlineTag))]
    [CanEditMultipleObjects]
    public class TMPOutlineTagEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Outline is rendered OUTSIDE the glyph via TMP's underlay layer. " +
                "The shared font asset is not modified — per-instance fontMaterial " +
                "receives the color/dilate. Different TMP objects can run different " +
                "colors without duplicating the SDF font.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Override", GUILayout.Height(22)))
                {
                    foreach (var obj in targets)
                    {
                        if (obj is TMPOutlineTag t)
                        {
                            Undo.RecordObject(t, "Reset TMP Outline Override");
                            t.ResetOverride();
                            EditorUtility.SetDirty(t);
                        }
                    }
                }

                if (GUILayout.Button("Apply Style Now", GUILayout.Height(22)))
                {
                    foreach (var obj in targets)
                        if (obj is TMPOutlineTag t) t.ApplyStyle();
                }
            }
        }
    }
}
