// SpriteOutlineMenu.cs
// -----------------------------------------------------------------------------
// Creates preset materials for the unified DarkSpire/Sprite shader. Since that
// shader carries both paper softening AND outline features, one shader powers
// every use case — the only difference between the presets is which params
// start enabled.
//
// Menu:
//   DarkSpire/Materials/Create Sprite Material (Plain)    — no effects, just shader
//   DarkSpire/Materials/Create Sprite Material (Paper)    — softening + grain on
//   DarkSpire/Materials/Create Sprite Material (Outline)  — outline on (color white, width 2)
//   DarkSpire/Materials/Create Sprite Material (Paper + Outline)
//                                                         — both effects on
//
//   GameObject/DarkSpire/Apply Paper Sprite Material to Children
//     (right-click in hierarchy) — bulk-assigns the first Paper preset found
//     to every SpriteRenderer under the selection.
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class SpriteOutlineMenu
    {
        public const string ShaderName = "DarkSpire/Sprite";

        private const string PlainPath      = "Assets/Materials/Sprite_Plain.mat";
        private const string PaperPath      = "Assets/Materials/Sprite_Paper.mat";
        private const string OutlinePath    = "Assets/Materials/Sprite_Outline.mat";
        private const string CombinedPath   = "Assets/Materials/Sprite_PaperAndOutline.mat";

        [MenuItem("DarkSpire/Materials/Create Sprite Material (Plain)")]
        public static void CreatePlain() => Create(PlainPath, paper: false, outline: false);

        [MenuItem("DarkSpire/Materials/Create Sprite Material (Paper)")]
        public static void CreatePaper() => Create(PaperPath, paper: true, outline: false);

        [MenuItem("DarkSpire/Materials/Create Sprite Material (Outline)")]
        public static void CreateOutline() => Create(OutlinePath, paper: false, outline: true);

        [MenuItem("DarkSpire/Materials/Create Sprite Material (Paper + Outline)")]
        public static void CreateCombined() => Create(CombinedPath, paper: true, outline: true);

        private static void Create(string targetPath, bool paper, bool outline)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader missing",
                    $"Couldn't find '{ShaderName}'. Make sure " +
                    "Assets/Shaders/DarkSpireSprite.shader compiled cleanly.",
                    "OK");
                return;
            }

            Directory.CreateDirectory("Assets/Materials");
            string path = AssetDatabase.GenerateUniqueAssetPath(targetPath);

            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };

            if (paper)
            {
                mat.SetFloat("_EdgeSoftness", 1.5f);
                mat.SetFloat("_GrainStrength", 0.12f);
                mat.SetFloat("_GrainScale", 60f);
                mat.SetColor("_PaperTint", new Color(1f, 0.97f, 0.92f, 1f));
            }
            if (outline)
            {
                mat.SetColor("_OutlineColor", Color.white);
                mat.SetFloat("_OutlineWidth", 2f);
                mat.SetFloat("_OutlineSoftness", 0.5f);
                mat.SetFloat("_OutlineAlphaThresh", 0.3f);
                // Dithered outline by default — makes the stroke look hand-drawn
                // instead of flat. Higher when paper softening is also on so both
                // effects register as "same pencil, same paper."
                mat.SetFloat("_OutlineGrainStrength", paper ? 0.55f : 0.3f);
                mat.SetFloat("_OutlineGrainScale", 1.0f);
                mat.SetFloat("_OutlineBehind", 1f);
            }

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = mat;
            Debug.Log($"[SpriteMaterial] Created → {path} (paper={paper}, outline={outline})");
        }

        [MenuItem("GameObject/DarkSpire/Apply Paper Sprite Material to Children", false, 40)]
        public static void ApplyPaperToChildren()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("No selection",
                    "Select a GameObject in the hierarchy first.", "OK");
                return;
            }

            var material = FindFirstPaperMaterial();
            if (material == null)
            {
                if (!EditorUtility.DisplayDialog("No paper material found",
                    "No material using DarkSpire/Sprite with paper settings was found. " +
                    "Create one now?", "Create", "Cancel")) return;
                CreatePaper();
                material = FindFirstPaperMaterial();
                if (material == null) return;
            }

            int count = 0;
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Undo.RecordObject(r, "Assign Paper Sprite Material");
                r.sharedMaterial = material;
                count++;
            }
            Debug.Log($"[SpriteMaterial] Applied '{material.name}' to {count} SpriteRenderer(s) under {root.name}");
        }

        private static Material FindFirstPaperMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) return null;
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || m.shader != shader) continue;
                if (m.GetFloat("_EdgeSoftness") > 0.001f ||
                    m.GetFloat("_GrainStrength") > 0.001f) return m;
            }
            return null;
        }
    }
}
