using UnityEngine;

namespace DarkSpire
{
    // Smooth follow on the party token. Optionally clamps the camera so it
    // stays inside the floor bounds (no empty space outside the dungeon).
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Higher = snappier follow. ~8 feels good at 60fps.")]
        [SerializeField] private float smoothing = 8f;
        [SerializeField] private bool clampToFloor = true;

        private GeneratedFloorData floor;
        private Camera cam;

        public void SetTarget(Transform t) => target = t;
        public void SetFloor(GeneratedFloorData f) => floor = f;

        private void Awake() => cam = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null) return;

            var current = transform.position;
            var desired = new Vector3(target.position.x, target.position.y, current.z);

            if (clampToFloor && floor != null && cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * Mathf.Max(cam.aspect, 0.0001f);
                float minX = halfW;
                float maxX = floor.gridSize.x - halfW;
                float minY = halfH;
                float maxY = floor.gridSize.y - halfH;

                // If floor is smaller than the view, just center.
                if (minX > maxX) { float c = floor.gridSize.x * 0.5f; minX = c; maxX = c; }
                if (minY > maxY) { float c = floor.gridSize.y * 0.5f; minY = c; maxY = c; }

                desired.x = Mathf.Clamp(desired.x, minX, maxX);
                desired.y = Mathf.Clamp(desired.y, minY, maxY);
            }

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(current, desired, t);
        }
    }
}
