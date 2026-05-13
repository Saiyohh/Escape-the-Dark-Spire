// ChanceBox.cs
// -----------------------------------------------------------------------------
// Player → enemy hit-chance preview. Pinned to the top-right corner of the
// enemy's hitbox and visible ONLY while the player is in targeting mode AND
// the pointer is over THIS enemy AND this enemy is a valid target.
//
// Shows the active caster's chance to hit this enemy with a standard attack
// roll: d20 + caster.ATK vs target.DEF (>=, ties hit). Math lives in
// AttackChance.Hit and is shared with the DangerPreviewController on-hover
// enemy-intent preview, so the two surfaces can't drift apart.
//
// Example: DEX 4 vs DEF 10 → needed = 6 → 15/20 = 75%.
//
// Visibility is driven by TargetingSystem events — UnitDisplay's hover path
// no longer touches this component.
// -----------------------------------------------------------------------------
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

            // Keep registration so UnitDisplay still has a handle (used during
            // teardown / future redesign of the enemy-intent preview).
            if (display != null) display.RegisterChanceBox(this);

            // Visibility is icon-presence-driven (no SetActive) so OnEnable /
            // OnDisable can stay tied to the GameObject's real lifecycle and
            // keep our TargetingSystem subscription stable.
            ClearIcon();
            // OnEnable may have run before TargetingSystem.Instance existed
            // (race during scene boot). Retry now that Initialize is called.
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

        // ─── Targeting subscription ─────────────────────────────────────────

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

        // ─── Build ───────────────────────────────────────────────────────────

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

        // Hit-chance math lives in AttackChance.Hit so this preview and the
        // intent-hover DangerPreviewController stay in lock-step with each
        // other and with DiceRoller.AttackRoll.

        // ─── Positioning ────────────────────────────────────────────────────

        /// <summary>
        /// Reparent the ChanceBox under the shared CombatUIManager.WorldCanvas
        /// (no per-unit canvas), and attach a WorldFollow that tracks the
        /// enemy's hitbox top-right corner in world space. Falls back to the
        /// legacy parent-to-UnitDisplay flow if the shared canvas isn't set
        /// up — keeps old prefabs working during migration.
        /// </summary>
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
