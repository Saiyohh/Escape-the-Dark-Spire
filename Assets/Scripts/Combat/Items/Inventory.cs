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

        public static IEnumerable<Entry> GetUsableFor(Unit unit)
        {
            if (unit == null) yield break;

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
