// DiceRoller.cs
// -----------------------------------------------------------------------------
// All RNG rolls used by combat go through here: d20 attack rolls, d8
// initiative, generic d-any. Pure static utility — no state, no events.
//
// Nat-1 crit miss (attack) and nat-1 auto-fail / nat-20 auto-succeed (save)
// are handled here and by SkillResolver. The hot-path tuples stay narrow —
// callers detect the natural-die edge cases themselves.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public static class DiceRoller
    {
        public static int RollD20() => Random.Range(1, 21);
        public static int RollD8() => Random.Range(1, 9);
        public static int Roll(int sides) => Random.Range(1, sides + 1);

        public static int RollInitiative(int agiModifier) => RollD8() + agiModifier;

        /// <summary>
        /// Attack roll: d20 + attackBonus vs targetDEF.
        /// Returns (hit, crit, rawD20, totalRoll).
        /// Crit: raw d20 >= critThreshold (default 20). Crits always hit.
        /// Nat-1 is NOT flagged here — callers detect `rawRoll == 1` to apply
        /// crit-miss self-damage (keeps this tuple narrow for hot-path use).
        /// </summary>
        public static (bool hit, bool crit, int rawRoll, int totalRoll) AttackRoll(
            int attackBonus, int targetDEF, int critThreshold = 20)
        {
            int raw = RollD20();
            int total = raw + attackBonus;
            bool crit = raw >= critThreshold;
            bool hit = crit || total >= targetDEF;
            return (hit, crit, raw, total);
        }

        /// <summary>
        /// WIL save roll: target rolls d20 + targetWIL vs DC. When <paramref name="dc"/>
        /// is 0, the default DC of <c>10 + casterWIL</c> applies (GDD default).
        /// Pass an explicit dc to override for specific skills.
        ///
        /// Natural-die edge cases (D&D convention, explicit on purpose):
        ///   • Nat-1  → save always fails
        ///   • Nat-20 → save always succeeds
        ///
        /// Returns (saveSucceeded, rawD20, totalRoll). "Success" = the target
        /// resists the effect.
        /// </summary>
        public static (bool success, int rawRoll, int totalRoll) SaveRoll(
            int targetWIL, int casterWIL, int dc = 0)
        {
            int effectiveDC = dc > 0 ? dc : 10 + casterWIL;
            int raw = RollD20();
            int total = raw + targetWIL;

            if (raw == 1)  return (false, raw, total);
            if (raw == 20) return (true,  raw, total);
            return (total >= effectiveDC, raw, total);
        }
    }
}
