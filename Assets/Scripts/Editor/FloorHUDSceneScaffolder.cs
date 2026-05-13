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
    // Re-running this menu item prompts before overwriting.
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
                " • TopBar Image — background color/alpha\n" +
                " • HPBars container — spacing, padding, position\n" +
                " • Each PartyHpBar — portrait, bar colors, name label\n" +
                " • Canvas sortingOrder if it overlaps another UI layer\n\n" +
                "Save the scene to lock changes in. Don't remove the FloorHUD " +
                "component from the root — DungeonBootstrap finds it by type.",
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
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 130f;
            le.preferredWidth = 180f;
            return tmp;
        }
    }
}
#endif
