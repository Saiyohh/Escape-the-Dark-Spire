#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkSpire
{
    // One-shot menu item that scaffolds Assets/Scenes/DungeonFloor.unity with
    // a camera + DungeonBootstrap GameObject so designers don't have to
    // wire the scene by hand. Safe to re-run: prompts before overwriting.
    public static class DungeonSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/DungeonFloor.unity";

        [MenuItem("Tools/DarkSpire/Dungeon/Create DungeonFloor Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog(
                    "Overwrite scene?",
                    $"{ScenePath} already exists. Overwrite it?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            var dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DungeonFloor";

            // Camera
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.transform.position = new Vector3(12.5f, 12.5f, -10f);
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.AddComponent<AudioListener>();

            // Bootstrap
            var bootGO = new GameObject("DungeonBootstrap");
            bootGO.AddComponent<DungeonBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorUtility.DisplayDialog(
                "DungeonFloor scene created",
                $"Created {ScenePath}.\n\n" +
                "Next:\n" +
                "1. Select DungeonBootstrap in the Hierarchy\n" +
                "2. Drag your FLOOR_1 config into Fallback Config\n" +
                "3. Sprite Library auto-resolves to the singleton at " +
                  "Assets/ScriptableObjects/MapEntitySpriteLibrary.asset.\n" +
                "   If it doesn't exist yet, run DarkSpire → Map Sprites → Create Library.\n" +
                "   (Optional) drag a custom override into Sprite Library for debug scenes.\n" +
                "4. Press Play",
                "OK");
        }
    }
}
#endif
