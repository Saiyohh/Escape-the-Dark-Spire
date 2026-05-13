using UnityEngine;

namespace DarkSpire
{
    public static class DiceRoller
    {
        public static int RollD20() => Random.Range(1, 21);
        public static int RollD8() => Random.Range(1, 9);
        public static int Roll(int sides) => Random.Range(1, sides + 1);

        public static int RollInitiative(int agiModifier) => RollD8() + agiModifier;

        public static (bool hit, bool crit, int rawRoll, int totalRoll) AttackRoll(
            int attackBonus, int targetDEF, int critThreshold = 20)
        {
            int raw = RollD20();
            int total = raw + attackBonus;
            bool crit = raw >= critThreshold;
            bool hit = crit || total >= targetDEF;
            return (hit, crit, raw, total);
        }

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
