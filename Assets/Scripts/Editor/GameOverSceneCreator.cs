#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DarkSpire
{
    // One-shot menu item that scaffolds Assets/Scenes/GameOver.unity:
    //   - Camera
    //   - EventSystem (new Input System)
    //   - Canvas + "GAME OVER" title + "Try Again" button
    //   - GameOverController GameObject wired to the button
    public static class GameOverSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/GameOver.unity";

        [MenuItem("Tools/DarkSpire/Scenes/Create GameOver Scene")]
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
            scene.name = "GameOver";

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();

            CreateTitle(canvas.transform, "GAME OVER", new Color(0.95f, 0.4f, 0.4f));
            var tryAgainBtn = CreateButton(canvas.transform, "TryAgainButton", "TRY AGAIN", new Vector2(0f, -120f));

            var controllerGO = new GameObject("GameOverController");
            var controller = controllerGO.AddComponent<GameOverController>();

            var so = new SerializedObject(controller);
            so.FindProperty("tryAgainButton").objectReferenceValue = tryAgainBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorUtility.DisplayDialog(
                "GameOver scene created",
                $"Created {ScenePath}.\n\nAdd it to File > Build Settings.",
                "OK");
        }

        private static void CreateCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.02f, 0.02f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            var canvasGO = new GameObject("GameOverCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateTitle(Transform parent, string text, Color color)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(800f, 160f);
            rt.anchoredPosition = new Vector2(0f, 200f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 96f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 64f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
            colors.pressedColor     = new Color(0.65f, 0.75f, 0.85f, 1f);
            colors.selectedColor    = new Color(0.85f, 0.95f, 1f, 1f);
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var text = labelGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 32f;
            text.color = new Color(0.92f, 0.95f, 1f);

            return btn;
        }
    }
}
#endif
