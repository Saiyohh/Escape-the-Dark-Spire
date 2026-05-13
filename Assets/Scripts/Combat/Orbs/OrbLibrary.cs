using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "OrbLibrary", menuName = "DarkSpire/Orb Library")]
    public class OrbLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/Libraries/OrbLibrary.asset";

        private static OrbLibrary _instance;
        public static OrbLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_instance == null)
                    Debug.LogError(
                        $"[OrbLibrary] Missing asset at {AssetPath}. " +
                        "Create one via Project → Create → DarkSpire → Orb Library.");
                return _instance;
            }
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        [SerializeField] private List<OrbDataSO> orbs = new();

        [System.NonSerialized] private Dictionary<OrbType, OrbDataSO> _lookup;

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<OrbType, OrbDataSO>();
            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                if (o == null) continue;
                _lookup[o.orbType] = o;
            }
        }

        public OrbDataSO Get(OrbType type)
        {
            BuildLookupIfNeeded();
            if (type == OrbType.Random)
            {
                if (orbs.Count == 0) return null;
                int n = orbs.Count;
                int idx = Random.Range(0, n);
                // Skip the Random sentinel if it ever ends up registered.
                for (int i = 0; i < n; i++)
                {
                    var pick = orbs[(idx + i) % n];
                    if (pick != null && pick.orbType != OrbType.Random) return pick;
                }
                return null;
            }
            return _lookup.TryGetValue(type, out var o) ? o : null;
        }

        public IReadOnlyList<OrbDataSO> All
        {
            get
            {
                orbs.RemoveAll(o => o == null);
                return orbs;
            }
        }

        public OrbDataSO GetRandomByTier(int tier)
        {
            BuildLookupIfNeeded();
            // Walk the serialized list directly so we draw from EVERY orb of
            // that tier, including any added later (e.g. an aspect that
            // unlocks a third Tier 1 orb).
            int count = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                if (o == null || o.tier != tier) continue;
                count++;
            }
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                if (o == null || o.tier != tier) continue;
                if (pick == 0) return o;
                pick--;
            }
            return null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }

    }
}
