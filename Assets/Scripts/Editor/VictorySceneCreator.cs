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
    // One-shot menu item that scaffolds Assets/Scenes/Victory.unity:
    //   - Camera
    //   - EventSystem (new Input System)
    //   - Canvas + "VICTORY" title + 3 score labels + "Continue" button
    //   - VictoryController GameObject wired to the labels + button
    public static class VictorySceneCreator
    {
        private const string ScenePath = "Assets/Scenes/Victory.unity";

        [MenuItem("Tools/DarkSpire/Scenes/Create Victory Scene")]
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
            scene.name = "Victory";

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();

            var titleTmp = CreateLabel(canvas.transform, "Title", "VICTORY", new Vector2(0f, 280f),
                                       new Vector2(800f, 160f), 96f, new Color(0.6f, 0.95f, 0.55f));
            var goldTmp  = CreateLabel(canvas.transform, "GoldLabel",  "Gold 0",  new Vector2(0f, 120f),
                                       new Vector2(640f, 60f), 36f, Color.white);
            var timeTmp  = CreateLabel(canvas.transform, "TimeLabel",  "Time 00:00", new Vector2(0f, 60f),
                                       new Vector2(640f, 60f), 36f, Color.white);
            var fightsTmp = CreateLabel(canvas.transform, "FightsLabel", "Fights Won 0", new Vector2(0f, 0f),
                                        new Vector2(640f, 60f), 36f, Color.white);
            var continueBtn = CreateButton(canvas.transform, "ContinueButton", "CONTINUE", new Vector2(0f, -120f));

            var controllerGO = new GameObject("VictoryController");
            var controller = controllerGO.AddComponent<VictoryController>();

            var so = new SerializedObject(controller);
            so.FindProperty("titleLabel").objectReferenceValue  = titleTmp;
            so.FindProperty("goldLabel").objectReferenceValue   = goldTmp;
            so.FindProperty("timeLabel").objectReferenceValue   = timeTmp;
            so.FindProperty("fightsLabel").objectReferenceValue = fightsTmp;
            so.FindProperty("continueButton").objectReferenceValue = continueBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorUtility.DisplayDialog(
                "Victory scene created",
                $"Created {ScenePath}.\n\nAdd it to File > Build Settings.",
                "OK");
        }

        private static void CreateCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.04f);
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
            var canvasGO = new GameObject("VictoryCanvas");
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

        private static TMP_Text CreateLabel(Transform parent, string name, string text,
                                            Vector2 anchoredPos, Vector2 size,
                                            float fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
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
            img.color = new Color(0.18f, 0.22f, 0.18f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.85f, 1f, 0.85f, 1f);
            colors.pressedColor     = new Color(0.65f, 0.85f, 0.65f, 1f);
            colors.selectedColor    = new Color(0.85f, 1f, 0.85f, 1f);
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
            text.color = new Color(0.92f, 1f, 0.92f);

            return btn;
        }
    }
}
#endif
