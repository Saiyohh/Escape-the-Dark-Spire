using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class DungeonWalkoverDispatcher : MonoBehaviour
    {
        [SerializeField] private PartyToken token;
        [SerializeField] private DungeonRegistry registry;
        [Tooltip("Optional. Set so the campsite overlay can be destroyed when " +
                 "the rest is consumed. DungeonBootstrap wires this at runtime.")]
        [SerializeField] private FloorRenderer floorRenderer;

        public void SetFloorRenderer(FloorRenderer r) => floorRenderer = r;

        private GeneratedFloorData floor;
        private readonly HashSet<Vector2Int> usedRestTiles = new();
        private bool bossDefeated;

        private readonly List<IDungeonEntity> buffer = new();

        public void Bind(PartyToken token, DungeonRegistry registry, GeneratedFloorData floor)
        {
            Unhook();
            this.token = token;
            this.registry = registry;
            this.floor = floor;
            usedRestTiles.Clear();
            bossDefeated = false;

            var holder = RunStateHolder.Instance;
            if (holder != null)
            {
                foreach (var p in holder.usedRestTiles) usedRestTiles.Add(p);
                bossDefeated = holder.bossDefeated;
            }
            Hook();
        }

        public void NotifyBossDefeated() => bossDefeated = true;

        private void OnDestroy() => Unhook();

        private void Hook()
        {
            if (token == null) return;
            token.OnPlaced += HandlePlaced;
            token.OnMoved  += HandleMoved;
        }

        private void Unhook()
        {
            if (token == null) return;
            token.OnPlaced -= HandlePlaced;
            token.OnMoved  -= HandleMoved;
        }

        private void HandlePlaced(Vector2Int to) => Check(to);
        private void HandleMoved(Vector2Int from, Vector2Int to) => Check(to);

        private void Check(Vector2Int pos)
        {
            if (registry != null)
            {
                var list = registry.GetAt(pos);
                buffer.Clear();
                buffer.AddRange(list);
                var ctx = new InteractionContext(token, registry);
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer[i] is IWalkOver w) w.OnWalkedOver(ctx);
                }
            }

            if (floor != null && floor.InBounds(pos))
            {
                var t = floor.tiles[pos.x, pos.y];
                if (t == TileType.Stairway)
                {
                    if (bossDefeated)
                    {
                        Debug.Log("[Stairway] Floor cleared.");
                        DungeonEvents.InvokeFloorAdvance();
                        SceneFlow.LoadVictory();
                    }
                    else
                    {
                        DungeonEvents.InvokeFlashMessage("Defeat the boss first.");
                    }
                }
            }
        }

        public bool TryTileInteract(Vector2Int pos)
        {
            if (floor == null || !floor.InBounds(pos)) return false;
            if (floor.tiles[pos.x, pos.y] == TileType.Rest && !usedRestTiles.Contains(pos))
            {
                usedRestTiles.Add(pos);
                RunStateHolder.Instance?.usedRestTiles.Add(pos);
                Debug.Log($"[Rest] Used rest tile at {pos}.");

                var menu = RestMenu.Instance ?? RestMenu.GetOrCreate();
                menu?.Open(pos);

                if (floorRenderer != null) floorRenderer.MarkRestUsed(pos);
                return true;
            }
            return false;
        }

        public bool IsRestAvailableAt(Vector2Int pos)
        {
            if (floor == null || !floor.InBounds(pos)) return false;
            return floor.tiles[pos.x, pos.y] == TileType.Rest && !usedRestTiles.Contains(pos);
        }
    }
}
