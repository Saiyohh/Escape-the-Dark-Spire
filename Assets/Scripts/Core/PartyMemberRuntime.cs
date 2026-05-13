using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class PartyMemberRuntime
    {
        public CharacterData characterData;
        public int currentHP;
        public int currentSP;

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

        public void CaptureFrom(Unit unit)
        {
            if (unit == null) return;
            currentHP = unit.currentHP;
            currentSP = unit.currentSP;
        }

        public void ApplyTo(Unit unit)
        {
            if (unit == null) return;
            unit.currentHP = Mathf.Clamp(currentHP, 0, unit.maxHP);
            unit.currentSP = Mathf.Clamp(currentSP, 0, unit.maxSP);
        }
    }
}
