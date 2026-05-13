#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    // One-shot tool that scaffolds an authored Minimap into the currently-open
    // scene (intended for DungeonFloor.unity). Use this once; then tweak the
    // resulting hierarchy freely in the Inspector.
    //
    // The scaffolder authors:
    //   Minimap (Canvas root + Minimap component)
    //   └── Frame (RectTransform + Image — visible border)
    //       └── Display (RectTransform + RawImage — runtime texture sits here)
    //
    // Frame.RectTransform is what you resize/reposition to control the
    // on-screen size of the minimap. The Image on Frame gives you a visible
    // rectangle in the Editor (and at runtime, until the texture paints over
    // its inset Display child) so you can see exactly where it sits.
    //
    // Minimap.cs has NO runtime fallback — the hierarchy MUST exist in the
    // scene with `display` wired. Re-running prompts before overwriting.
    public static class MinimapSceneScaffolder
    {
        [MenuItem("Tools/DarkSpire/Scenes/Scaffold Minimap into open scene")]
        public static void Scaffold()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("No scene open",
                    "Open the scene you want the Minimap scaffolded into " +
                    "(typically DungeonFloor.unity) and try again.",
                    "OK");
                return;
            }

            var existing = Object.FindAnyObjectByType<Minimap>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite existing Minimap?",
                    $"There is already a Minimap in scene '{scene.name}' ({existing.gameObject.name}). " +
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

            EditorUtility.DisplayDialog("Minimap scaffolded",
                $"Created Minimap in '{scene.name}'.\n\n" +
                "Tweak in the Inspector:\n" +
                " • Frame > RectTransform — anchor, position, and SIZE.\n" +
                "   This is what controls the on-screen minimap size.\n" +
                " • Frame > Image — border color/alpha.\n" +
                " • Display > RectTransform offsetMin/Max — border thickness.\n" +
                " • Minimap (root) > Tile Size — internal pixel-art resolution.\n" +
                " • Canvas Sorting Order if it clashes with the HUD.\n\n" +
                "The Frame's Image is visible in-Editor (and at runtime in the " +
                "border area) so you always have a rectangle to align to.",
                "OK");
        }

        // ─── Build helpers ──────────────────────────────────────────────────

        private static GameObject BuildScene()
        {
            // Root with Canvas + Minimap component.
            var root = new GameObject("Minimap");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 610; // just above FloorHUD (600)

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // Frame — top-right corner by default. THIS is the rectangle you
            // resize to control on-screen size. Image gives it a visible body
            // both in-Editor and at runtime (the Display child insets to leave
            // the Image visible as a border).
            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(1f, 1f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot     = new Vector2(1f, 1f);
            frt.anchoredPosition = new Vector2(-24f, -180f);
            frt.sizeDelta = new Vector2(220f, 220f);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Display — inset child holding the runtime RawImage. Offsets
            // form the visible border.
            var displayGO = new GameObject("Display");
            displayGO.transform.SetParent(frame.transform, false);
            var drt = displayGO.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(4f, 4f);
            drt.offsetMax = new Vector2(-4f, -4f);
            var raw = displayGO.AddComponent<RawImage>();
            raw.color = Color.white;

            // Component + serialized references.
            var minimap = root.AddComponent<Minimap>();
            var so = new SerializedObject(minimap);
            so.FindProperty("display").objectReferenceValue = raw;
            so.FindProperty("tileSize").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
#endif
