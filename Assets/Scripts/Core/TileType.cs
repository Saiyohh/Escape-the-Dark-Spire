namespace DarkSpire
{
    public enum TileType
    {
        // ── Impassable ──
        Wall = 0,    // Bordering wall sprite — sits next to walkable tiles, builds the silhouette of rooms/corridors.
        Empty = 5,   // The void OUTSIDE the walls — visually distinct from Wall but behaviorally identical (impassable, blocks LoS). Created at the end of generation by demoting any Wall tile that has no walkable neighbor.

        // ── Walkable ──
        Floor = 1,
        Start = 2,
        Stairway = 3,
        Rest = 4,    // Walkable. Renders as the campsite sprite (the same thing visually); walkover triggers the "Press E to rest" interaction.
    }

    public static class TileTypeExtensions
    {
        /// <summary>True if a unit can stand on / pass through this tile type.</summary>
        public static bool IsWalkable(this TileType t) =>
            t == TileType.Floor
            || t == TileType.Start
            || t == TileType.Stairway
            || t == TileType.Rest;

        /// <summary>True if this tile blocks movement / line of sight (Wall + Empty).</summary>
        public static bool IsImpassable(this TileType t) => !t.IsWalkable();
    }
}
