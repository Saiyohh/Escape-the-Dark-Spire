namespace DarkSpire
{
    public enum ItemID
    {
        None = 0,
    }

    public enum ItemActionCostType
    {
        Action,
        FreeAction,
        ZeroCost,
    }

    public enum ItemCategory
    {
        Pouch,
        PartyInventory,
        BrewedPotion,
    }

    public enum ItemDuration
    {
        Persistent,
        Transient,
        Passing,
        Ethereal,
    }

    public enum ItemSource
    {
        Party,
        Pouch,
    }
}
