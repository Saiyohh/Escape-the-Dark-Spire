// CombatActionResult.cs
// -----------------------------------------------------------------------------
// Data object returned by SkillResolver / EnemyAI after an action resolves.
// Carries everything the UI + event system needs to render the outcome:
// who did what to whom, the dice roll, damage, healing, conditions applied.
// CombatManager passes it into CombatEvents.InvokeActionResolved.
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace DarkSpire
{
    public class CombatActionResult
    {
        public Unit source;
        public Unit target;
        public ActionType actionType;
        public string actionName;

        // Attack roll info
        public bool didRoll;
        public bool didHit;
        public bool wasCrit;
        public bool wasCritMiss;   // Natural 1 on a weapon/skill attack → 1 self-damage
        public bool wasDodged;
        public int rawD20Roll;
        public int totalAttackRoll;
        public int targetDEF;

        // WIL save info (Pass 2.C). Populated whenever the target rolled a save
        // against this action — either because the skill's diceRule == WilSave
        // or because a Move effect carried saveDC > 0. didSave = false means
        // no save was attempted; read the other fields only when didSave = true.
        public bool didSave;
        public bool saveSucceeded; // true = target resisted the effect
        public int saveRawD20;
        public int saveTotal;
        public int saveDC;

        // Outcomes
        public int damageDealt;
        public int healingDone;
        public int defenseGained;
        public List<(ConditionID condition, int stacks)> conditionsApplied = new();

        // SP cost
        public int spSpent;
    }
}
