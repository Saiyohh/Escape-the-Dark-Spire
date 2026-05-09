using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Hand-authored interior layout for a room kind. The generator picks one
    // template at random per placed room and stamps it into the grid.
    //
    // Phase 1 ships without authored templates — the generator falls back to
    // a flat rectangular floor fill when template == null. Templates can be
    // added incrementally to add variety without changing the pipeline.
    [CreateAssetMenu(fileName = "RT_NewTemplate", menuName = "DarkSpire/Dungeon/Room Template")]
    public class RoomTemplateSO : ScriptableObject
    {
        public RoomKind kind;
        public Vector2Int size;

        // Flat row-major: index = y * size.x + x
        public TileType[] tilesFlat;

        // Edge positions where corridors may connect. Local coordinates
        // relative to the template's (0,0) bottom-left.
        public List<Vector2Int> doorways = new();

        // Hand-authored entity placements relative to the template origin.
        public List<EntitySlot> entitySlots = new();

        public TileType GetTile(int x, int y) => tilesFlat[y * size.x + x];

        [Serializable]
        public struct EntitySlot
        {
            public Vector2Int localPosition;
            public EntityKind kind;
        }
    }
}
