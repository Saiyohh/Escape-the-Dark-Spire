using System;
using UnityEngine;

namespace DarkSpire
{
    [Serializable]
    public struct EntityPlacement
    {
        public Vector2Int position;
        public EntityKind kind;

        // Generic payload slots so the generator can pass per-entity context
        // (gold amount, chest contents tag, key index, etc.) without a separate
        // per-kind struct hierarchy. Phases 6/7 read these by kind.
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
