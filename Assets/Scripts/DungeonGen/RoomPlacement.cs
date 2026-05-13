using UnityEngine;

namespace DarkSpire
{
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
