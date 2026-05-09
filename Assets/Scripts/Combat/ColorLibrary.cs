// ColorLibrary.cs
// -----------------------------------------------------------------------------
// Singleton ScriptableObject of named colors organized into Categories.
// Every color belongs to a Category — no free-floating colors. Lookup at
// runtime is by either ("Category", "Name") or "Category/Name" string,
// returning a fallback when missing.
//
//   Color c = ColorLibrary.Get("UI", "ButtonHover");
//   Color c = ColorLibrary.Get("UI/ButtonHover");
//
// Asset location: Assets/ScriptableObjects/ColorLibrary.asset
// Mirrors ConditionLibrary / CharacterLibrary — preloaded for runtime access.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<ColorLibrary>(AssetPath);
#endif
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

#if UNITY_EDITOR
        private void OnValidate() => _lookup = null;
#endif

        // ── Static API ──────────────────────────────────────────────────────

        /// <summary>Lookup by ("Category", "Name"). Returns fallback on miss.</summary>
        public static Color Get(string category, string name, Color fallback)
            => Get($"{category}/{name}", fallback);

        /// <summary>Lookup by "Category/Name" composite key. Returns fallback on miss.</summary>
        public static Color Get(string compositeKey, Color fallback)
        {
            var lib = Instance;
            if (lib == null) return fallback;
            lib.BuildLookupIfNeeded();
            return lib._lookup.TryGetValue(compositeKey ?? string.Empty, out var c) ? c : fallback;
        }

        /// <summary>Magenta-fallback variant for loud "you forgot to wire this" misses.</summary>
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

#if UNITY_EDITOR
        // Editor helpers used by the custom inspector buttons.

        public Category AddCategory(string name = "New Category")
        {
            var cat = new Category { name = name };
            categories.Add(cat);
            _lookup = null;
            EditorUtility.SetDirty(this);
            return cat;
        }

        public void RemoveCategory(int index)
        {
            if (index < 0 || index >= categories.Count) return;
            categories.RemoveAt(index);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public NamedColor AddColor(int categoryIndex, string name = "New Color", Color color = default)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return null;
            if (color == default) color = Color.white;
            var nc = new NamedColor { name = name, color = color };
            categories[categoryIndex].colors.Add(nc);
            _lookup = null;
            EditorUtility.SetDirty(this);
            return nc;
        }

        public void RemoveColor(int categoryIndex, int colorIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return;
            var list = categories[categoryIndex].colors;
            if (colorIndex < 0 || colorIndex >= list.Count) return;
            list.RemoveAt(colorIndex);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public void NotifyMutated()
        {
            _lookup = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
