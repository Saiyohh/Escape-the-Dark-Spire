using UnityEngine;

namespace DarkSpire
{
    public class ChanceBox : MonoBehaviour
    {
        [Header("Prefab + Icons")]
        [Tooltip("Prefab with a ChanceIconUI component (Image + TMP label).")]
        [SerializeField] private GameObject chanceIconPrefab;

        [Tooltip("Sprite shown for attack-hit chance.")]
        [SerializeField] private Sprite attackIcon;

        [Header("Color Tints")]
        [SerializeField] private Color attackTint = new Color(0.82f, 0.32f, 0.30f, 1f);

        private Unit linkedEnemy;
        private UnitDisplay display;
        private ChanceIconUI spawnedIcon;
        private Unit lastHovered;

        public void Initialize(Unit enemy)
        {
            linkedEnemy = enemy;
            display = (enemy != null) ? UnitDisplay.GetDisplay(enemy) : null;

            PositionToHitboxTopRight();

            if (display != null) display.RegisterChanceBox(this);

            ClearIcon();
            SubscribeTargeting();
        }

        private void OnEnable()
        {
            SubscribeTargeting();
        }

        private void OnDisable()
        {
            UnsubscribeTargeting();
            lastHovered = null;
        }

        private bool subscribed;

        private void SubscribeTargeting()
        {
            if (subscribed || TargetingSystem.Instance == null) return;
            TargetingSystem.Instance.OnTargetingStateChanged += HandleTargetingStateChanged;
            TargetingSystem.Instance.OnTargetHovered        += HandleTargetHovered;
            TargetingSystem.Instance.OnTargetUnhovered      += HandleTargetUnhovered;
            subscribed = true;
        }

        private void UnsubscribeTargeting()
        {
            if (!subscribed || TargetingSystem.Instance == null) { subscribed = false; return; }
            TargetingSystem.Instance.OnTargetingStateChanged -= HandleTargetingStateChanged;
            TargetingSystem.Instance.OnTargetHovered        -= HandleTargetHovered;
            TargetingSystem.Instance.OnTargetUnhovered      -= HandleTargetUnhovered;
            subscribed = false;
        }

        private void HandleTargetingStateChanged(bool isTargeting)
        {
            if (!isTargeting) Hide();
            else EvaluateVisibility();
        }

        private void HandleTargetHovered(Unit unit)
        {
            lastHovered = unit;
            EvaluateVisibility();
        }

        private void HandleTargetUnhovered()
        {
            lastHovered = null;
            Hide();
        }

        private void EvaluateVisibility()
        {
            var ts = TargetingSystem.Instance;
            if (ts == null || !ts.IsTargeting) { Hide(); return; }
            if (lastHovered != linkedEnemy)    { Hide(); return; }
            if (!ts.IsValidTarget(linkedEnemy)) { Hide(); return; }
            Show();
        }

        private void Show() => Rebuild();

        private void Hide() => ClearIcon();

        private void Rebuild()
        {
            ClearIcon();

            if (chanceIconPrefab == null || linkedEnemy == null) return;

            var caster = TargetingSystem.Instance != null ? TargetingSystem.Instance.Caster : null;
            if (caster == null) return;

            float percent = AttackChance.Hit(caster, linkedEnemy);
            SpawnChanceIcon(attackIcon, attackTint, percent);
        }

        private void ClearIcon()
        {
            if (spawnedIcon != null)
            {
                Destroy(spawnedIcon.gameObject);
                spawnedIcon = null;
            }
        }

        private void SpawnChanceIcon(Sprite sprite, Color tint, float percent01)
        {
            var go = Instantiate(chanceIconPrefab, transform);
            var ui = go.GetComponent<ChanceIconUI>();
            if (ui != null)
            {
                ui.Bind(sprite, tint, percent01);
                spawnedIcon = ui;
            }
        }

        private void PositionToHitboxTopRight()
        {
            var rt = transform as RectTransform;
            if (rt == null || display == null) return;

            var size = display.HitboxSize;
            var hb   = display.HitboxOffset;

            Vector3 cornerOffset = new Vector3(
                hb.x + size.x * 0.5f,
                hb.y + size.y * 0.5f,
                0f);

            rt.pivot = new Vector2(1f, 1f);

            var sharedCanvas = CombatUIManager.WorldCanvas;
            if (sharedCanvas != null)
            {
                rt.SetParent(sharedCanvas.transform, worldPositionStays: false);
                rt.anchoredPosition = Vector2.zero;

                var follow = GetComponent<WorldFollow>();
                if (follow == null) follow = gameObject.AddComponent<WorldFollow>();
                follow.Bind(display.transform, cornerOffset, display.LinkedUnit);
            }
            else
            {
                rt.SetParent(display.transform, worldPositionStays: false);
                rt.anchoredPosition = new Vector2(cornerOffset.x, cornerOffset.y);
            }
        }
    }
}
