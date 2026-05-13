using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "KeywordGlossary", menuName = "DarkSpire/Keyword Glossary")]
    public class KeywordGlossary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/KeywordGlossary.asset";

        [System.Serializable]
        public class Entry
        {
            [Tooltip("Lookup key — case-insensitive. Standalone entries usually " +
                     "match the displayed word verbatim ('Attack', 'Seal'). " +
                     "Condition-linked entries use the ConditionID name.")]
            public string key;

            [Tooltip("Word as it appears in the description. Empty falls back to key.")]
            public string displayName;

            [TextArea(2, 6)]
            [Tooltip("Tooltip body for standalone entries. Ignored when linkedCondition " +
                     "is set — the body is pulled from ConditionData.description.")]
            public string description;

            [Tooltip("Optional icon for the tooltip header. Ignored for condition-linked " +
                     "entries (uses ConditionData.icon).")]
            public Sprite icon;

            [Tooltip("Per-keyword color override. When alpha == 0 the renderer falls " +
                     "back to the default keyword color from ColorLibrary " +
                     "(Text/KeywordPaleYellow).")]
            public Color colorOverride = new Color(0f, 0f, 0f, 0f);

            [Tooltip("If set, this entry is a condition reference. Its display name + " +
                     "icon + body are pulled from the matching ConditionData via " +
                     "ConditionLibrary.Get(linkedCondition). Use the matching " +
                     "ConditionID enum value here; leave 'isConditionLinked' true.")]
            public ConditionID linkedCondition;

            [Tooltip("True when linkedCondition should be honored. Needed because the " +
                     "ConditionID enum has no 'None' value — we have to opt in explicitly.")]
            public bool isConditionLinked;
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        // ── Singleton ───────────────────────────────────────────────────────
        private static KeywordGlossary _instance;
        public static KeywordGlossary Instance
        {
            get
            {
                if (_instance != null) return _instance;
                return _instance;
            }
        }

        [System.NonSerialized] private Dictionary<string, Entry> _byKey;
        [System.NonSerialized] private Dictionary<ConditionID, Entry> _byCondition;

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            _byKey = null;
            _byCondition = null;
        }

        private void BuildLookupIfNeeded()
        {
            if (_byKey != null && _byCondition != null) return;
            _byKey = new Dictionary<string, Entry>(System.StringComparer.OrdinalIgnoreCase);
            _byCondition = new Dictionary<ConditionID, Entry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(e.key))
                    _byKey[e.key] = e;
                if (e.isConditionLinked)
                    _byCondition[e.linkedCondition] = e;
            }
        }

        public Entry Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            BuildLookupIfNeeded();
            return _byKey.TryGetValue(key, out var e) ? e : null;
        }

        public Entry ResolveCondition(ConditionID id)
        {
            BuildLookupIfNeeded();
            return _byCondition.TryGetValue(id, out var e) ? e : null;
        }

        // ── Resolution helpers ──────────────────────────────────────────────
        // Encapsulate the "is this entry condition-linked? then pull from
        // ConditionData" branch so callers don't all duplicate it.

        public string GetDisplayName(Entry e)
        {
            if (e == null) return string.Empty;
            if (e.isConditionLinked)
            {
                var cd = ConditionLibrary.Instance != null
                    ? ConditionLibrary.Instance.Get(e.linkedCondition)
                    : null;
                if (cd != null && !string.IsNullOrEmpty(cd.displayName))
                    return cd.displayName;
            }
            return !string.IsNullOrEmpty(e.displayName) ? e.displayName : e.key ?? string.Empty;
        }

        public string GetDescription(Entry e)
        {
            if (e == null) return string.Empty;
            if (e.isConditionLinked)
            {
                var cd = ConditionLibrary.Instance != null
                    ? ConditionLibrary.Instance.Get(e.linkedCondition)
                    : null;
                if (cd != null && !string.IsNullOrEmpty(cd.description))
                    return cd.description;
            }
            return e.description ?? string.Empty;
        }

        public Sprite GetIcon(Entry e)
        {
            if (e == null) return null;
            if (e.isConditionLinked)
            {
                var cd = ConditionLibrary.Instance != null
                    ? ConditionLibrary.Instance.Get(e.linkedCondition)
                    : null;
                if (cd != null && cd.icon != null)
                    return cd.icon;
            }
            return e.icon;
        }

        public Color GetColor(Entry e)
        {
            if (e != null && e.colorOverride.a > 0f) return e.colorOverride;
            return ColorLibrary.Get("Text/KeywordPaleYellow", new Color(0.96f, 0.86f, 0.54f, 1f));
        }
    }
}
