// AttackChance.cs
// -----------------------------------------------------------------------------
// Pure math helpers for "what % chance does this attacker have to land this
// effect on this target" — shared by every UI that previews a die roll
// (ChanceBox during targeting, DangerPreviewController on intent hover, future
// inline tooltips). Mirrors DiceRoller.AttackRoll / DiceRoller.SaveRoll
// exactly so previews can't drift from the dice.
//
// Conventions:
//   • Attack hit: d20 + attackerATK >= targetDEF (ties hit), nat-1 auto-miss.
//     successFaces = 21 - max(2, def - atk), clamped to [0.05, 0.95].
//   • Save: d20 + targetWIL >= effectiveDC; nat-1 fails, nat-20 succeeds.
//     successFaces = 21 - max(2, dc - wil), clamped to [0.05, 0.95].
//   • Afflict land % = 1 - save success %.
//
// Example: DEX 4 vs DEF 10 → needed = 6 → 15/20 = 75% hit.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    public static class AttackChance
    {
        /// <summary>Probability the attacker's d20 + ATK lands a hit against target.DEF. Mirrors DiceRoller.AttackRoll's >= rule.</summary>
        public static float Hit(Unit attacker, Unit target)
        {
            if (attacker == null || target == null) return 0f;
            int atk = attacker.EffectiveATK;
            int def = target.EffectiveDEF;
            int needed = Mathf.Max(2, def - atk); // ≥2: nat-1 always misses
            int successFaces = 21 - needed;
            return Mathf.Clamp(successFaces / 20f, 0.05f, 0.95f);
        }

        /// <summary>
        /// Probability the target SUCCEEDS its WIL save against caster's DC.
        /// DC defaults to <c>10 + casterWIL</c> when <paramref name="saveDC"/> is 0
        /// (matches DiceRoller.SaveRoll's default).
        /// </summary>
        public static float Save(Unit caster, Unit target, int saveDC)
        {
            if (caster == null || target == null) return 1f;
            int casterWIL = caster.EffectiveWIL;
            int targetWIL = target.EffectiveWIL;
            int dc = saveDC > 0 ? saveDC : 10 + casterWIL;
            int needed = Mathf.Max(2, dc - targetWIL);
            int successFaces = 21 - needed;
            return Mathf.Clamp(successFaces / 20f, 0.05f, 0.95f);
        }

        /// <summary>Probability an afflict lands (i.e. the target FAILS its save).</summary>
        public static float Afflict(Unit caster, Unit target, int saveDC) =>
            1f - Save(caster, target, saveDC);
    }
}
