using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "ConditionLibrary", menuName = "DarkSpire/Condition Library")]
    public class ConditionLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/ConditionLibrary.asset";

        private static ConditionLibrary _instance;
        public static ConditionLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;

                if (_instance == null)
                    Debug.LogError(
                        $"[ConditionLibrary] Missing asset at {AssetPath}. " +
                        "Create one via DarkSpire → Conditions → Create Library.");
                return _instance;
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        [SerializeField]
        private List<ConditionData> conditions = new();

        [System.NonSerialized]
        private Dictionary<ConditionID, ConditionData> _lookup;

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<ConditionID, ConditionData>();
            for (int i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (c == null) continue;
                _lookup[c.conditionID] = c;
            }
        }

        public ConditionData Get(ConditionID id)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(id, out var c) ? c : null;
        }

        public bool Contains(ConditionID id)
        {
            BuildLookupIfNeeded();
            return _lookup.ContainsKey(id);
        }

        public IReadOnlyList<ConditionData> All
        {
            get
            {
                conditions.RemoveAll(c => c == null); // drop deleted entries lazily
                return conditions;
            }
        }

        private void OnValidate()
        {
            _lookup = null;
        }

    }
}
