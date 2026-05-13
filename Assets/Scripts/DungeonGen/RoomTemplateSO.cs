using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "RT_NewTemplate", menuName = "DarkSpire/Dungeon/Room Template")]
    public class RoomTemplateSO : ScriptableObject
    {
        public RoomKind kind;
        public Vector2Int size;

        public TileType[] tilesFlat;

        public List<Vector2Int> doorways = new();

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
