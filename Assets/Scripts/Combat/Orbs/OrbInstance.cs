// OrbInstance.cs
// -----------------------------------------------------------------------------
// One live orb in a Defect's slot lineup. Holds a reference to the immutable
// OrbDataSO that defines its type and adds per-instance state — `stacks`,
// the canonical counter used by Dark (gain on Passive, dump on Evoke),
// Light (gain on Passive, scale heal on Evoke), and Glass (decay on Passive,
// drives AoE damage). Lightning, Frost, and Plasma ignore stacks.
//
// Glass clamps at max(stacks, 0); a hidden floor of 0 keeps the orb in its
// slot after Evoke (Glass is unique in not being consumed) so Focus boosts
// can still produce damage on subsequent Passives.
//
// Slot 0 is the OLDEST orb (the one that Evokes next at turn end) and sits
// at the RIGHT endpoint of the visual arc; new orbs channel into the
// rightmost array index and visually slide in on the LEFT.
//
// Lifetime: created by OrbManager.Channel, removed by Evoke (except Glass)
// or ClearAll. Plain POCO — no UnityEngine.Object overhead in the orb list.
// -----------------------------------------------------------------------------

namespace DarkSpire
{
    public class OrbInstance
    {
        public readonly OrbDataSO data;

        /// <summary>
        /// Per-instance counter. Initialized from OrbDataSO.startingStacks at
        /// channel time. Mutated by Passive/Evoke per orb type:
        ///   Dark   — Passive adds (4+Focus); Evoke deals stacks+Focus.
        ///   Light  — Passive adds (2+Focus); Evoke scales heal by stacks+Focus.
        ///   Glass  — Passive uses max(stacks,0)+Focus then -1; Evoke uses
        ///            same base × 2.5 then sets to 0 (orb stays in slot).
        /// Lightning / Frost / Plasma ignore this field.
        /// </summary>
        public int stacks;

        public OrbInstance(OrbDataSO data)
        {
            this.data = data;
            this.stacks = data != null ? data.startingStacks : 0;
        }

        public OrbType Type => data != null ? data.orbType : OrbType.Lightning;
    }
}
