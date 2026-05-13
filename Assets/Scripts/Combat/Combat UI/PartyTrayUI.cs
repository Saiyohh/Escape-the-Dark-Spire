using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class PartyTrayUI : MonoBehaviour
    {
        [Tooltip("Parent transform that pills are spawned under. Should have a HorizontalLayoutGroup.")]
        [SerializeField] private Transform pillContainer;

        [Tooltip("Prefab spawned once per party member.")]
        [SerializeField] private PartyMemberCombatPill pillPrefab;

        private readonly List<PartyMemberCombatPill> pills = new();
        private bool boundOnce;

        // Per-pill rank handlers, captured so we can unsubscribe cleanly.
        private readonly Dictionary<Unit, System.Action<int, int>> rankHandlers = new();

        private void OnEnable()
        {
            CombatEvents.OnUnitTurnStart += HandleUnitTurnStart;
            CombatEvents.OnPhaseChanged  += HandlePhaseChanged;

            // CombatManager.Instance.PlayerUnits may not be populated yet during
            // scene boot — try once now, defer to first phase change otherwise.
            TryBuildPills();
        }

        private void OnDisable()
        {
            CombatEvents.OnUnitTurnStart -= HandleUnitTurnStart;
            CombatEvents.OnPhaseChanged  -= HandlePhaseChanged;

            UnbindAndClearPills();
            boundOnce = false;
        }

        // ── Bind / build ───────────────────────────────────────────────────

        private void HandlePhaseChanged(CombatPhase _)
        {
            if (!boundOnce) TryBuildPills();
        }

        private void TryBuildPills()
        {
            if (boundOnce) return;
            if (pillContainer == null || pillPrefab == null) return;

            var cm = CombatManager.Instance;
            if (cm == null || cm.PlayerUnits == null || cm.PlayerUnits.Count == 0) return;

            UnbindAndClearPills();

            foreach (var unit in cm.PlayerUnits)
            {
                if (unit == null) continue;
                var pill = Instantiate(pillPrefab, pillContainer);
                pill.Bind(unit, unit.characterData);
                pills.Add(pill);

                // Per-pill rank handler — re-sort whole tray on any rank change.
                System.Action<int, int> handler = (_, __) => ResortByRank();
                unit.OnRankChanged += handler;
                rankHandlers[unit] = handler;
            }

            ResortByRank();

            // Initial expansion: whoever is the active unit right now.
            var active = cm.ActiveUnit;
            if (active != null) HandleUnitTurnStart(active);

            boundOnce = true;
        }

        private void UnbindAndClearPills()
        {
            foreach (var kv in rankHandlers)
            {
                if (kv.Key != null && kv.Value != null)
                    kv.Key.OnRankChanged -= kv.Value;
            }
            rankHandlers.Clear();

            for (int i = 0; i < pills.Count; i++)
            {
                if (pills[i] == null) continue;
                pills[i].Unbind();
                Destroy(pills[i].gameObject);
            }
            pills.Clear();
        }

        // ── Event handlers ─────────────────────────────────────────────────

        private void HandleUnitTurnStart(Unit u)
        {
            for (int i = 0; i < pills.Count; i++)
            {
                var p = pills[i];
                if (p == null) continue;
                p.SetExpanded(p.BoundUnit == u);
            }
        }

        private void ResortByRank()
        {
            // Front (currentRank == 1) should be rightmost. With a default
            // left-to-right HorizontalLayoutGroup, that means the highest
            // sibling index. Sibling index = (count - rank).
            int count = pills.Count;
            for (int i = 0; i < pills.Count; i++)
            {
                var p = pills[i];
                if (p == null || p.BoundUnit == null) continue;
                int rank = Mathf.Clamp(p.BoundUnit.currentRank, 1, count);
                p.transform.SetSiblingIndex(count - rank);
            }
        }
    }
}
