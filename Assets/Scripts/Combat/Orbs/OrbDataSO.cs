// OrbDataSO.cs
// -----------------------------------------------------------------------------
// One ScriptableObject per orb type (Lightning, Frost, Dark, Light, Plasma,
// Glass). Holds the metadata used by the HUD and runtime: orb type tag, tier,
// presentation (icon, vfxColor), and the base magnitudes that OrbManager reads
// when triggering Passive (per-turn) and Evoke (consume) effects.
//
// Effect *behavior* per orb type lives in OrbManager (a switch on OrbType),
// not on this SO — Lightning hits a random enemy, Dark stores damage on the
// instance, Glass debuffs itself per Passive, etc. Encoding that diversity
// into a single field set on this SO would either lose information or grow
// unwieldy; the switch is direct and easy to read.
//
// Magnitudes are *base* values. OrbManager adds the Defect's Focus stacks at
// resolution time via FocusBoost(). See the canonical Defect GDD page for the
// per-orb table.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "Orb_New", menuName = "DarkSpire/Orb Data")]
    public class OrbDataSO : ScriptableObject
    {
        [Header("Identity")]
        public OrbType orbType = OrbType.Lightning;
        public string displayName = "Lightning";

        [Tooltip("1 = free via Orb Strike (Lightning, Frost). 2 = skill-gated " +
                 "(Dark, Light). 3 = skill-gated, highest cost (Plasma, Glass).")]
        [Range(1, 3)] public int tier = 1;

        [Header("Presentation")]
        public Sprite icon;
        public Color vfxColor = Color.white;

        [Header("Starting stacks (Dark, Light, Glass)")]
        [Tooltip("Initial stacks set when this orb is channeled. Per the canonical " +
                 "spec: Dark = 4, Light = 2, Glass = 4. Lightning / Frost / Plasma = 0.")]
        [Min(0)] public int startingStacks = 0;

        [Tooltip("When true, this orb's Passive fires at the bearer's turn START " +
                 "(before the player chooses their action) instead of at turn end. " +
                 "Plasma is the only orb that uses this — its D20 Passive needs to " +
                 "resolve before the player acts so the extra-action grant lands in " +
                 "time to be used. Leave false for every other orb.")]
        public bool passiveAtTurnStart = false;

        [Header("Base magnitudes (Focus adds at resolve time except Plasma)")]
        [Tooltip("Lightning Passive: damage to a random enemy. (3)\n" +
                 "Frost Passive: Shields gained. (3)\n" +
                 "Dark Passive: stacks gained per tick. (4)\n" +
                 "Light Passive: stacks gained per tick. (2)\n" +
                 "Plasma Passive: ignored — D20 ≥ 11 grants extra action; Focus N/A.\n" +
                 "Glass Passive: ignored — damage = max(stacks,0) + Focus.")]
        [Min(0)] public int passiveBaseMagnitude = 0;

        [Tooltip("Lightning Evoke: damage to a random enemy. (8)\n" +
                 "Frost Evoke: Shields gained. (8)\n" +
                 "Dark Evoke: ignored — damage = stacks + Focus to a random enemy.\n" +
                 "Light Evoke: ignored — heal = stacks + Focus.\n" +
                 "Plasma Evoke: ignored — grants extra action; Focus N/A.\n" +
                 "Glass Evoke: ignored — damage = (max(stacks,0) + Focus) × 2.5 to all.")]
        [Min(0)] public int evokeBaseMagnitude = 0;

        [Header("Description (HUD tooltip)")]
        [TextArea(2, 4)] public string passiveDescription;
        [TextArea(2, 4)] public string evokeDescription;
    }
}
