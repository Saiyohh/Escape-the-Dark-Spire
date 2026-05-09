// OutlineProfileEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for OutlineProfile. Renders each tag override row with a
// "Reset to Default" button that copies the profile's default style into
// that slot — so designers don't have to manually match every field.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(OutlineProfile))]
    public class OutlineProfileEditor : Editor
    {
        private SerializedProperty defaultColorProp;
        private SerializedProperty defaultWidthPixelsProp;
        private SerializedProperty defaultUnderlayDilateProp;
        private SerializedProperty tagOverridesProp;

        private void OnEnable()
        {
            defaultColorProp = serializedObject.FindProperty("defaultColor");
            defaultWidthPixelsProp = serializedObject.FindProperty("defaultWidthPixels");
            defaultUnderlayDilateProp = serializedObject.FindProperty("defaultUnderlayDilate");
            tagOverridesProp = serializedObject.FindProperty("tagOverrides");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultColorProp);
            EditorGUILayout.PropertyField(defaultWidthPixelsProp);
            EditorGUILayout.PropertyField(defaultUnderlayDilateProp);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Per-Tag Overrides", EditorStyles.boldLabel);

            int arraySize = tagOverridesProp.arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                DrawOverrideRow(i);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Tag Override", GUILayout.Height(22)))
            {
                tagOverridesProp.InsertArrayElementAtIndex(arraySize);
                var newEl = tagOverridesProp.GetArrayElementAtIndex(arraySize);
                CopyDefaultsInto(newEl);
            }
            if (arraySize > 0 && GUILayout.Button("Clear All", GUILayout.Height(22), GUILayout.Width(100)))
            {
                tagOverridesProp.ClearArray();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOverrideRow(int index)
        {
            var el = tagOverridesProp.GetArrayElementAtIndex(index);
            var tagProp = el.FindPropertyRelative("tag");
            var colorProp = el.FindPropertyRelative("color");
            var widthProp = el.FindPropertyRelative("widthPixels");
            var dilateProp = el.FindPropertyRelative("underlayDilate");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(tagProp, GUIContent.none, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset to Default", GUILayout.Width(120)))
                {
                    CopyDefaultsInto(el);
                }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    tagOverridesProp.DeleteArrayElementAtIndex(index);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(colorProp);
                EditorGUILayout.PropertyField(widthProp);
                EditorGUILayout.PropertyField(dilateProp);
            }
        }

        private void CopyDefaultsInto(SerializedProperty element)
        {
            var colorProp = element.FindPropertyRelative("color");
            var widthProp = element.FindPropertyRelative("widthPixels");
            var dilateProp = element.FindPropertyRelative("underlayDilate");

            colorProp.colorValue = defaultColorProp.colorValue;
            widthProp.floatValue = defaultWidthPixelsProp.floatValue;
            dilateProp.floatValue = defaultUnderlayDilateProp.floatValue;
        }
    }
}
