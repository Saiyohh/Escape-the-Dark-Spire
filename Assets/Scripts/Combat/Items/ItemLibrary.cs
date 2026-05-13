using System.Collections.Generic;
using UnityEngine;

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

    }
}
