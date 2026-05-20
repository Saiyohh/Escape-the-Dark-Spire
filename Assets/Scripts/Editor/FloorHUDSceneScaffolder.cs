#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    // One-shot tool that scaffolds an authored FloorHUD into the currently-open
    // scene (intended for DungeonFloor.unity). Use this once; then tweak the
    // resulting hierarchy freely in the Inspector.
    //
    // FloorHUD.cs has NO runtime fallback — the HUD must exist in the scene
    // with its serialized refs wired or it logs a warning and renders nothing.
    // Re-running the full Scaffold prompts before overwriting. The "Add Legend
    // Button" companion adds just the ? button to an existing HUD without
    // touching the rest of the hierarchy — use it when you've customized the
    // HUD and don't want to start over.
    public static class FloorHUDSceneScaffolder
    {
        [MenuItem("Tools/DarkSpire/Scenes/Scaffold FloorHUD into open scene")]
        public static void Scaffold()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("No scene open",
                    "Open the scene you want the HUD scaffolded into (typically " +
                    "DungeonFloor.unity) and try again.",
                    "OK");
                return;
            }

            var existing = Object.FindAnyObjectByType<FloorHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite existing FloorHUD?",
                    $"There is already a FloorHUD in scene '{scene.name}' ({existing.gameObject.name}). " +
                    "Replace it with a fresh scaffold? Your previous tweaks will be lost.",
                    "Replace", "Cancel"))
                {
                    return;
                }
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = BuildScene();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog("FloorHUD scaffolded",
                $"Created FloorHUD in '{scene.name}'.\n\n" +
                "Tweak in the Inspector:\n" +
                " • TopBar > the 4 TMP labels (Floor / Keys / Gold / Timer)\n" +
                " • LegendButton (?) at the right edge of the top bar\n" +
                " • TopBar Image — background color/alpha\n" +
                " • HPBars container — spacing, padding, position\n" +
                " • Each PartyHpBar — portrait, bar colors, name label\n" +
                " • Canvas sortingOrder if it overlaps another UI layer\n\n" +
                "Save the scene to lock changes in. Don't remove the FloorHUD " +
                "component from the root — DungeonBootstrap finds it by type.",
                "OK");
        }

        [MenuItem("Tools/DarkSpire/Scenes/Add Legend Button to FloorHUD")]
        public static void AddLegendButtonToExisting()
        {
            var hud = Object.FindAnyObjectByType<FloorHUD>(FindObjectsInactive.Include);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("No FloorHUD found",
                    "Open the scene with your FloorHUD (typically DungeonFloor.unity) " +
                    "and run this menu again. If you haven't scaffolded one yet, " +
                    "use 'Scaffold FloorHUD into open scene' instead.",
                    "OK");
                return;
            }

            var so = new SerializedObject(hud);
            var legendProp = so.FindProperty("legendButton");
            if (legendProp == null)
            {
                EditorUtility.DisplayDialog("Field missing",
                    "FloorHUD has no 'legendButton' field. Pull the latest changes " +
                    "and recompile.",
                    "OK");
                return;
            }

            if (legendProp.objectReferenceValue != null)
            {
                if (!EditorUtility.DisplayDialog("Legend button already wired",
                    $"FloorHUD already references a legend button ('{legendProp.objectReferenceValue.name}'). " +
                    "Add another one anyway? The existing reference will be replaced.",
                    "Replace", "Cancel"))
                {
                    return;
                }
            }

            // Find the top bar so the button slots into the existing horizontal
            // layout. Falls back to the HUD root if no TopBar child is found
            // — the button still functions, just positioned arbitrarily.
            Transform topBar = hud.transform.Find("TopBar");
            if (topBar == null) topBar = hud.transform;

            var btn = BuildLegendButton(topBar);
            legendProp.objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = btn.gameObject;

            EditorUtility.DisplayDialog("Legend button added",
                $"Wired a ? button into FloorHUD.legendButton ('{btn.gameObject.name}').\n\n" +
                "Tweak position, color, label in the Inspector. Saving the scene " +
                "persists the change.",
                "OK");
        }

        // ─── Build helpers ──────────────────────────────────────────────────

        private static GameObject BuildScene()
        {
            // Root with Canvas + HUD component.
            var root = new GameObject("FloorHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // Top bar.
            var bar = new GameObject("TopBar");
            bar.transform.SetParent(root.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot     = new Vector2(0.5f, 1f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 80f);

            var bbg = bar.AddComponent<Image>();
            bbg.color = new Color(0f, 0f, 0f, 0.55f);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 24, 12, 12);
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var floorLabel = BuildBarLabel(bar.transform, "FloorLabel", "Floor 1");
            var keysLabel  = BuildBarLabel(bar.transform, "KeysLabel",  "Keys 0/0");
            var goldLabel  = BuildBarLabel(bar.transform, "GoldLabel",  "Gold 0");

            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bar.transform, false);
            spacer.AddComponent<RectTransform>();
            var sle = spacer.AddComponent<LayoutElement>();
            sle.flexibleWidth = 999f;

            var timerLabel = BuildBarLabel(bar.transform, "TimerLabel", "00:00");

            // ? button at the right edge of the top bar — last child so the
            // HorizontalLayoutGroup positions it after the timer.
            var legendBtn = BuildLegendButton(bar.transform);

            // HP row.
            var hpRow = new GameObject("HPBars");
            hpRow.transform.SetParent(root.transform, false);
            var hprt = hpRow.AddComponent<RectTransform>();
            hprt.anchorMin = new Vector2(0f, 1f);
            hprt.anchorMax = new Vector2(1f, 1f);
            hprt.pivot     = new Vector2(0.5f, 1f);
            hprt.anchoredPosition = new Vector2(0f, -80f);
            hprt.sizeDelta = new Vector2(0f, 60f);

            var hpHlg = hpRow.AddComponent<HorizontalLayoutGroup>();
            hpHlg.padding = new RectOffset(24, 24, 4, 4);
            hpHlg.spacing = 12f;
            hpHlg.childAlignment = TextAnchor.MiddleLeft;
            hpHlg.childControlWidth = false;
            hpHlg.childControlHeight = true;
            hpHlg.childForceExpandWidth = false;
            hpHlg.childForceExpandHeight = true;

            var bars = new PartyHpBar[4];
            for (int i = 0; i < 4; i++)
                bars[i] = PartyHpBar.BuildRuntime(hpRow.transform);

            // Component + serialized references.
            var hud = root.AddComponent<FloorHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("floorLabel").objectReferenceValue = floorLabel;
            so.FindProperty("keysLabel").objectReferenceValue  = keysLabel;
            so.FindProperty("goldLabel").objectReferenceValue  = goldLabel;
            so.FindProperty("timerLabel").objectReferenceValue = timerLabel;
            so.FindProperty("hpBarContainer").objectReferenceValue = hprt;
            so.FindProperty("legendButton").objectReferenceValue  = legendBtn;

            var hpBarsProp = so.FindProperty("hpBars");
            hpBarsProp.arraySize = bars.Length;
            for (int i = 0; i < bars.Length; i++)
                hpBarsProp.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static TMP_Text BuildBarLabel(Transform parent, string name, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(tmp);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 130f;
            le.preferredWidth = 180f;
            return tmp;
        }

        /// <summary>
        /// Build the ? icon button and return it. Used by both the full
        /// scaffolder and the "Add Legend Button to FloorHUD" entry point.
        /// </summary>
        private static Button BuildLegendButton(Transform parent)
        {
            var go = new GameObject("LegendButton");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(48f, 48f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.10f, 0.12f, 0.85f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;
            var colors = btn.colors;
            colors.normalColor      = new Color(0.10f, 0.10f, 0.12f, 0.85f);
            colors.highlightedColor = new Color(0.22f, 0.22f, 0.26f, 0.95f);
            colors.pressedColor     = new Color(0.06f, 0.06f, 0.08f, 1f);
            colors.selectedColor    = new Color(0.22f, 0.22f, 0.26f, 0.95f);
            colors.disabledColor    = new Color(0.10f, 0.10f, 0.12f, 0.6f);
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 48f;
            le.preferredHeight = 48f;
            le.flexibleWidth = 0f;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "?";
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.92f, 0.45f);
            tmp.raycastTarget = false;
            UIFonts.Apply(tmp);

            return btn;
        }
    }
}
#endif
