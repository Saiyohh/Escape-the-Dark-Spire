using UnityEngine;

namespace DarkSpire
{
    // Step 6: BFS reachability assertions. Returns true if the floor is
    // playable; false signals the top-level driver to discard this attempt
    // and regenerate with a different seed offset.
    internal static class ValidateFloor
    {
        public static bool Apply(GenContext ctx)
        {
            // Reachability from Start over all non-wall tiles.
            var reachable = GridBfs.Reachable(
                ctx.tiles, ctx.startPos, t => t.IsWalkable());

            // Every room center (Key/Rest/Shrine/Boss/Loot) must be reachable.
            foreach (var room in ctx.rooms)
            {
                if (!reachable.Contains(room.Center))
                {
                    Debug.LogWarning($"[Validate] {room.kind} at {room.Center} unreachable from Start");
                    return false;
                }
            }

            // Boss Gate must exist and be reachable.
            if (ctx.bossGatePos == default)
            {
                Debug.LogWarning("[Validate] Boss Gate was not placed");
                return false;
            }
            if (!reachable.Contains(ctx.bossGatePos))
            {
                Debug.LogWarning($"[Validate] Boss Gate at {ctx.bossGatePos} unreachable from Start");
                return false;
            }

            // Key count satisfied.
            int keys = 0;
            foreach (var e in ctx.entities) if (e.kind == EntityKind.Key) keys++;
            if (keys < ctx.config.keysRequired)
            {
                Debug.LogWarning($"[Validate] Only {keys} keys placed; required {ctx.config.keysRequired}");
                return false;
            }

            return true;
        }
    }
}
