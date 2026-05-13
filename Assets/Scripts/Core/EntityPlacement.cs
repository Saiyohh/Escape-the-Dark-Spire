using System;
using UnityEngine;

namespace DarkSpire
{
    [Serializable]
    public struct EntityPlacement
    {
        public Vector2Int position;
        public EntityKind kind;

        public int intPayload;
        public string strPayload;

        public EntityPlacement(Vector2Int position, EntityKind kind, int intPayload = 0, string strPayload = null)
        {
            this.position = position;
            this.kind = kind;
            this.intPayload = intPayload;
            this.strPayload = strPayload;
        }
    }
}
