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

        public ItemData Resolve()
        {
            var lib = ItemLibrary.Instance;
            return lib != null ? lib.Get(itemID) : null;
        }
    }
}
