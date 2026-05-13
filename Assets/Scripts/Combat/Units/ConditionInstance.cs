// ConditionInstance.cs
// -----------------------------------------------------------------------------
// Runtime pairing of a ConditionData SO with mutable state (stacks, duration).
// Lives inside ConditionManager's dictionary on each Unit.
//
// Stack behavior depends on data.stackType:
//   Counter  → stacks accumulate, duration unused
//   Duration → duration refreshes to max(current, applied), stacks fixed at 1
//   Single   → one stack, duration counts down turns
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    [System.Serializable]
    public class ConditionInstance
    {
        public ConditionData data;
        public int stacks;
        public int duration;

        // The unit that applied this instance, if known. Set by ConditionManager.ApplyCondition.
        // Used by caster-gated conditions (Shrink) to self-clear when the source dies.
        // Non-serialized — runtime-only reference, must be re-established each combat.
        [System.NonSerialized] public Unit sourceUnit;

        public ConditionInstance(ConditionData data, int amount)
        {
            this.data = data;
            switch (data.stackType)
            {
                case ConditionStackType.Counter:
                    stacks = amount;
                    duration = -1;
                    break;
                case ConditionStackType.Duration:
                    stacks = 1;
                    duration = amount;
                    break;
                case ConditionStackType.Single:
                    stacks = 1;
                    duration = amount > 0 ? amount : 1;
                    break;
            }
        }

        public void Apply(int amount)
        {
            switch (data.stackType)
            {
                case ConditionStackType.Counter:
                    stacks += amount;
                    if (data.maxStacks > 0 && stacks > data.maxStacks)
                        stacks = data.maxStacks;
                    break;
                case ConditionStackType.Duration:
                    if (amount > duration)
                        duration = amount;
                    break;
                case ConditionStackType.Single:
                    stacks = 1;
                    if (amount > duration)
                        duration = amount;
                    break;
            }
        }

        public bool TickDuration()
        {
            if (data.stackType == ConditionStackType.Duration ||
                data.stackType == ConditionStackType.Single)
            {
                duration--;
                return duration <= 0;
            }
            return false;
        }

        public bool TickStacks()
        {
            if (data.stackType == ConditionStackType.Counter && data.ticksDown)
            {
                stacks--;
                return stacks <= 0;
            }
            return false;
        }

        public int GetDisplayValue()
        {
            return data.stackType == ConditionStackType.Counter ? stacks : duration;
        }
    }
}
