// ConditionLibrary.cs
// -----------------------------------------------------------------------------
// Central registry of every ConditionData SO in the project, keyed by
// ConditionID. Anything that needs a ConditionData from an enum ID goes
// through `ConditionLibrary.Instance.Get(id)` — this is the single source of
// truth. Adding a new ConditionData asset to the project auto-registers it
// (via ConditionLibraryPostprocessor), and removing one drops it from the
// lookup on the next access.
//
// Asset location: `Assets/ScriptableObjects/ConditionLibrary.asset`.
// Create via Menu: DarkSpire → Conditions → Create Library.
//
// Loading mechanism (why not Resources):
//   The library lives OUTSIDE of any Resources folder so all gameplay data
//   sits together under Assets/ScriptableObjects/. Runtime access works via
//   Unity's "Preloaded Assets" list (Project Settings → Player → Preloaded
//   Assets) — the menu command auto-adds the library on creation, and the
//   SO's OnEnable callback wires itself into the static Instance as soon as
//   the asset is loaded at game start. In the editor, a direct AssetDatabase
//   lookup is the fallback when Instance is accessed before a play session.
//
// Why a singleton at a fixed path:
//   • Every system that touches conditions (SkillResolver, WeaponData on-hit,
//     EnemyData actions, Unit.GainDefense for Shields, combat UI) needs the
//     same mapping. Passing an SO array down through bootstrap is tedious
//     and error-prone.
//   • Designers add a new ConditionData SO → library updates on next asset
//     import → every skill using that ConditionID now resolves correctly.
//
// The ConditionID enum is still the addressing system (C# enums can't be
// extended at runtime). When a designer needs a genuinely new condition type,
// they add one entry to `ConditionID` in CombatEnums.cs, create the SO, and
// the library picks it up automatically. The library is "dynamic" in the
// sense that its CONTENT is authored-driven; the enum is the stable address
// space.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "ConditionLibrary", menuName = "DarkSpire/Condition Library")]
    public class ConditionLibrary : ScriptableObject
    {
        // Canonical project-relative path. The menu command both creates the
        // asset here and registers it into PlayerSettings.preloadedAssets so
        // Unity loads it at runtime without a Resources folder.
        public const string AssetPath = "Assets/ScriptableObjects/ConditionLibrary.asset";

        // ── Singleton access ──────────────────────────────────────────────────
        private static ConditionLibrary _instance;
        public static ConditionLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;

#if UNITY_EDITOR
                // Editor fallback — preloaded-assets loading only runs during
                // Play / builds. In edit mode, access the asset directly.
                _instance = AssetDatabase.LoadAssetAtPath<ConditionLibrary>(AssetPath);
#endif
                if (_instance == null)
                    Debug.LogError(
                        $"[ConditionLibrary] Missing asset at {AssetPath}. " +
                        "Create one via DarkSpire → Conditions → Create Library.");
                return _instance;
            }
        }

        /// <summary>
        /// Unity calls OnEnable when the asset is loaded (either via the
        /// preloaded-assets list at runtime, or any time the inspector opens
        /// it in the editor). First call after domain reload claims the
        /// singleton slot for this asset instance.
        /// </summary>
        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        // Serialized set of conditions. Edit directly in the inspector or via
        // the postprocessor / menu refresh.
        [SerializeField]
        private List<ConditionData> conditions = new();

        // Lazy-built lookup. Rebuilt whenever the backing list changes.
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

        /// <summary>Returns the ConditionData registered for the given ID, or null.</summary>
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

        /// <summary>Iteration helper for the custom editor.</summary>
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
            // Invalidate lookup when the list changes in-editor.
            _lookup = null;
        }

#if UNITY_EDITOR
        /// <summary>Called by the postprocessor when a new ConditionData is imported.</summary>
        public void Register(ConditionData c)
        {
            if (c == null) return;
            if (conditions.Contains(c)) return;
            conditions.Add(c);
            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public void Unregister(ConditionData c)
        {
            if (c == null) return;
            if (conditions.Remove(c))
            {
                _lookup = null;
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Editor-only: rebuild the list by scanning every ConditionData asset
        /// in the project. Useful after bulk-importing SOs or recovering from
        /// a stale library.
        /// </summary>
        public void RefreshFromProject()
        {
            conditions.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ConditionData");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var c = AssetDatabase.LoadAssetAtPath<ConditionData>(path);
                if (c != null) conditions.Add(c);
            }
            _lookup = null;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
