using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    public class DungeonInteractor : MonoBehaviour
    {
        [SerializeField] private PartyToken token;
        [SerializeField] private DungeonRegistry registry;
        [SerializeField] private DungeonWalkoverDispatcher walkoverDispatcher;
        [SerializeField] private bool blockOnModal = true;
        public bool ModalOpen { get; set; }

        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(0, 1),    // N
            new Vector2Int(0, -1),   // S
            new Vector2Int(-1, 0),   // W
            new Vector2Int(1, 0),    // E
        };

        public void Bind(PartyToken token, DungeonRegistry registry, DungeonWalkoverDispatcher walkover = null)
        {
            this.token = token;
            this.registry = registry;
            this.walkoverDispatcher = walkover;
        }

        private void Update()
        {
            if (token == null || registry == null) return;
            if (blockOnModal && ModalOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.eKey.wasPressedThisFrame) return;

            var ctx = new InteractionContext(token, registry);

            if (TryAt(token.GridPos, ctx)) return;

            foreach (var d in Dirs)
            {
                if (TryAt(token.GridPos + d, ctx)) return;
            }

            if (walkoverDispatcher != null && walkoverDispatcher.TryTileInteract(token.GridPos))
                return;
        }

        private bool TryAt(Vector2Int pos, InteractionContext ctx)
        {
            var ent = registry.GetAt<IInteractable>(pos);
            if (ent == null) return false;
            if (!ent.CanInteract(ctx)) return false;
            ent.Interact(ctx);
            return true;
        }
    }
}
