using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "ColorLibrary", menuName = "DarkSpire/Color Library")]
    public class ColorLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/ColorLibrary.asset";

        [System.Serializable]
        public class NamedColor
        {
            public string name = "Untitled";
            public Color color = Color.white;
        }

        [System.Serializable]
        public class Category
        {
            public string name = "Untitled";
            public List<NamedColor> colors = new();
        }

        [SerializeField] private List<Category> categories = new();

        public IReadOnlyList<Category> Categories => categories;

        // ── Singleton ───────────────────────────────────────────────────────
        private static ColorLibrary _instance;
        public static ColorLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
                return _instance;
            }
        }

        // Lookup cache — flattened "Category/Name" → Color.
        [System.NonSerialized] private Dictionary<string, Color> _lookup;

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            _lookup = null;
        }

        // ── Static API ──────────────────────────────────────────────────────

        public static Color Get(string category, string name, Color fallback)
            => Get($"{category}/{name}", fallback);

        public static Color Get(string compositeKey, Color fallback)
        {
            var lib = Instance;
            if (lib == null) return fallback;
            lib.BuildLookupIfNeeded();
            return lib._lookup.TryGetValue(compositeKey ?? string.Empty, out var c) ? c : fallback;
        }

        public static Color Get(string category, string name)
            => Get(category, name, new Color(1f, 0f, 1f, 1f));

        public static Color Get(string compositeKey)
            => Get(compositeKey, new Color(1f, 0f, 1f, 1f));

        public static bool TryGet(string category, string name, out Color color)
            => TryGet($"{category}/{name}", out color);

        public static bool TryGet(string compositeKey, out Color color)
        {
            color = default;
            var lib = Instance;
            if (lib == null) return false;
            lib.BuildLookupIfNeeded();
            return lib._lookup.TryGetValue(compositeKey ?? string.Empty, out color);
        }

        public static bool Contains(string category, string name)
            => TryGet(category, name, out _);

        // ── Internal ────────────────────────────────────────────────────────

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Color>(64);
            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                if (cat == null || string.IsNullOrWhiteSpace(cat.name)) continue;
                for (int j = 0; j < cat.colors.Count; j++)
                {
                    var c = cat.colors[j];
                    if (c == null || string.IsNullOrWhiteSpace(c.name)) continue;
                    _lookup[$"{cat.name}/{c.name}"] = c.color;
                }
            }
        }

    }
}
