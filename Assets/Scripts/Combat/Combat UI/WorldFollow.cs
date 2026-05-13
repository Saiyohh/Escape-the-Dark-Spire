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

        public void ApplyNow()
        {
            if (target == null) return;
            transform.position = target.position + offset;
        }
    }
}
