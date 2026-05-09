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
    // One-shot menu item that scaffolds Assets/Scenes/MainMenu.unity:
    //   - Camera (solid-colour clear)
    //   - EventSystem with InputSystemUIInputModule (project uses new Input System)
    //   - Canvas (Screen Space - Overlay) with CanvasScaler @ 1920x1080
    //   - Logo placeholder rect (white square; user replaces with art)
    //   - Play / Quit TMP buttons
    //   - MainMenuController GameObject wired to the buttons
    //
    // Safe to re-run: prompts before overwriting.
    public static class MainMenuSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/DarkSpire/Scenes/Create MainMenu Scene")]
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
            scene.name = "MainMenu";

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();

            var logo       = CreateLogoPlaceholder(canvas.transform);
            var playButton = CreateButton(canvas.transform, "PlayButton",  "PLAY",  new Vector2(0f, -120f));
            var quitButton = CreateButton(canvas.transform, "QuitButton",  "QUIT",  new Vector2(0f, -200f));

            var controllerGO = new GameObject("MainMenuController");
            var controller = controllerGO.AddComponent<MainMenuController>();

            // Wire button refs via SerializedObject so the inspector reflects them.
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorUtility.DisplayDialog(
                "MainMenu scene created",
                $"Created {ScenePath}.\n\n" +
                "Next:\n" +
                "1. File > Build Settings — drag MainMenu.unity in (slot 0 so it boots first), " +
                "DungeonFloor.unity, and CombatTest.unity.\n" +
                "2. Replace the white Logo placeholder with your art when ready.\n" +
                "3. Press Play in MainMenu — buttons should slide the overlay and load Dungeon.",
                "OK");
        }

        // ─── Scaffolding helpers ────────────────────────────────────────

        private static void CreateCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Project uses Unity's new Input System; legacy StandaloneInputModule
            // would log "Input System has UI Toolkit input mode" warnings.
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            var canvasGO = new GameObject("MainMenuCanvas");
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

        private static GameObject CreateLogoPlaceholder(Transform parent)
        {
            var go = new GameObject("LogoPlaceholder");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 200f);
            rt.anchoredPosition = new Vector2(0f, 220f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.85f, 0.9f, 0.4f);   // soft placeholder fill

            // Caption inside the placeholder so designers know to swap it out.
            var labelGO = new GameObject("PlaceholderLabel");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "[ LOGO ]";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 64f;
            label.color = new Color(0.15f, 0.15f, 0.2f);

            return go;
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
            // Subtle hover/pressed tints so the button reads as interactive even pre-art.
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
            colors.pressedColor     = new Color(0.65f, 0.75f, 0.85f, 1f);
            colors.selectedColor    = new Color(0.85f, 0.95f, 1f, 1f);
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            // Label
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
