using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Subscribes to PartyToken.OnPlaced + OnMoved and triggers:
    //   - IWalkOver entities at the party's new tile (key pickup, gold pile)
    //   - TileType.Stairway -> Floor advance (gated by bossDefeated flag)
    //
    // Rest tiles are intentionally NOT auto-triggered here — they're consumed
    // via DungeonInteractor's E-press at the party's own tile so the player
    // can walk past one without spending it. Phase 11 adds an above-tile
    // button prompt (icon + text) when standing on a rest tile.
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

        // Reusable buffer for snapshot iteration (walkover handlers may
        // unregister themselves mid-iteration).
        private readonly List<IDungeonEntity> buffer = new();

        public void Bind(PartyToken token, DungeonRegistry registry, GeneratedFloorData floor)
        {
            Unhook();
            this.token = token;
            this.registry = registry;
            this.floor = floor;
            usedRestTiles.Clear();
            bossDefeated = false;

            // Resume: hydrate from RunStateHolder so previously-used rest tiles
            // and a defeated boss carry over from before combat.
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
                        // boss is the Victory trigger. Multi-floor handling
                        // (next floor vs. final floor) lands when floors 2+
                        // ship.
                        SceneFlow.LoadVictory();
                    }
                    else
                    {
                        DungeonEvents.InvokeFlashMessage("Defeat the boss first.");
                    }
                }
            }
        }

        // Called by DungeonInteractor when E is pressed and no entity claims it.
        // Returns true if a tile-based interaction fired.
        public bool TryTileInteract(Vector2Int pos)
        {
            if (floor == null || !floor.InBounds(pos)) return false;
            if (floor.tiles[pos.x, pos.y] == TileType.Rest && !usedRestTiles.Contains(pos))
            {
                usedRestTiles.Add(pos);
                RunStateHolder.Instance?.usedRestTiles.Add(pos);
                Debug.Log($"[Rest] Used rest tile at {pos}.");

                // Open the modal Rest Menu — buttons handle the actual heal,
                // revive, and per-member toast feedback. Tile is consumed
                // regardless of which button the player picks (consume on open).
                var menu = RestMenu.Instance ?? RestMenu.GetOrCreate();
                menu?.Open(pos);

                // Remove the campsite overlay so the tile reads as a regular
                // Floor going forward.
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
