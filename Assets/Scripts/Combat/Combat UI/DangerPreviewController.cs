// DangerPreviewController.cs
// -----------------------------------------------------------------------------
// Singleton that drives the "you are in danger" auras when the player hovers
// an enemy's intent icon. Owns a pool of DangerAura prefabs, spawns one per
// player the intent will threaten (resolved by IntentTargetResolver), and
// hides everything on:
//   • IntentIconUI.OnPointerExit             (explicit hide)
//   • CombatEvents.OnEnemyMoveSet            (intent retargeted — preview stale)
//   • CombatEvents.OnUnitTurnStart on source (enemy is about to act)
//   • CombatEvents.OnUnitTurnEnd   on source (defensive)
//
// IntentIconUI.OnPointerEnter calls ShowFor(source, intent). Subsequent
// ShowFor calls Hide() first so flipping between two intent icons on the
// same enemy is idempotent and the pool is reused.
//
// Each DangerAura is a world-space prefab (SpriteRenderer-based, sibling
// pattern to TurnIndicator). The controller is its stable parent — auras
// reparent themselves under the threatened player's UnitDisplay on Bind
// and back to the controller on Release.
//
// Scene wiring: drop a DangerPreviewController GameObject anywhere under
// CombatUI (the transform itself doesn't need to be anywhere specific —
// it's just the pool root). Assign the DangerAura prefab.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class DangerPreviewController : MonoBehaviour
    {
        public static DangerPreviewController Instance { get; private set; }

        [Tooltip("World-space DangerAura prefab. Spawned per threatened player and pooled.")]
        [SerializeField] private DangerAura auraPrefab;

        // Live preview state — what we're currently showing (or null if nothing).
        private Unit _previewSource;
        private EnemyIntent _previewIntent;

        // Pool + active set.
        private readonly List<DangerAura> _active = new();
        private readonly Stack<DangerAura> _pool  = new();

        // Reused per ShowFor call so resolution doesn't allocate.
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

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Show danger auras for every player <paramref name="intent"/> would
        /// threaten. Safe to call repeatedly — implicitly hides any prior
        /// preview first.
        /// </summary>
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

        /// <summary>Return every active aura to the pool and clear preview state.</summary>
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

        // ─── Lifecycle hooks ────────────────────────────────────────────────

        private void HandleEnemyMoveSet(Unit enemy, EnemyMove _)
        {
            if (enemy == _previewSource) Hide();
        }

        private void HandleUnitTurnLifecycle(Unit unit)
        {
            if (unit == _previewSource) Hide();
        }

        // ─── Pool plumbing ──────────────────────────────────────────────────

        private DangerAura AcquireAura()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled; // valid entry — reuse
                // else: previously destroyed by a scene unload / parent cleanup,
                // drop it and keep popping.
            }
            // Spawn a fresh one as our own child so DangerAura's Awake captures
            // this transform as its stableRoot. Subsequent Bind() reparents
            // under the target's UnitDisplay; Release() returns it here.
            var aura = Instantiate(auraPrefab, transform);
            aura.gameObject.SetActive(false);
            return aura;
        }
    }
}
