// ItemInstance.cs
// -----------------------------------------------------------------------------
// Runtime, serializable record for one item the party (or a character's pouch)
// holds. Stored as a list on:
//   • RunContext.partyInventory       — shared bag (Party Inventory + Brewed)
//   • PartyMemberRuntime.pouchItems   — character-locked pouches
//
// Stores ItemID rather than a direct ItemData reference so save/load and
// scene-spanning persistence don't depend on Unity SO refs surviving. The
// ItemData is looked up via ItemLibrary.Instance.Get(itemID) at point of use.
//
// charges is 1 for non-stackable items. Stackable items merge on Add and
// decrement-then-remove on Consume.
// -----------------------------------------------------------------------------
using System;

namespace DarkSpire
{
    [Serializable]
    public class ItemInstance
    {
        public ItemID itemID;
        public int charges = 1;

        public ItemInstance() { }

        public ItemInstance(ItemID id, int charges = 1)
        {
            this.itemID = id;
            this.charges = charges < 1 ? 1 : charges;
        }

        /// <summary>Resolve the live ItemData via ItemLibrary, or null if unregistered.</summary>
        public ItemData Resolve()
        {
            var lib = ItemLibrary.Instance;
            return lib != null ? lib.Get(itemID) : null;
        }
    }
}
