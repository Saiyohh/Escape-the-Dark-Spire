using UnityEngine;

namespace DarkSpire
{
    // Common interface for anything registered with the DungeonRegistry by
    // grid position. Each gameplay component on the floor (key, chest, gate,
    // shrine, monster in Phase 9) implements this and registers itself in
    // Awake/Initialize.
    public interface IDungeonEntity
    {
        Vector2Int GridPos { get; }
    }

    // Adjacency-interact (E key from the party). PartyToken's tile and the
    // four cardinal neighbours are checked.
    public interface IInteractable : IDungeonEntity
    {
        bool CanInteract(in InteractionContext ctx);
        void Interact(in InteractionContext ctx);
        string PromptText { get; }
    }

    // Auto-trigger when the party settles on this tile (key pickup, gold pile).
    public interface IWalkOver : IDungeonEntity
    {
        void OnWalkedOver(in InteractionContext ctx);
    }

    // Blocks party movement onto this tile while IsBlocking is true.
    public interface IBlocker : IDungeonEntity
    {
        bool IsBlocking { get; }
        string BlockReason { get; }
    }
}
