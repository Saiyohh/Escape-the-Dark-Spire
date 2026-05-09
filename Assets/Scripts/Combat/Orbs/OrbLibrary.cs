// OrbLibrary.cs
// -----------------------------------------------------------------------------
// Central registry of every OrbDataSO in the project, keyed by OrbType.
// Mirrors ConditionLibrary's pattern: a single ScriptableObject at a fixed
// path, registered into PlayerSettings.preloadedAssets so it loads at runtime
// without a Resources folder. SkillResolver and OrbStrike both reach the
// per-type SO via `OrbLibrary.Instance.Get(orbType)`.
//
// Asset location: `Assets/ScriptableObjects/OrbLibrary.asset`.
//
// OrbType.Random is a sentinel — Get returns a uniform pick over all
// non-Random orbs registered.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
#if UNITY_EDITOR
                _instance = AssetDatabase.LoadAssetAtPath<OrbLibrary>(AssetPath);
#endif
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

        /// <summary>
        /// Returns the OrbDataSO for the given type. OrbType.Random returns a
        /// uniform pick over all registered non-Random orbs.
        /// </summary>
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

        /// <summary>
        /// Returns a random orb whose <c>tier</c> matches <paramref name="tier"/>,
        /// or null if no orb of that tier is registered. Used by Defect-style
        /// weapons whose channelOrbOnAttack auto-picks from the Tier 1 pool
        /// (Lightning, Frost) without hardcoding the type list.
        /// </summary>
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

#if UNITY_EDITOR
        public void Register(OrbDataSO o)
        {
            if (o == null) return;
            if (orbs.Contains(o)) return;
            orbs.Add(o);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public void Unregister(OrbDataSO o)
        {
            if (o == null) return;
            if (orbs.Remove(o))
            {
                _lookup = null;
                EditorUtility.SetDirty(this);
            }
        }

        public void RefreshFromProject()
        {
            orbs.Clear();
            string[] guids = AssetDatabase.FindAssets("t:OrbDataSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var o = AssetDatabase.LoadAssetAtPath<OrbDataSO>(path);
                if (o != null) orbs.Add(o);
            }
            _lookup = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
