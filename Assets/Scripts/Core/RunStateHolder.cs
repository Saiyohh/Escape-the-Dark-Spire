using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // DontDestroyOnLoad carrier for in-progress run state that must survive
    // scene loads (dungeon -> combat -> dungeon). Holds:
    //   - the active GeneratedFloorData (so the dungeon scene resumes the
    //     same layout instead of regenerating)
    //   - the party's return position
    //   - per-position "this entity has already been consumed/used" sets
    //     so on resume the dungeon scene doesn't respawn picked-up keys,
    //     re-lock unlocked gates, etc.
    //
    // Cleared via ClearForNewRun on title-screen "Descend".
    public class RunStateHolder : MonoBehaviour
    {
        public static RunStateHolder Instance { get; private set; }

        public bool resumeMode;
        public GeneratedFloorData savedFloor;
        public Vector2Int returnPos;

        public readonly HashSet<Vector2Int> removedEntities  = new();   // keys, gold piles
        public readonly HashSet<Vector2Int> openedChests     = new();
        public readonly HashSet<Vector2Int> usedShrines      = new();
        public readonly HashSet<Vector2Int> unlockedGates    = new();
        public readonly HashSet<Vector2Int> usedRestTiles    = new();
        public readonly HashSet<Vector2Int> defeatedMonsters = new();   // by spawn start position
        public bool bossDefeated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static RunStateHolder GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RunStateHolder");
            return go.AddComponent<RunStateHolder>();
        }

        public void ClearForNewRun()
        {
            resumeMode = false;
            savedFloor = null;
            returnPos = default;
            removedEntities.Clear();
            openedChests.Clear();
            usedShrines.Clear();
            unlockedGates.Clear();
            usedRestTiles.Clear();
            defeatedMonsters.Clear();
            bossDefeated = false;
        }

        public void SaveForCombat(GeneratedFloorData floor, Vector2Int partyPos)
        {
            savedFloor = floor;
            returnPos = partyPos;
            resumeMode = true;
        }

        // Convenience predicates ------------------------------------------

        public bool IsRemoved(Vector2Int p)         => removedEntities.Contains(p);
        public bool IsChestOpened(Vector2Int p)     => openedChests.Contains(p);
        public bool IsShrineUsed(Vector2Int p)      => usedShrines.Contains(p);
        public bool IsGateUnlocked(Vector2Int p)    => unlockedGates.Contains(p);
        public bool IsRestUsed(Vector2Int p)        => usedRestTiles.Contains(p);
        public bool IsMonsterDefeated(Vector2Int p) => defeatedMonsters.Contains(p);
    }
}
