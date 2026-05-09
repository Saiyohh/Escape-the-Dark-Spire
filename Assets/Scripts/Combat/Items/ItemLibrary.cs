// ItemLibrary.cs
// -----------------------------------------------------------------------------
// Central registry of every ItemData SO in the project, keyed by ItemID.
// Anything that needs an ItemData from an enum ID goes through
// `ItemLibrary.Instance.Get(id)` — single source of truth. Adding a new
// ItemData asset auto-registers it (via ItemLibraryPostprocessor); deletes
// drop from the lookup on the next access.
//
// Asset location: `Assets/ScriptableObjects/ItemLibrary.asset`.
// Create via: DarkSpire → Items → Create Library
//
// Mirrors the ConditionLibrary pattern exactly — see ConditionLibrary.cs for
// the rationale on why we use Preloaded Assets instead of a Resources folder.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "ItemLibrary", menuName = "DarkSpire/Item Library")]
    public class ItemLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/ItemLibrary.asset";

        // ── Singleton access ──────────────────────────────────────────────────
        private static ItemLibrary _instance;
        public static ItemLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;

#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<ItemLibrary>(AssetPath);
#endif
                if (_instance == null)
                    Debug.LogError(
                        $"[ItemLibrary] Missing asset at {AssetPath}. " +
                        "Create one via DarkSpire → Items → Create Library.");
                return _instance;
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        [SerializeField]
        private List<ItemData> items = new();

        [System.NonSerialized]
        private Dictionary<ItemID, ItemData> _lookup;

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<ItemID, ItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (item.itemID == ItemID.None) continue;
                // First-write wins — duplicate IDs across the project produce
                // an editor-only warning surfaced by ItemDataEditor.
                if (_lookup.ContainsKey(item.itemID)) continue;
                _lookup[item.itemID] = item;
            }
        }

        /// <summary>Returns the ItemData registered for the given ID, or null.</summary>
        public ItemData Get(ItemID id)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(id, out var item) ? item : null;
        }

        public bool Contains(ItemID id)
        {
            BuildLookupIfNeeded();
            return _lookup.ContainsKey(id);
        }

        /// <summary>Iteration helper for the custom editor and party inventory UI.</summary>
        public IReadOnlyList<ItemData> All
        {
            get
            {
                items.RemoveAll(i => i == null);
                return items;
            }
        }

        private void OnValidate()
        {
            _lookup = null;
        }

#if UNITY_EDITOR
        public void Register(ItemData item)
        {
            if (item == null) return;
            if (items.Contains(item)) return;
            items.Add(item);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public void Unregister(ItemData item)
        {
            if (item == null) return;
            if (items.Remove(item))
            {
                _lookup = null;
                EditorUtility.SetDirty(this);
            }
        }

        public void RefreshFromProject()
        {
            items.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null) items.Add(item);
            }
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Editor-only: finds every ItemData asset that uses the given ID.
        /// Used by ItemDataEditor to surface a duplicate-ID warning.
        /// </summary>
        public int CountWithID(ItemID id)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].itemID == id) count++;
            return count;
        }
#endif
    }
}
