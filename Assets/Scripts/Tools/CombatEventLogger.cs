using UnityEngine;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tools/Combat Event Logger")]
    public class CombatEventLogger : MonoBehaviour
    {
        [Header("Log Filters")]
        public bool logPhase = true;
        public bool logTurns = true;
        public bool logDice = true;
        public bool logActions = true;
        public bool logHits = true;
        public bool logDeaths = true;
        public bool logCombatLifecycle = true;
        public bool logActionState = false;

        private void OnEnable()
        {
            CombatEvents.OnCombatStart        += HandleCombatStart;
            CombatEvents.OnCombatEnd          += HandleCombatEnd;
            CombatEvents.OnPhaseChanged       += HandlePhaseChanged;
            CombatEvents.OnRoundStart         += HandleRoundStart;
            CombatEvents.OnRoundEnd           += HandleRoundEnd;
            CombatEvents.OnPlayerPhaseStart   += HandlePlayerPhaseStart;
            CombatEvents.OnPlayerPhaseEnd     += HandlePlayerPhaseEnd;
            CombatEvents.OnEnemyPhaseStart    += HandleEnemyPhaseStart;
            CombatEvents.OnEnemyPhaseEnd      += HandleEnemyPhaseEnd;
            CombatEvents.OnTurnStart          += HandleTurnStart;
            CombatEvents.OnTurnEnd            += HandleTurnEnd;
            CombatEvents.OnUnitTurnStart      += HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd        += HandleUnitTurnEnd;
            CombatEvents.OnDiceRolled         += HandleDiceRolled;
            CombatEvents.OnActionResolved     += HandleActionResolved;
            CombatEvents.OnPlayerHit          += HandlePlayerHit;
            CombatEvents.OnEnemyDeath         += HandleEnemyDeath;
            CombatEvents.OnActionStateChanged += HandleActionStateChanged;
        }

        private void OnDisable()
        {
            CombatEvents.OnCombatStart        -= HandleCombatStart;
            CombatEvents.OnCombatEnd          -= HandleCombatEnd;
            CombatEvents.OnPhaseChanged       -= HandlePhaseChanged;
            CombatEvents.OnRoundStart         -= HandleRoundStart;
            CombatEvents.OnRoundEnd           -= HandleRoundEnd;
            CombatEvents.OnPlayerPhaseStart   -= HandlePlayerPhaseStart;
            CombatEvents.OnPlayerPhaseEnd     -= HandlePlayerPhaseEnd;
            CombatEvents.OnEnemyPhaseStart    -= HandleEnemyPhaseStart;
            CombatEvents.OnEnemyPhaseEnd      -= HandleEnemyPhaseEnd;
            CombatEvents.OnTurnStart          -= HandleTurnStart;
            CombatEvents.OnTurnEnd            -= HandleTurnEnd;
            CombatEvents.OnUnitTurnStart      -= HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd        -= HandleUnitTurnEnd;
            CombatEvents.OnDiceRolled         -= HandleDiceRolled;
            CombatEvents.OnActionResolved     -= HandleActionResolved;
            CombatEvents.OnPlayerHit          -= HandlePlayerHit;
            CombatEvents.OnEnemyDeath         -= HandleEnemyDeath;
            CombatEvents.OnActionStateChanged -= HandleActionStateChanged;
        }

        private void HandleCombatStart()
        {
            if (logCombatLifecycle) Debug.Log("[Combat] ⚔ Start");
        }
        private void HandleCombatEnd(bool victory)
        {
            if (logCombatLifecycle) Debug.Log($"[Combat] End → {(victory ? "VICTORY" : "DEFEAT")}");
        }
        private void HandlePhaseChanged(CombatPhase p)
        {
            if (logPhase) Debug.Log($"[Combat] Phase → {p}");
        }
        private void HandleTurnStart(int n)
        {
            if (logTurns) Debug.Log($"[Combat] ── Turn {n} begins ──");
        }
        private void HandleTurnEnd(int n)
        {
            if (logTurns) Debug.Log($"[Combat] ── Turn {n} ends ──");
        }
        private void HandleRoundStart(int n)
        {
            if (logTurns) Debug.Log($"[Combat] ═══ ROUND {n} START ═══");
        }
        private void HandleRoundEnd(int n)
        {
            if (logTurns) Debug.Log($"[Combat] ═══ ROUND {n} END ═══");
        }
        private void HandlePlayerPhaseStart()
        {
            if (logPhase) Debug.Log("[Combat] ▶ Player Phase Start");
        }
        private void HandlePlayerPhaseEnd()
        {
            if (logPhase) Debug.Log("[Combat] ◀ Player Phase End");
        }
        private void HandleEnemyPhaseStart()
        {
            if (logPhase) Debug.Log("[Combat] ▶ Enemy Phase Start");
        }
        private void HandleEnemyPhaseEnd()
        {
            if (logPhase) Debug.Log("[Combat] ◀ Enemy Phase End");
        }
        private void HandleUnitTurnStart(Unit u)
        {
            if (logTurns) Debug.Log($"[Combat] • {u.unitName} → turn start (HP {u.currentHP}/{u.maxHP} SP {u.currentSP}/{u.maxSP})");
        }
        private void HandleUnitTurnEnd(Unit u)
        {
            if (logTurns) Debug.Log($"[Combat] • {u.unitName} → turn end");
        }
        private void HandleDiceRolled(Unit u, int raw, int total, bool hit, bool crit)
        {
            if (logDice)
                Debug.Log($"[Combat] 🎲 {u.unitName}: raw {raw}, total {total} → " +
                          $"{(crit ? "CRIT " : "")}{(hit ? "HIT" : "MISS")}");
        }
        private void HandleActionResolved(CombatActionResult r)
        {
            if (!logActions || r == null) return;
            string line = $"[Combat] → {r.source?.unitName ?? "?"} {r.actionName}";
            if (r.target != null && r.target != r.source) line += $" on {r.target.unitName}";
            if (r.wasCritMiss) line += " | 💥 CRIT MISS (1 self-dmg)";
            if (r.didSave)
            {
                string verdict = r.saveSucceeded ? "🛡 RESISTED" : "FAILED SAVE";
                line += $" | {verdict} (rolled {r.saveRawD20}→{r.saveTotal} vs DC {r.saveDC})";
            }
            if (r.damageDealt > 0) line += $" | dmg {r.damageDealt}";
            if (r.healingDone > 0) line += $" | heal +{r.healingDone}";
            if (r.defenseGained > 0) line += $" | shields +{r.defenseGained}";
            if (r.wasDodged) line += " | DODGED";
            if (r.spSpent > 0) line += $" | SP -{r.spSpent}";
            Debug.Log(line);
        }
        private void HandlePlayerHit(Unit u, int dmg)
        {
            if (logHits) Debug.Log($"[Combat] ✦ {u.unitName} took {dmg} → HP {u.currentHP}/{u.maxHP}");
        }
        private void HandleEnemyDeath(Unit u)
        {
            if (logDeaths) Debug.Log($"[Combat] ☠ {u.unitName} defeated");
        }
        private void HandleActionStateChanged(Unit u)
        {
            if (logActionState) Debug.Log($"[Combat] · {u.unitName} action state changed");
        }
    }
}
