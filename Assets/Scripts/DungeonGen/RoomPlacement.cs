using UnityEngine;

namespace DarkSpire
{
    // Runtime record of a room placed during generation. Bounds are inclusive
    // of walls? No: bounds describes the floor-tile rectangle of the room
    // interior. Surrounding walls are carved in the grid outside this rect.
    public struct RoomPlacement
    {
        public RoomKind kind;
        public RectInt bounds;
        public RoomTemplateSO template;     // null = fallback rectangular fill

        public Vector2Int Center => new Vector2Int(
            bounds.xMin + bounds.width / 2,
            bounds.yMin + bounds.height / 2);

        public RoomPlacement(RoomKind kind, RectInt bounds, RoomTemplateSO template = null)
        {
            this.kind = kind;
            this.bounds = bounds;
            this.template = template;
        }
    }
}
