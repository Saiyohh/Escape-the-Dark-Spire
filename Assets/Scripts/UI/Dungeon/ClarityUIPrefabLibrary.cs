// ClarityUIPrefabLibrary.cs
// -----------------------------------------------------------------------------
// Single source-of-truth for the dungeon clarity-pass UI prefabs (rest menu,
// first-run intro, icon legend, legend button, map hover tooltip). Lets the
// prefabs live in Assets/Prefabs/ClarityUI/ — outside any Resources folder —
// and still be loaded at runtime via this ScriptableObject's Instance.
//
// Mirrors the MapEntitySpriteLibrary pattern: one canonical asset at
// Assets/ScriptableObjects/ClarityUIPrefabLibrary.asset, registered into
// PlayerSettings → Preloaded Assets so the singleton is wired at game start
// without a Resources folder.
//
// Use ClarityUIPrefabScaffolder (Tools > DarkSpire > Clarity UI > ...) to
// (re)generate the prefabs + library and register it for preload in one click.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "ClarityUIPrefabLibrary",
                     menuName = "DarkSpire/UI/Clarity UI Prefab Library")]
    public class ClarityUIPrefabLibrary : ScriptableObject
    {
        public const string AssetPath =
            "Assets/ScriptableObjects/ClarityUIPrefabLibrary.asset";

        [Header("Default font")]
        [Tooltip("TMP font applied to every runtime-built UI text (flash " +
                 "messages, pickup toasts, modal labels, [E] prompts, etc.). " +
                 "Leave null to fall back to TMP's project default. The editor " +
                 "scaffolder auto-resolves Kreon_Regular_SDF if it can find it.")]
        public TMP_FontAsset defaultFont;

        [Header("Dungeon clarity-pass prefabs (Assets/Prefabs/ClarityUI)")]
        [Tooltip("Rest tile modal — heal / revive / view-stats / leave.")]
        public GameObject restMenu;

        [Tooltip("Onboarding card shown once per install on first dungeon entry.")]
        public GameObject firstRunIntro;

        [Tooltip("Icon guide modal — listing every dungeon icon with its meaning.")]
        public GameObject iconLegend;

        [Tooltip("Cursor-following name + description panel for dungeon hover.")]
        public GameObject mapHoverTooltip;

        // ── Singleton access ─────────────────────────────────────────────────
        private static ClarityUIPrefabLibrary _instance;
        public static ClarityUIPrefabLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<ClarityUIPrefabLibrary>(AssetPath);
#endif
                return _instance;
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }
    }
}
