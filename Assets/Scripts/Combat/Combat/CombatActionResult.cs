using System.Collections.Generic;

namespace DarkSpire
{
    public class CombatActionResult
    {
        public Unit source;
        public Unit target;
        public ActionType actionType;
        public string actionName;

        public bool didRoll;
        public bool didHit;
        public bool wasCrit;
        public bool wasCritMiss;   // Natural 1 on a weapon/skill attack → 1 self-damage
        public bool wasDodged;
        public int rawD20Roll;
        public int totalAttackRoll;
        public int targetDEF;

        public bool didSave;
        public bool saveSucceeded; // true = target resisted the effect
        public int saveRawD20;
        public int saveTotal;
        public int saveDC;

        public int damageDealt;
        public int healingDone;
        public int defenseGained;
        public List<(ConditionID condition, int stacks)> conditionsApplied = new();

        public int spSpent;
    }
}
