using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    // Reads WASD / Arrow Keys via the new Input System and feeds direction
    // inputs to a PartyToken. Cooldown caps the move rate at ~8 moves/sec
    // per the GDD.
    //
    // Vertical takes precedence over horizontal when both are held (cheap
    // tiebreak; tweak Read() to swap).
    public class GridMovement : MonoBehaviour
    {
        [SerializeField] private PartyToken token;
        [SerializeField] private float moveCooldown = 0.12f;

        [Tooltip("If true, ignores movement input while a modal UI (e.g. Rest Menu) is open.")]
        [SerializeField] private bool blockOnModal = true;
        public bool ModalOpen { get; set; }

        private float cooldownTimer;

        public void SetToken(PartyToken t) => token = t;

        private void Update()
        {
            if (token == null) return;
            if (blockOnModal && ModalOpen) return;

            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;
            if (token.IsMoving) return;

            var dir = ReadDirection();
            if (dir == Vector2Int.zero) return;

            if (token.TryMove(dir))
                cooldownTimer = moveCooldown;
        }

        private static Vector2Int ReadDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2Int.zero;

            // Vertical takes precedence over horizontal when both held.
            if (kb.wKey.isPressed     || kb.upArrowKey.isPressed)    return Vector2Int.up;
            if (kb.sKey.isPressed     || kb.downArrowKey.isPressed)  return Vector2Int.down;
            if (kb.aKey.isPressed     || kb.leftArrowKey.isPressed)  return Vector2Int.left;
            if (kb.dKey.isPressed     || kb.rightArrowKey.isPressed) return Vector2Int.right;
            return Vector2Int.zero;
        }
    }
}
