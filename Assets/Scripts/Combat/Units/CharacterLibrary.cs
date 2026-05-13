using System.Collections.Generic;
using UnityEngine;

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

        public static Color Signature(Alignment a)
        {
            var lib = Instance;
            var data = lib != null ? lib.Get(a) : null;
            return data != null ? data.signatureColor : CharacterPalette.DefaultSignature(a);
        }

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

    }
}
