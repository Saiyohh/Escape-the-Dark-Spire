using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class OrbManager
    {
        private const float GlassEvokeMultiplier = 2.5f;

        private const int PlasmaPassiveD20Threshold = 11;

        public static void Channel(Unit defect, OrbDataSO data)
        {
            if (defect == null || data == null) return;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) return;
            if (defect.orbs == null) defect.orbs = new List<OrbInstance>();

            if (defect.orbs.Count >= defect.orbSlotMax && defect.orbSlotMax > 0)
                defect.orbs.RemoveAt(0);

            defect.orbs.Add(new OrbInstance(data));
            defect.RaiseOrbsChanged();
        }

        public static void EvokeFirst(Unit defect, bool suppressOrbsChanged = false)
        {
            EvokeFirstRepeated(defect, 1, suppressOrbsChanged);
        }

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

        public static void EvokeLeftmost(Unit defect)
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

        public static System.Collections.IEnumerator OnPhaseEndCoroutine(
            Unit defect, float perOrbInterval = 0.45f)
        {
            if (defect == null || defect.orbs == null) yield break;
            if (defect.characterData == null || !defect.characterData.hasOrbSystem) yield break;

            var snapshot = new List<OrbInstance>(defect.orbs);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var orb = snapshot[i];
                if (orb == null || orb.data == null) continue;
                if (orb.data.passiveAtTurnStart) continue;

                TriggerPassive(defect, orb);
                CombatEvents.InvokeOrbPassiveTriggered(defect, orb);
                defect.RaiseOrbsChanged();

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
                    orb.stacks += FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    break;
                }
                case OrbType.Light:
                {
                    orb.stacks += FocusBoost(defect, orb.data.passiveBaseMagnitude);
                    break;
                }
                case OrbType.Plasma:
                {
                    int roll = Random.Range(1, 21);
                    if (roll >= PlasmaPassiveD20Threshold)
                        GrantExtraAction(defect, "Plasma Passive");
                    break;
                }
                case OrbType.Glass:
                {
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
                    int amt = FocusBoost(defect, orb.stacks);
                    if (amt > 0) ResolveLightHeal(defect, amt);
                    return true;
                }
                case OrbType.Plasma:
                {
                    GrantExtraAction(defect, "Plasma Evoke");
                    return true;
                }
                case OrbType.Glass:
                {
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

        public static int GetPassiveDisplayValue(OrbInstance orb, Unit bearer)
        {
            if (orb == null || orb.data == null) return 0;
            switch (orb.data.orbType)
            {
                case OrbType.Lightning:
                case OrbType.Frost:
                case OrbType.Dark:
                case OrbType.Light:
                    return FocusBoost(bearer, orb.data.passiveBaseMagnitude);
                case OrbType.Glass:
                    return FocusBoost(bearer, Mathf.Max(0, orb.stacks));
                case OrbType.Plasma:
                default:
                    return 0;
            }
        }

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
                    return FocusBoost(bearer, orb.stacks);
                case OrbType.Glass:
                    return Mathf.FloorToInt(
                        FocusBoost(bearer, Mathf.Max(0, orb.stacks)) * GlassEvokeMultiplier);
                case OrbType.Plasma:
                default:
                    return 0;
            }
        }

        public static bool ShouldShowActiveOnIcon(OrbInstance orb)
        {
            if (orb == null || orb.data == null) return false;
            return orb.data.orbType == OrbType.Dark;
        }

        public static int FocusBoost(Unit defect, int baseValue)
        {
            if (defect == null) return baseValue;
            int focus = defect.conditions != null
                ? defect.conditions.GetStacks(ConditionID.Focus)
                : 0;
            return baseValue + focus;
        }

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
                int selfShare = amt / 2;
                if (selfShare > 0) defect.Heal(selfShare);
            }
            else
            {
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

        private static void GrantExtraAction(Unit defect, string source)
        {
            Debug.Log($"[OrbManager] Extra action granted to {defect?.unitName} via {source} (queue plumbing pending).");
        }
    }
}
