// Inventory.cs
// -----------------------------------------------------------------------------
// Static helper that bridges the persistent inventory (RunContext.partyInventory
// + PartyMemberRuntime.pouchItems) to combat-time consumers (the action menu's
// item submenu, SkillResolver's GenerateItem effect, the dungeon inventory
// panel, etc).
//
// Single point of access for "what items can this unit use right now?" — the
// caller doesn't need to know whether an item is shared (party) or
// character-locked (pouch); they just call GetUsableFor and consume by the
// returned (source, index) pair.
//
// Why a static helper, not a Unit field:
//   • Two sources of truth (party + pouch) merge at use-time
//   • Persistence lives on RunContext / PartyMemberRuntime, not Unit (which is
//     rebuilt every combat) — keeping Unit ignorant avoids re-sync churn
//   • Brewed Potions go straight into partyInventory; no extra plumbing
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class Inventory
    {
        public readonly struct Entry
        {
            public readonly ItemSource source;
            public readonly int index;
            public readonly ItemInstance instance;

            public Entry(ItemSource source, int index, ItemInstance instance)
            {
                this.source = source;
                this.index = index;
                this.instance = instance;
            }
        }

        /// <summary>
        /// Items the given unit can use this turn — this character's pouch
        /// first, then the shared party bag. Enumerated lazily so the caller
        /// can short-circuit if a count check is enough.
        /// </summary>
        public static IEnumerable<Entry> GetUsableFor(Unit unit)
        {
            if (unit == null) yield break;

            // Pouch first — character-locked, owner is this unit.
            if (unit.partyMember != null && unit.partyMember.pouchItems != null)
            {
                var pouch = unit.partyMember.pouchItems;
                for (int i = 0; i < pouch.Count; i++)
                {
                    var inst = pouch[i];
                    if (inst == null) continue;
                    yield return new Entry(ItemSource.Pouch, i, inst);
                }
            }

            // Then shared party inventory.
            var party = RunContext.partyInventory;
            if (party != null)
            {
                for (int i = 0; i < party.Count; i++)
                {
                    var inst = party[i];
                    if (inst == null) continue;
                    yield return new Entry(ItemSource.Party, i, inst);
                }
            }
        }

        /// <summary>
        /// Look up the ItemInstance at the given source+index. Returns null
        /// if the index is out of bounds (e.g. a stale UI reference after
        /// another action consumed the slot).
        /// </summary>
        public static ItemInstance Resolve(Unit user, ItemSource source, int index)
        {
            switch (source)
            {
                case ItemSource.Pouch:
                    if (user == null || user.partyMember == null) return null;
                    var pouch = user.partyMember.pouchItems;
                    if (pouch == null || index < 0 || index >= pouch.Count) return null;
                    return pouch[index];

                case ItemSource.Party:
                    var party = RunContext.partyInventory;
                    if (party == null || index < 0 || index >= party.Count) return null;
                    return party[index];

                default:
                    return null;
            }
        }

        /// <summary>
        /// Consume one charge from the item at source+index. Removes the
        /// instance entirely when charges hit 0. Safe to call after a
        /// non-consumable resolve — caller is expected to gate on
        /// itemData.consumedOnUse before invoking.
        /// </summary>
        public static void Consume(Unit user, ItemSource source, int index)
        {
            var inst = Resolve(user, source, index);
            if (inst == null) return;

            inst.charges = Mathf.Max(0, inst.charges - 1);
            if (inst.charges <= 0)
            {
                switch (source)
                {
                    case ItemSource.Pouch:
                        if (user?.partyMember?.pouchItems != null)
                            user.partyMember.pouchItems.RemoveAt(index);
                        break;
                    case ItemSource.Party:
                        if (RunContext.partyInventory != null)
                            RunContext.partyInventory.RemoveAt(index);
                        break;
                }
            }
        }

        /// <summary>
        /// Add an item instance to the appropriate bag. For Pouch items the
        /// owner unit must be supplied (used to find the right
        /// PartyMemberRuntime). Stackable items merge with an existing entry
        /// of the same ID; non-stackable items occupy a new slot.
        /// </summary>
        public static void Add(ItemSource source, Unit owner, ItemInstance inst)
        {
            if (inst == null || inst.itemID == ItemID.None) return;

            List<ItemInstance> bag = source switch
            {
                ItemSource.Pouch => owner?.partyMember?.pouchItems,
                ItemSource.Party => RunContext.partyInventory,
                _ => null,
            };
            if (bag == null) return;

            var data = inst.Resolve();
            if (data != null && data.stackable)
            {
                for (int i = 0; i < bag.Count; i++)
                {
                    var existing = bag[i];
                    if (existing != null && existing.itemID == inst.itemID)
                    {
                        existing.charges += Mathf.Max(1, inst.charges);
                        return;
                    }
                }
            }
            bag.Add(inst);
        }

        /// <summary>
        /// Convenience: add by ID + count. Resolves stack/category from
        /// ItemLibrary; routes Pouch-category items to the owner's pouch and
        /// everything else to the shared bag.
        /// </summary>
        public static void Grant(ItemID id, int count, Unit owner)
        {
            if (id == ItemID.None || count <= 0) return;

            var lib = ItemLibrary.Instance;
            var data = lib != null ? lib.Get(id) : null;
            if (data == null)
            {
                Debug.LogWarning(
                    $"[Inventory] Grant({id}, {count}) — ItemLibrary has no entry " +
                    $"for {id}. Did you forget to register the ItemData asset?");
                return;
            }

            ItemSource source = data.category == ItemCategory.Pouch
                ? ItemSource.Pouch
                : ItemSource.Party;

            int per = data.stackable ? count : 1;
            int repeats = data.stackable ? 1 : count;

            for (int r = 0; r < repeats; r++)
            {
                Add(source, owner, new ItemInstance(id, per));
            }
        }
    }
}
