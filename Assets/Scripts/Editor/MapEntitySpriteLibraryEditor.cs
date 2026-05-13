// MapEntitySpriteLibraryEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for MapEntitySpriteLibrary. Groups the (long) flat list of
// Sprite slots into collapsible categories and shows a thumbnail next to each
// field so designers can verify assignments at a glance.
//
// Foldout state persists across selection and domain reloads via SessionState.
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(MapEntitySpriteLibrary))]
    public class MapEntitySpriteLibraryEditor : Editor
    {
        private const float PreviewSize = 32f;
        private const float RowSpacing = 2f;
        private const string FoldoutKeyPrefix = "DarkSpire.MapEntitySpriteLibrary.Foldout.";

        private static readonly Color HeaderStrip = new(0.35f, 0.72f, 0.88f);

        // (Foldout title, [(Field name, Display label), ...])
        private static readonly (string Title, (string Field, string Label)[] Fields)[] Categories =
        {
            ("Monster tiers", new[]
            {
                ("standardMonster", "Standard Monster"),
                ("eliteMonster",    "Elite Monster"),
                ("boss",            "Boss"),
            }),
            ("Encounter tiles", new[]
            {
                ("campsite",   "Campsite"),
                ("eventTile",  "Event Tile"),
                ("shrine",     "Shrine"),
            }),
            ("Collectibles & interactables", new[]
            {
                ("key",            "Key"),
                ("chest",          "Chest"),
                ("chestOpen",      "Chest Open"),
                ("goldPile",       "Gold Pile"),
                ("stairway",       "Stairway"),
                ("stairwayLocked", "Stairway Locked"),
            }),
            ("Boss Gate (directional)", new[]
            {
                ("bossGateNorth",     "Locked — North"),
                ("bossGateSouth",     "Locked — South"),
                ("bossGateEast",      "Locked — East"),
                ("bossGateWest",      "Locked — West"),
                ("bossGateOpenNorth", "Open — North"),
                ("bossGateOpenSouth", "Open — South"),
                ("bossGateOpenEast",  "Open — East"),
                ("bossGateOpenWest",  "Open — West"),
            }),
            ("Tile types", new[]
            {
                ("floorTile", "Floor Tile"),
                ("wallTile",  "Wall Tile"),
                ("emptyTile", "Empty Tile"),
                ("startTile", "Start Tile"),
            }),
            ("Indicators", new[]
            {
                ("alertIndicator", "Alert Indicator"),
                ("chaseIndicator", "Chase Indicator"),
            }),
            ("Party", new[]
            {
                ("partyToken", "Party Token"),
            }),
        };

        public override void OnInspectorGUI()
        {
            EditorStyleKit.DrawColoredSectionHeader("Map Entity Sprite Library", HeaderStrip);
            EditorGUILayout.LabelField(
                "Sprites used by FloorRenderer, EntitySpawner, and map overlays. " +
                "Boss Gate uses direction-facing variants picked by PlaceBossGate.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            serializedObject.Update();

            foreach (var (title, fields) in Categories)
            {
                DrawCategory(title, fields);
                EditorGUILayout.Space(2);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCategory(string title, (string Field, string Label)[] fields)
        {
            string key = FoldoutKeyPrefix + title;
            bool expanded = SessionState.GetBool(key, true);

            bool next = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (next != expanded)
            {
                expanded = next;
                SessionState.SetBool(key, expanded);
            }

            if (expanded)
            {
                EditorGUI.indentLevel++;
                foreach (var (field, label) in fields)
                {
                    var prop = serializedObject.FindProperty(field);
                    if (prop == null)
                    {
                        EditorGUILayout.HelpBox($"Missing field: {field}", MessageType.Warning);
                        continue;
                    }
                    DrawSpriteRow(prop, label);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawSpriteRow(SerializedProperty prop, string label)
        {
            // Reserve a row tall enough for the preview, then split it into:
            //   [preview thumbnail] [object field aligned to the same baseline]
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float rowHeight = Mathf.Max(PreviewSize, lineHeight);
            var row = EditorGUILayout.GetControlRect(false, rowHeight + RowSpacing);
            row.height = rowHeight;

            // Honour indent so foldout nesting doesn't push our manual layout
            // past the inspector's text label column.
            row = EditorGUI.IndentedRect(row);
            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var previewRect = new Rect(row.x, row.y, PreviewSize, PreviewSize);
            var fieldRect = new Rect(
                row.x + PreviewSize + 6f,
                row.y + (rowHeight - lineHeight) * 0.5f,
                row.width - PreviewSize - 6f,
                lineHeight);

            DrawSpritePreview(previewRect, prop.objectReferenceValue as Sprite);
            EditorGUI.ObjectField(fieldRect, prop, typeof(Sprite), new GUIContent(label));

            EditorGUI.indentLevel = oldIndent;
        }

        private static void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            if (sprite == null || sprite.texture == null) return;

            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(
                tr.x / tex.width,
                tr.y / tex.height,
                tr.width / tex.width,
                tr.height / tex.height);

            // Letterbox inside the preview square so non-square sprites don't
            // stretch — matches the sprite's authored aspect ratio.
            float aspect = tr.width / Mathf.Max(1f, tr.height);
            Rect drawRect = rect;
            if (aspect >= 1f)
            {
                float h = rect.width / aspect;
                drawRect = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
            }
            else
            {
                float w = rect.height * aspect;
                drawRect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
            }

            GUI.DrawTextureWithTexCoords(drawRect, tex, uv);
        }
    }
}
