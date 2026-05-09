using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Runtime, per-run state for one party member. Lives on RunContext.partyState
    // so HP and SP carry across the dungeon ↔ combat scene swaps (Unit instances
    // are re-created each fight; this record is what survives).
    //
    // SCOPE: HP, SP, and pouch items. Stars are intentionally excluded —
    // they're a per-combat resource granted by Divine Right at combat start
    // (handled in CombatManager.InitializeCombat) and should NOT persist
    // between fights. Conditions, orbs, etc. are also out of scope.
    //
    // Pouch items are character-locked (Notion Category=Pouch) — only the
    // owning character can use them. Shared party items live on
    // RunContext.partyInventory, not here.
    public class PartyMemberRuntime
    {
        public CharacterData characterData;
        public int currentHP;
        public int currentSP;

        // Character-locked pouch — only this party member can use these items.
        // Seeded from CharacterData.startingPouch in FromCharacter.
        public List<ItemInstance> pouchItems = new();

        public static PartyMemberRuntime FromCharacter(CharacterData cd)
        {
            var runtime = new PartyMemberRuntime
            {
                characterData = cd,
                currentHP     = cd != null ? cd.maxHP : 0,
                currentSP     = cd != null ? cd.maxSP : 0,
            };

            if (cd != null && cd.startingPouch != null)
            {
                for (int i = 0; i < cd.startingPouch.Length; i++)
                {
                    var id = cd.startingPouch[i];
                    if (id == ItemID.None) continue;
                    runtime.pouchItems.Add(new ItemInstance(id, 1));
                }
            }

            return runtime;
        }

        // Read live values off a Unit at end-of-combat.
        public void CaptureFrom(Unit unit)
        {
            if (unit == null) return;
            currentHP = unit.currentHP;
            currentSP = unit.currentSP;
        }

        // Push persisted values onto a freshly-constructed Unit at start-of-combat.
        // Clamps against the unit's actual max stats in case CharacterData was
        // edited between fights.
        public void ApplyTo(Unit unit)
        {
            if (unit == null) return;
            unit.currentHP = Mathf.Clamp(currentHP, 0, unit.maxHP);
            unit.currentSP = Mathf.Clamp(currentSP, 0, unit.maxSP);
        }
    }
}
