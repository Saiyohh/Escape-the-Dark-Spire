using UnityEngine;
using UnityEngine.Serialization;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "DarkSpire/Character")]
    public class CharacterData : ScriptableObject
    {
        // owns section layout and its DrawColoredSectionHeader strips play the
        // same role. Leaving [Header] in would render duplicate inline labels and
        // misalign paired fields (e.g. HP/SP) inside BeginHorizontal rows.

        // ── Identity ─────────────────────────────────────────────────────────
        public string characterName;
        [TextArea] public string description;

        [Tooltip("Small round icon used on portraits, initiative order, callouts. " +
                 "Replaces the old Portrait Front / Back pair.")]
        [FormerlySerializedAs("portraitFront")]
        public Sprite headIcon;

        public Sprite combatSprite;
        public Alignment alignment;

        // ── Base Stats ───────────────────────────────────────────────────────
        public int maxHP;
        public int maxSP;
        [Tooltip("Damage bonus added to attacks.")] public int pow;
        [Tooltip("Attack-roll bonus (D20 + DEX + weapon.attackBonus vs target DEF). " +
                 "Also feeds initiative for players.")] public int dex;
        [Tooltip("Attack-roll target; enemies need total ≥ DEF to hit.")] public int def;
        [Tooltip("Save-roll bonus; rolled when Afflicted (D20 + WIL vs 10 + caster WIL).")] public int wil;

        // ── Display ──────────────────────────────────────────────────────────
        [Tooltip("Per-character UnitDisplay prefab with custom hitbox, indicator anchor, etc.\n" +
                 "If null, CombatManager falls back to its default prefab.")]
        public GameObject displayPrefab;

        [Tooltip("Scale applied to combatSprite when rendered inside the in-combat " +
                 "party tray mask. Default 3 fits the canonical sprite size to a 140×80 cell.")]
        public float partyTrayPortraitScale = 3f;

        [Tooltip("Pixel offset (in mask-local space) applied to combatSprite inside the " +
                 "party tray mask. Use to reframe characters whose sprite isn't centered on their head.")]
        public Vector2 partyTrayPortraitOffset = Vector2.zero;

        // ── Palette ──────────────────────────────────────────────────────────
        [Tooltip("Signature color — darker / fully saturated. Used for callout text " +
                 "backgrounds, name plates, damage number tints, section strips in " +
                 "the inspector. Auto-seeded from Alignment on first edit; override " +
                 "freely per character.")]
        public Color signatureColor = Color.white;

        [Tooltip("Highlight color — lighter variant of the signature. Used for " +
                 "background fills, selection glows, card tints. Auto-seeded " +
                 "from Alignment on first edit.")]
        public Color highlightColor = Color.white;

        // ── Starter Loadout ──────────────────────────────────────────────────
        public WeaponData startingWeapon;
        public SkillData[] startingSkills; // 2 starters

        [Tooltip("Items this character starts the run with in their personal " +
                 "Pouch. Seeded into PartyMemberRuntime.pouchItems by " +
                 "FromCharacter. Items here should be authored with " +
                 "Category=Pouch and Owner=this character. Leave empty for " +
                 "characters with no starting pouch items.")]
        public ItemID[] startingPouch;
        // PORT STUB: startingRelic (RelicData) removed — relics not ported.
        //            Aspect Tree will live on a separate progression SO.

        // ── 5th Pool Flags ───────────────────────────────────────────────────
        public bool hasOrbSystem;
        public bool hasSpellbook;
        public bool hasPotionSack;
        public bool hasStanceSystem;

        [Tooltip("Defect's Cracked Core: orb channeled at start of combat. " +
                 "Leave null on non-Defect characters.")]
        public OrbDataSO startingOrb;

        [Tooltip("Regent's Divine Right: Stars granted at start of combat. " +
                 "Set to 3 on the Regent's CharacterData; leave 0 elsewhere.")]
        [Min(0)] public int startingStars = 0;

        // ── Action Refusal Voice Lines ───────────────────────────────────────
        // Per-character speech lines for the action-refusal speech bubble.
        // Leaving an entry blank falls back to the generic line in
        // ActionRefusalMessages.For(reason).
        [TextArea(1, 3)] public string refusalLine_NoAction;
        [TextArea(1, 3)] public string refusalLine_NoFreeAction;
        [TextArea(1, 3)] public string refusalLine_NotEnoughSP;
        [TextArea(1, 3)] public string refusalLine_NotEnoughStars;
        [TextArea(1, 3)] public string refusalLine_Immobilized;

        public string GetRefusalLine(ActionRefusalReason reason)
        {
            switch (reason)
            {
                case ActionRefusalReason.NoAction:        return refusalLine_NoAction;
                case ActionRefusalReason.NoFreeAction:    return refusalLine_NoFreeAction;
                case ActionRefusalReason.NotEnoughSP:     return refusalLine_NotEnoughSP;
                case ActionRefusalReason.NotEnoughStars:  return refusalLine_NotEnoughStars;
                case ActionRefusalReason.Immobilized:     return refusalLine_Immobilized;
                default:                                  return null;
            }
        }

        // ── Progression ──────────────────────────────────────────────────────
        public int startingLevel = 1;
        public int hpPerLevel;
        // PORT STUB: skillPool (SkillPoolData) removed — Dark Spire rebuilds
        //            this as the skill-draft pool in Pass 5.

    }
}
