// ItemEnums.cs
// -----------------------------------------------------------------------------
// Enums for the items system. Kept in their own file (not CombatEnums.cs) so
// the items folder is self-contained and CombatEnums doesn't bloat further.
//
// ItemID is the stable address space for items — mirrors ConditionID. When a
// designer wants a new item they add an entry here, create an ItemData SO with
// that ID, and the ItemLibrary postprocessor auto-registers it.
//
// Items reuse SkillEffectData for their effects array (see ItemData.cs), so no
// item-specific effect enum is needed.
// -----------------------------------------------------------------------------
namespace DarkSpire
{
    /// <summary>
    /// Stable address for every item in the project. Mirrors the ConditionID
    /// pattern: serialized state (ItemInstance) stores this enum and resolves
    /// to ItemData via ItemLibrary at point of use. Designers extend this
    /// list as they author new items.
    /// </summary>
    public enum ItemID
    {
        None = 0,
    }

    /// <summary>
    /// How using an item interacts with turn economy. Mirrors and extends
    /// ActionCostType (which is skill-specific and lacks ZeroCost).
    /// </summary>
    public enum ItemActionCostType
    {
        /// <summary>Consumes the unit's Action — sets hasActedThisTurn.</summary>
        Action,
        /// <summary>Consumes the once-per-turn Free Action slot.</summary>
        FreeAction,
        /// <summary>Bypasses both gates — usable any number of times per turn.</summary>
        ZeroCost,
    }

    /// <summary>
    /// Where an item lives in the merged inventory panel. Drives display and
    /// ownership rules: Pouch items are character-locked, Party Inventory and
    /// Brewed Potions are shared across the party.
    /// </summary>
    public enum ItemCategory
    {
        Pouch,
        PartyInventory,
        BrewedPotion,
    }

    /// <summary>
    /// When the item expires. Persistent is the default — item stays until
    /// used. Transient/Passing/Ethereal define expiry rules for future
    /// inventory-lifetime work; runtime currently only honors Persistent.
    /// </summary>
    public enum ItemDuration
    {
        Persistent,
        Transient,
        Passing,
        Ethereal,
    }

    /// <summary>
    /// Where in the party-wide inventory an item lives at runtime. Used by
    /// the Inventory helper to disambiguate which list to consume from.
    /// </summary>
    public enum ItemSource
    {
        Party,
        Pouch,
    }
}
