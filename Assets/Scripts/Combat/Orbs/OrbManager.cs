// OrbManager.cs
// -----------------------------------------------------------------------------
// Orchestrates the Defect's orb queue. Static helper (no Unity component) —
// mirrors RankHelper's style: stateless functions that read/mutate state on
// a Unit + the live combat lineup.
//
// Slot convention:
//   • Slots are FIXED visual positions: slot 0 = far RIGHT, slot N-1 = far
//     LEFT. Slot 0 is the "evoke chamber" — Evoke always targets it.
//   • orbs[i] always renders at slot i. The list is packed from index 0,
//     so orbs[0] is the OLDEST orb (next to Evoke) and orbs[count-1] is
//     the most recently channeled.
//   • Channel = "place into the rightmost empty slot." Since the list is
//     packed from slot 0 leftward, the rightmost empty slot is always at
//     index orbs.Count, which is exactly where List.Add lands.
//   • Evoke (slot 0) removes orbs[0]; the remaining orbs shift one slot
//     to the right (orbs[1] → orbs[0], etc.) by virtue of List.RemoveAt(0).
//     The freshly emptied position is always on the LEFT (slot count-1).
//   • Channeling into a full tray (orbs.Count == orbSlotMax) auto-evokes
//     orbs[0] first (still slot 0, still the rightmost), opening up slot
//     count-1 on the left for the new orb.
//   • At the BEARER'S TURN START: orbs flagged passiveAtTurnStart fire their
//     Passive (Plasma — its D20 extra-action roll resolves before the player
//     picks their action). Per-turn so the bearer benefits directly.
//   • At PLAYER PHASE END (after every player has acted): orbs WITHOUT the
//     start-of-turn flag fire their Passive. NO automatic Evoke at phase
//     end — orbs only Evoke from explicit skill effects (Dualcast,
//     evoke-tagged skills, etc.). Channeling into a full tray drops the
//     oldest orb silently rather than auto-evoking.
//
// Spec source: "Defect: 6 Orb Types (3 Tiers)" page in the Characters DB.
// Plasma's extra-action grant is stubbed (Debug.Log) — implementing it
// requires extending the action queue, which is out of scope for this pass.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class OrbManager
    {
        // Glass Evoke multiplier is hardcoded here (not on OrbDataSO) because
        // it's a Glass-specific shape, not a tunable per-asset value.
        private const float GlassEvokeMultiplier = 2.5f;

        // Plasma Passive D20 threshold for the extra-action grant.
        private const int PlasmaPassiveD20Threshold = 11;

        // ─── Public API ──────────────────────────────────────────────────────

        public static void Channel(Unit defect, OrbDataSO data)
        {
            if (defect == null || data == null) return;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) return;
            if (defect.orbs == null) defect.orbs = new List<OrbInstance>();

            // Tray full: drop the oldest (slot 0) silently. Per the new rule
            // orbs only Evoke from explicit skill effects, so overflow does
            // NOT trigger an auto-evoke. The discarded orb's effect is lost.
            if (defect.orbs.Count >= defect.orbSlotMax && defect.orbSlotMax > 0)
                defect.orbs.RemoveAt(0);

            defect.orbs.Add(new OrbInstance(data));
            defect.RaiseOrbsChanged();
        }

        public static void EvokeFirst(Unit defect, bool suppressOrbsChanged = false)
        {
            EvokeFirstRepeated(defect, 1, suppressOrbsChanged);
        }

        /// <summary>
        /// Fire the rightmost orb's Evoke <paramref name="times"/> times AS THE
        /// SAME ORB (Dualcast pattern). The orb is removed from the slot only
        /// once, after every repetition has resolved — so even with a single
        /// orb in the tray, Dualcast still produces both effects. Glass
        /// follows its normal rule and never consumes regardless of count.
        ///
        /// Note that re-firing the same orb means stack-driven orbs (Dark,
        /// Light) deal/heal the same amount each repetition (their stacks
        /// don't reset between calls). Glass zeroes its stacks on the first
        /// fire, so subsequent fires only deliver Focus damage.
        /// </summary>
        public static void EvokeFirstRepeated(Unit defect, int times, bool suppressOrbsChanged = false)
        {
            if (defect == null || defect.orbs == null || defect.orbs.Count == 0) return;
            if (times <= 0) return;

            var orb = defect.orbs[0];
            bool consume = false;
            for (int i = 0; i < times; i++)
                consume = TriggerEvoke(defect, orb);

            if (consume) defect.orbs.RemoveAt(0);
            if (!suppressOrbsChanged) defect.RaiseOrbsChanged();
        }

        public static void EvokeRightmost(Unit defect)
        {
            if (defect == null || defect.orbs == null || defect.orbs.Count == 0) return;
            int idx = defect.orbs.Count - 1;
            var orb = defect.orbs[idx];
            bool consume = TriggerEvoke(defect, orb);
            if (consume) defect.orbs.RemoveAt(idx);
            defect.RaiseOrbsChanged();
        }

        public static void EvokeAll(Unit defect)
        {
            if (defect == null || defect.orbs == null || defect.orbs.Count == 0) return;
            // Walk a snapshot so cascading effects can't re-trigger; rebuild the
            // list with any non-consumed orbs (Glass) preserving order.
            var snapshot = new List<OrbInstance>(defect.orbs);
            var keep = new List<OrbInstance>();
            foreach (var orb in snapshot)
            {
                bool consume = TriggerEvoke(defect, orb);
                if (!consume) keep.Add(orb);
            }
            defect.orbs.Clear();
            defect.orbs.AddRange(keep);
            defect.RaiseOrbsChanged();
        }

        /// <summary>
        /// Start-of-turn pass: orbs flagged passiveAtTurnStart fire their
        /// Passive before the player picks an action. Currently Plasma only —
        /// its D20 extra-action roll has to resolve in time for the player to
        /// use the bonus action.
        /// </summary>
        public static void OnTurnStart(Unit defect)
        {
            if (defect == null || defect.orbs == null) return;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) return;

            foreach (var orb in defect.orbs)
            {
                if (orb == null || orb.data == null) continue;
                if (orb.data.passiveAtTurnStart)
                    TriggerPassive(defect, orb);
            }
        }

        /// <summary>
        /// End-of-player-phase pass: orbs WITHOUT the start-of-turn flag fire
        /// their Passive — and that's it. Slot 0 does NOT auto-evoke at
        /// phase end; orbs only ever Evoke from explicit skill effects.
        ///
        /// Per-orb passive triggers run with a short interval (driven by
        /// CombatManager's phase-end coroutine) so the player can see each
        /// individual passive resolve. This method is the synchronous
        /// fallback; OnPhaseEndCoroutine is the preferred entry point.
        /// </summary>
        public static void OnPhaseEnd(Unit defect)
        {
            if (defect == null || defect.orbs == null) return;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) return;

            foreach (var orb in defect.orbs)
            {
                if (orb == null || orb.data == null) continue;
                if (orb.data.passiveAtTurnStart) continue;
                TriggerPassive(defect, orb);
                CombatEvents.InvokeOrbPassiveTriggered(defect, orb);
            }
            defect.RaiseOrbsChanged();
        }

        /// <summary>
        /// Coroutine variant of OnPhaseEnd that yields between each orb's
        /// passive trigger so the player can see them resolve one at a time.
        /// VFX/animation hook fires per orb via CombatEvents.OnOrbPassiveTriggered;
        /// OrbSlotsUI subscribes to play a flash on the corresponding slot.
        /// </summary>
        public static System.Collections.IEnumerator OnPhaseEndCoroutine(
            Unit defect, float perOrbInterval = 0.45f)
        {
            if (defect == null || defect.orbs == null) yield break;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) yield break;

            // Snapshot so cascading effects (e.g. an orb passive that channels
            // another orb mid-sweep) don't extend this loop unbounded.
            var snapshot = new List<OrbInstance>(defect.orbs);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var orb = snapshot[i];
                if (orb == null || orb.data == null) continue;
                if (orb.data.passiveAtTurnStart) continue;

                TriggerPassive(defect, orb);
                CombatEvents.InvokeOrbPassiveTriggered(defect, orb);
                defect.RaiseOrbsChanged();

                // Pause between orbs so the player reads the resolution.
                if (perOrbInterval > 0f && i < snapshot.Count - 1)
                    yield return new WaitForSeconds(perOrbInterval);
            }
        }

        public static void ClearAll(Unit defect)
        {
            if (defect == null || defect.orbs == null) return;
            if (defect.orbs.Count == 0) return;
            defect.orbs.Clear();
            defect.RaiseOrbsChanged();
        }

        // ─── Effect triggers (per orb type) ──────────────────────────────────

        private static void TriggerPassive(Unit defect, OrbInstance orb)
        {
            if (orb == null || orb.data == null) return;

            switch (orb.data.orbType)
            {
                case OrbType.Lightning:
                {
                    int amt = FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    var target = PickRandomLivingEnemy(defect);
                    if (target != null) target.TakeDamage(amt, defect);
                    break;
                }
                case OrbType.Frost:
                {
                    int amt = FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    ApplyShields(defect, amt);
                    break;
                }
                case OrbType.Dark:
                {
                    // Gain (passiveBase + Focus) stacks per tick.
                    orb.stacks += FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    break;
                }
                case OrbType.Light:
                {
                    // Gain (passiveBase + Focus) stacks per tick.
                    orb.stacks += FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    break;
                }
                case OrbType.Plasma:
                {
                    // Roll D20 ≥ 11 → extra action this turn. Focus does NOT apply.
                    int roll = Random.Range(1, 21);
                    if (roll >= PlasmaPassiveD20Threshold)
                        GrantExtraAction(defect, "Plasma Passive");
                    break;
                }
                case OrbType.Glass:
                {
                    // Damage all enemies for max(stacks, 0) + Focus, then -1
                    // stack (clamped at 0).
                    int baseAmt = Mathf.Max(0, orb.stacks);
                    int dmg = FocusBoost(defect, baseAmt);
                    if (dmg > 0)
                        foreach (var e in GetLivingEnemies(defect))
                            e.TakeDamage(dmg, defect);
                    orb.stacks = Mathf.Max(0, orb.stacks - 1);
                    break;
                }
            }
        }

        /// <summary>
        /// Returns true if the orb is consumed (removed from the slot lineup).
        /// Glass returns false — it stays in the slot at 0 stacks per the spec.
        /// </summary>
        private static bool TriggerEvoke(Unit defect, OrbInstance orb)
        {
            if (orb == null || orb.data == null) return true;

            switch (orb.data.orbType)
            {
                case OrbType.Lightning:
                {
                    int amt = FocusBoost(defect, orb.data.evokeBaseMagnitude);
                    var target = PickRandomLivingEnemy(defect);
                    if (target != null) target.TakeDamage(amt, defect);
                    return true;
                }
                case OrbType.Frost:
                {
                    int amt = FocusBoost(defect, orb.data.evokeBaseMagnitude);
                    ApplyShields(defect, amt);
                    return true;
                }
                case OrbType.Dark:
                {
                    // Deal stacks + Focus damage to a random enemy.
                    int amt = FocusBoost(defect, orb.stacks);
                    if (amt > 0)
                    {
                        var target = PickRandomLivingEnemy(defect);
                        if (target != null) target.TakeDamage(amt, defect);
                    }
                    return true;
                }
                case OrbType.Light:
                {
                    // Heal lowest-HP ally (excluding Defect) for stacks + Focus.
                    // Heal Defect for half rounded down. If no other living
                    // ally below max HP, Defect gets (stacks + Focus) × 1.5
                    // rounded down.
                    int amt = FocusBoost(defect, orb.stacks);
                    if (amt > 0) ResolveLightHeal(defect, amt);
                    return true;
                }
                case OrbType.Plasma:
                {
                    // Guaranteed extra action. Focus does NOT apply.
                    GrantExtraAction(defect, "Plasma Evoke");
                    return true;
                }
                case OrbType.Glass:
                {
                    // Damage all for (max(stacks, 0) + Focus) × 2.5, then 0
                    // stacks. Orb STAYS in slot — return false so the caller
                    // doesn't remove it.
                    int baseAmt = Mathf.Max(0, orb.stacks);
                    int boosted = FocusBoost(defect, baseAmt);
                    int dmg = Mathf.FloorToInt(boosted * GlassEvokeMultiplier);
                    if (dmg > 0)
                        foreach (var e in GetLivingEnemies(defect))
                            e.TakeDamage(dmg, defect);
                    orb.stacks = 0;
                    return false;
                }
            }
            return true;
        }

        // ─── HUD display values ──────────────────────────────────────────────

        /// <summary>
        /// What to render as the orb's "passive number" on the HUD — i.e. what
        /// the next Passive tick would produce given the bearer's current
        /// Focus. Returns 0 when the orb has no meaningful numeric passive
        /// (Plasma's D20 roll). Used by OrbSlotsUI; not on the combat hot
        /// path (TriggerPassive computes the same value inline).
        /// </summary>
        public static int GetPassiveDisplayValue(OrbInstance orb, Unit bearer)
        {
            if (orb == null || orb.data == null) return 0;
            switch (orb.data.orbType)
            {
                case OrbType.Lightning:
                case OrbType.Frost:
                case OrbType.Dark:
                case OrbType.Light:
                    // Static formula: passiveBase + Focus.
                    return FocusBoost(bearer, orb.data.passiveBaseMagnitude);
                case OrbType.Glass:
                    // Damage per tick = max(stacks, 0) + Focus.
                    return FocusBoost(bearer, Mathf.Max(0, orb.stacks));
                case OrbType.Plasma:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// What to render as the orb's "active number" — what an Evoke right
        /// now would produce. Surfaced on the icon for Dark (per the GDD)
        /// and via the hover tooltip for other orbs.
        /// </summary>
        public static int GetActiveDisplayValue(OrbInstance orb, Unit bearer)
        {
            if (orb == null || orb.data == null) return 0;
            switch (orb.data.orbType)
            {
                case OrbType.Lightning:
                case OrbType.Frost:
                    return FocusBoost(bearer, orb.data.evokeBaseMagnitude);
                case OrbType.Dark:
                case OrbType.Light:
                    // Stacks-driven payoff: stacks + Focus.
                    return FocusBoost(bearer, orb.stacks);
                case OrbType.Glass:
                    // (max(stacks, 0) + Focus) × 2.5, floored.
                    return Mathf.FloorToInt(
                        FocusBoost(bearer, Mathf.Max(0, orb.stacks)) * GlassEvokeMultiplier);
                case OrbType.Plasma:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// True when the active value should be drawn on the orb icon at all
        /// times (Dark, per the GDD: "showing both communicates that growth
        /// visibly"). Other orbs hide it on the icon and surface it via the
        /// tooltip on hover.
        /// </summary>
        public static bool ShouldShowActiveOnIcon(OrbInstance orb)
        {
            if (orb == null || orb.data == null) return false;
            return orb.data.orbType == OrbType.Dark;
        }

        // ─── Focus scaling ───────────────────────────────────────────────────

        public static int FocusBoost(Unit defect, int baseValue)
        {
            if (defect == null) return baseValue;
            int focus = defect.conditions != null
                ? defect.conditions.GetStacks(ConditionID.Focus)
                : 0;
            return baseValue + focus;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static List<Unit> GetLivingEnemies(Unit defect)
        {
            var mgr = CombatManager.Instance;
            var result = new List<Unit>();
            if (mgr == null) return result;
            bool defectIsPlayer = defect.isPlayerControlled;
            var enemies = defectIsPlayer ? mgr.EnemyUnits : mgr.PlayerUnits;
            foreach (var u in enemies)
                if (u != null && u.IsAlive) result.Add(u);
            return result;
        }

        private static List<Unit> GetLivingAlliesExcludingSelf(Unit defect)
        {
            var mgr = CombatManager.Instance;
            var result = new List<Unit>();
            if (mgr == null) return result;
            var allies = defect.isPlayerControlled ? mgr.PlayerUnits : mgr.EnemyUnits;
            foreach (var u in allies)
                if (u != null && u != defect && u.IsAlive) result.Add(u);
            return result;
        }

        private static Unit PickRandomLivingEnemy(Unit defect)
        {
            var living = GetLivingEnemies(defect);
            if (living.Count == 0) return null;
            return living[Random.Range(0, living.Count)];
        }

        private static void ResolveLightHeal(Unit defect, int amt)
        {
            // Find lowest-HP ally (excluding Defect) below max HP.
            Unit best = null;
            int bestHp = int.MaxValue;
            foreach (var u in GetLivingAlliesExcludingSelf(defect))
            {
                if (u.currentHP >= u.maxHP) continue; // at full
                if (u.currentHP < bestHp)
                {
                    bestHp = u.currentHP;
                    best = u;
                }
            }

            if (best != null)
            {
                best.Heal(amt);
                // Defect also heals for half, rounded down.
                int selfShare = amt / 2;
                if (selfShare > 0) defect.Heal(selfShare);
            }
            else
            {
                // No valid ally → Defect gets 1.5x rounded down.
                int selfFull = Mathf.FloorToInt(amt * 1.5f);
                if (selfFull > 0) defect.Heal(selfFull);
            }
        }

        private static void ApplyShields(Unit unit, int stacks)
        {
            if (unit == null || stacks <= 0) return;
            if (ConditionLibrary.Instance == null) return;
            var data = ConditionLibrary.Instance.Get(ConditionID.Shields);
            if (data == null) return;
            unit.conditions.ApplyCondition(data, stacks, unit);
        }

        /// <summary>
        /// Plasma's extra-action grant. Implementing this end-to-end requires
        /// extending the turn-action queue so the Defect can act again before
        /// the turn finalizes. Out of scope for this pass — log the intent so
        /// it surfaces during playtest, then wire to the action queue when
        /// that infrastructure is in place.
        /// </summary>
        private static void GrantExtraAction(Unit defect, string source)
        {
            // TODO: route to the action-queue extension when available.
            Debug.Log($"[OrbManager] Extra action granted to {defect?.unitName} via {source} (queue plumbing pending).");
        }
    }
}
