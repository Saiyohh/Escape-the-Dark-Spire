// CharacterLibrary.cs
// -----------------------------------------------------------------------------
// Central registry of every CharacterData SO in the project, keyed by Alignment.
// Mirrors ConditionLibrary. Anything that needs a CharacterData from an
// Alignment (editor inspectors reading signature/highlight colors, future UI
// pulling portrait + palette, etc.) goes through
// `CharacterLibrary.Instance.Get(alignment)`.
//
// Asset location: `Assets/ScriptableObjects/CharacterLibrary.asset`.
// Create via: DarkSpire → Characters → Create Library.
// Auto-registers new CharacterData SOs on import (see postprocessor).
//
// Loading mechanism: same as ConditionLibrary — preloaded-assets at runtime,
// AssetDatabase fallback in the editor. Not inside a Resources folder.
//
// The Alignment enum is the stable address space. Adding a new playable
// character in code means adding an Alignment value; the library then
// associates the SO with it.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "CharacterLibrary", menuName = "DarkSpire/Character Library")]
    public class CharacterLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/CharacterLibrary.asset";

        private static CharacterLibrary _instance;
        public static CharacterLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;

#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(AssetPath);
#endif
                return _instance; // null is valid — call sites fall back to CharacterPalette defaults
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        [SerializeField]
        private List<CharacterData> characters = new();

        [System.NonSerialized]
        private Dictionary<Alignment, CharacterData> _lookup;

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<Alignment, CharacterData>();
            for (int i = 0; i < characters.Count; i++)
            {
                var c = characters[i];
                if (c == null) continue;
                _lookup[c.alignment] = c;
            }
        }

        /// <summary>Returns the CharacterData for the given alignment, or null.</summary>
        public CharacterData Get(Alignment a)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(a, out var c) ? c : null;
        }

        public bool Contains(Alignment a)
        {
            BuildLookupIfNeeded();
            return _lookup.ContainsKey(a);
        }

        /// <summary>
        /// Signature color lookup with fallback. Call sites can use this
        /// directly: `CharacterLibrary.Signature(Alignment.Ironclad)`.
        /// </summary>
        public static Color Signature(Alignment a)
        {
            var lib = Instance;
            var data = lib != null ? lib.Get(a) : null;
            return data != null ? data.signatureColor : CharacterPalette.DefaultSignature(a);
        }

        /// <summary>Highlight color lookup with fallback.</summary>
        public static Color Highlight(Alignment a)
        {
            var lib = Instance;
            var data = lib != null ? lib.Get(a) : null;
            return data != null ? data.highlightColor : CharacterPalette.DefaultHighlight(a);
        }

        public IReadOnlyList<CharacterData> All
        {
            get
            {
                characters.RemoveAll(c => c == null);
                return characters;
            }
        }

        private void OnValidate() => _lookup = null;

#if UNITY_EDITOR
        public void Register(CharacterData c)
        {
            if (c == null || characters.Contains(c)) return;
            characters.Add(c);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public void Unregister(CharacterData c)
        {
            if (c == null) return;
            if (characters.Remove(c)) { _lookup = null; EditorUtility.SetDirty(this); }
        }

        public void RefreshFromProject()
        {
            characters.Clear();
            string[] guids = AssetDatabase.FindAssets("t:CharacterData");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (c != null) characters.Add(c);
            }
            _lookup = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
