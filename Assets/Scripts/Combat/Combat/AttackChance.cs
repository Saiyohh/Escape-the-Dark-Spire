using UnityEngine;

namespace DarkSpire
{
    public static class AttackChance
    {
        public static float Hit(Unit attacker, Unit target)
        {
            if (attacker == null || target == null) return 0f;
            int atk = attacker.EffectiveATK;
            int def = target.EffectiveDEF;
            int needed = Mathf.Max(2, def - atk); // ≥2: nat-1 always misses
            int successFaces = 21 - needed;
            return Mathf.Clamp(successFaces / 20f, 0.05f, 0.95f);
        }

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

        public static float Afflict(Unit caster, Unit target, int saveDC) =>
            1f - Save(caster, target, saveDC);
    }
}
