using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class DangerPreviewController : MonoBehaviour
    {
        public static DangerPreviewController Instance { get; private set; }

        [Tooltip("World-space DangerAura prefab. Spawned per threatened player and pooled.")]
        [SerializeField] private DangerAura auraPrefab;

        private Unit _previewSource;
        private EnemyIntent _previewIntent;

        private readonly List<DangerAura> _active = new();
        private readonly Stack<DangerAura> _pool  = new();

        private readonly List<IntentTargetEntry> _resolved = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            CombatEvents.OnEnemyMoveSet  += HandleEnemyMoveSet;
            CombatEvents.OnUnitTurnStart += HandleUnitTurnLifecycle;
            CombatEvents.OnUnitTurnEnd   += HandleUnitTurnLifecycle;
        }

        private void OnDisable()
        {
            CombatEvents.OnEnemyMoveSet  -= HandleEnemyMoveSet;
            CombatEvents.OnUnitTurnStart -= HandleUnitTurnLifecycle;
            CombatEvents.OnUnitTurnEnd   -= HandleUnitTurnLifecycle;
            if (Instance == this) Instance = null;
        }

        public void ShowFor(Unit source, EnemyIntent intent)
        {
            Hide();

            if (source == null || !source.IsAlive || intent == null) return;
            if (auraPrefab == null) return;

            var mgr = CombatManager.Instance;
            if (mgr == null) return;

            IntentTargetResolver.Resolve(
                source, intent, mgr.PlayerUnits, mgr.EnemyUnits, _resolved);
            if (_resolved.Count == 0) return;

            _previewSource = source;
            _previewIntent = intent;

            for (int i = 0; i < _resolved.Count; i++)
            {
                var entry = _resolved[i];
                var display = UnitDisplay.GetDisplay(entry.unit);
                if (display == null) continue;

                var aura = AcquireAura();
                aura.Bind(entry, display);
                _active.Add(aura);
            }
        }

        public void Hide()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null) continue;
                a.Release();
                _pool.Push(a);
            }
            _active.Clear();
            _previewSource = null;
            _previewIntent = null;
        }

        private void HandleEnemyMoveSet(Unit enemy, EnemyMove _)
        {
            if (enemy == _previewSource) Hide();
        }

        private void HandleUnitTurnLifecycle(Unit unit)
        {
            if (unit == _previewSource) Hide();
        }

        private DangerAura AcquireAura()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled; // valid entry — reuse
            }
            var aura = Instantiate(auraPrefab, transform);
            aura.gameObject.SetActive(false);
            return aura;
        }
    }
}
