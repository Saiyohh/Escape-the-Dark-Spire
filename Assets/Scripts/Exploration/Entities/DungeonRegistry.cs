using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class DungeonRegistry : MonoBehaviour
    {
        public static DungeonRegistry Instance { get; private set; }

        private readonly Dictionary<Vector2Int, List<IDungeonEntity>> byPos = new();
        private static readonly List<IDungeonEntity> Empty = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DungeonRegistry] Replacing existing Instance.");
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            byPos.Clear();
        }

        public void Register(IDungeonEntity e)
        {
            if (e == null) return;
            if (!byPos.TryGetValue(e.GridPos, out var list))
            {
                list = new List<IDungeonEntity>();
                byPos[e.GridPos] = list;
            }
            if (!list.Contains(e)) list.Add(e);
        }

        public void Unregister(IDungeonEntity e)
        {
            if (e == null) return;
            if (byPos.TryGetValue(e.GridPos, out var list))
            {
                list.Remove(e);
                if (list.Count == 0) byPos.Remove(e.GridPos);
            }
        }

        public void Move(IDungeonEntity e, Vector2Int oldPos, Vector2Int newPos)
        {
            if (e == null) return;
            if (byPos.TryGetValue(oldPos, out var oldList))
            {
                oldList.Remove(e);
                if (oldList.Count == 0) byPos.Remove(oldPos);
            }
            if (!byPos.TryGetValue(newPos, out var newList))
            {
                newList = new List<IDungeonEntity>();
                byPos[newPos] = newList;
            }
            if (!newList.Contains(e)) newList.Add(e);
        }

        public IReadOnlyList<IDungeonEntity> GetAt(Vector2Int pos)
        {
            return byPos.TryGetValue(pos, out var list) ? list : Empty;
        }

        public T GetAt<T>(Vector2Int pos) where T : class
        {
            if (!byPos.TryGetValue(pos, out var list)) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is T t) return t;
            return null;
        }

        public bool IsBlocked(Vector2Int pos, out string reason)
        {
            reason = null;
            if (!byPos.TryGetValue(pos, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is IBlocker b && b.IsBlocking)
                {
                    reason = b.BlockReason;
                    return true;
                }
            }
            return false;
        }
    }
}
