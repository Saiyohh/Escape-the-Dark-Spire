using UnityEngine;

namespace DarkSpire
{
    public interface IDungeonEntity
    {
        Vector2Int GridPos { get; }
    }

    public interface IInteractable : IDungeonEntity
    {
        bool CanInteract(in InteractionContext ctx);
        void Interact(in InteractionContext ctx);
        string PromptText { get; }
    }

    public interface IWalkOver : IDungeonEntity
    {
        void OnWalkedOver(in InteractionContext ctx);
    }

    public interface IBlocker : IDungeonEntity
    {
        bool IsBlocking { get; }
        string BlockReason { get; }
    }
}
