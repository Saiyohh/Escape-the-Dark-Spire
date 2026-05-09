// WorldFollow.cs
// -----------------------------------------------------------------------------
// Glues a UI RectTransform to a world-space Transform's position. Used when
// a HUD lives under a SHARED world-space Canvas (instead of as a child of
// the unit it's tracking) but still needs to follow that unit's movement —
// rank slides, knockbacks, etc.
//
// Cheap LateUpdate copy of `target.position + offset` to `transform.position`.
// LateUpdate ensures we read the unit's final per-frame position after any
// of its own move animations have run.
//
// Optional `lifetimeBoundTo` field — if set, this object self-destroys when
// the bound Unit dies (cleans up dangling HUDs from the shared canvas).
// -----------------------------------------------------------------------------
using UnityEngine;

namespace DarkSpire
{
    [DisallowMultipleComponent]
    public class WorldFollow : MonoBehaviour
    {
        [Tooltip("World-space transform to follow. Set via Bind() at spawn.")]
        [SerializeField] private Transform target;

        [Tooltip("World-space offset added to the target's position each frame.")]
        [SerializeField] private Vector3 offset;

        [Tooltip("Optional Unit reference. When non-null and the unit dies, this " +
                 "GameObject is destroyed automatically so the shared canvas " +
                 "doesn't accumulate corpses' HUDs.")]
        [SerializeField] private Unit lifetimeBoundTo;

        public Transform Target { get => target; set => target = value; }
        public Vector3 Offset { get => offset; set => offset = value; }

        /// <summary>
        /// Wire this follower to a unit. Sets the target transform, offset,
        /// and (optionally) the unit-death cleanup hook.
        /// </summary>
        public void Bind(Transform target, Vector3 offset = default, Unit lifetimeUnit = null)
        {
            this.target = target;
            this.offset = offset;
            this.lifetimeBoundTo = lifetimeUnit;
            if (lifetimeUnit != null)
                lifetimeUnit.OnDeath += HandleBoundUnitDeath;

            ApplyNow();
        }

        private void OnDestroy()
        {
            if (lifetimeBoundTo != null)
                lifetimeBoundTo.OnDeath -= HandleBoundUnitDeath;
        }

        private void HandleBoundUnitDeath()
        {
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + offset;
        }

        /// <summary>Snap once now (e.g. immediately after Bind so frame-1 doesn't lag).</summary>
        public void ApplyNow()
        {
            if (target == null) return;
            transform.position = target.position + offset;
        }
    }
}
