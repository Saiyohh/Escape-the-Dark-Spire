// NewConditionIDWindow.cs
// -----------------------------------------------------------------------------
// Small modal EditorWindow that prompts for a new ConditionID name and hands
// it to ConditionIDGenerator. Opened from:
//
//   • The "+ New..." button next to the Condition ID field in
//     ConditionDataEditor.
//   • The menu shortcut DarkSpire → Conditions → Add New ID...
//
// On success the window closes and Unity starts recompiling. The caller's
// inspector can't immediately switch its serialized enumValueIndex to the new
// value because the enum doesn't know about it until after domain reload —
// the user picks it from the now-updated dropdown.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public class NewConditionIDWindow : EditorWindow
    {
        private string nameInput = "";
        private string lastError = "";
        private bool focusedInput = false;

        [MenuItem("DarkSpire/Conditions/Add New ID...")]
        public static void Open()
        {
            var window = GetWindow<NewConditionIDWindow>(true, "Add New Condition ID", true);
            window.minSize = new Vector2(360, 150);
            window.maxSize = new Vector2(520, 220);
            window.nameInput = "";
            window.lastError = "";
            window.focusedInput = false;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Adds a new value to the ConditionID enum in CombatEnums.cs. " +
                "Use PascalCase — letters and digits only. The new value is " +
                "appended to the end of the enum so existing serialized data " +
                "is preserved.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(6);

            GUI.SetNextControlName("CondIDInputField");
            nameInput = EditorGUILayout.TextField("Name", nameInput);

            if (!focusedInput)
            {
                EditorGUI.FocusTextInControl("CondIDInputField");
                focusedInput = true;
            }

            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(lastError, MessageType.Warning);
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(24)))
            {
                Close();
                return;
            }

            bool canAdd = !string.IsNullOrWhiteSpace(nameInput);

            // Enter key in the text field = click Add.
            bool enterPressed = Event.current.type == EventType.KeyDown
                             && (Event.current.keyCode == KeyCode.Return
                              || Event.current.keyCode == KeyCode.KeypadEnter);

            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add", GUILayout.Height(24)) || (canAdd && enterPressed))
                {
                    if (ConditionIDGenerator.AddID(nameInput.Trim(), out var error))
                    {
                        Debug.Log($"[ConditionID] Added '{nameInput.Trim()}'. " +
                                  "Unity is recompiling — the new value appears in " +
                                  "condition dropdowns shortly.");
                        Close();
                    }
                    else
                    {
                        lastError = error;
                        Repaint();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
