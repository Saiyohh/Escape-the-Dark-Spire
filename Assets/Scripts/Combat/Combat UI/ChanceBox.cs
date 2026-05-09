// ChanceBox.cs
// -----------------------------------------------------------------------------
// Per-enemy hit/afflict chance preview. Pinned to the top-right corner of the
// enemy's hitbox and expands leftward from that anchor. Visible ONLY while
// the pointer hovers the enemy (via UnitDisplay's targeting hover path) AND
// the enemy's current intent actually targets a party member.
//
// Layout:
//   • HorizontalLayoutGroup with childAlignment = MiddleRight and
//     reverseArrangement = true, so the FIRST spawned icon anchors at the
//     right edge and each new one piles to its left.
//   • Each ChanceIconUI shows one small sprite + percentage text.
//   • One row total. Attack icons + afflict icons coexist on the same row;
//     attack icons first (rightmost), afflict icons after (left of them).
//
// Math:
//   Attack hit %:   21 - max(2, targetDEF - attackerATK), / 20, clamped [0.05, 0.95]
//   Afflict land %: 1 - saveSuccess, where saveSuccess = same formula with
//                    (dc - targetWIL) as the gap.
//   Multi-hit intents show a single chance icon (all hits roll at the same %).
//
// Target heuristic:
//   The enemy AI picks its actual target at resolve time, not at intent-set
//   time. For the preview we pick the lowest-HP alive player — the worst-
//   case for the party, and a useful "is this dangerous?" signal.
//
// Prefab setup:
//   ChanceBox (GameObject, child of the enemy HUD canvas)
//     RectTransform (pivot 1,1 / anchor top-right of hitbox — repositioned in
//                     Initialize from UnitDisplay.HitboxSize/Offset)
//     HorizontalLayoutGroup (reverseArrangement true, childAlignment MiddleRight,
//                              childForceExpandWidth/Height false, spacing ~2)
//     ContentSizeFitter (HorizontalFit = PreferredSize, VerticalFit = PreferredSize)
//     └─ (ChanceIconUI children spawned at runtime)
//
// Wire container, chanceIconPrefab, attackIcon, afflictIcon on this component.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
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

        [Tooltip("Sprite shown for afflict-land chance.")]
        [SerializeField] private Sprite afflictIcon;

        [Header("Color Tints")]
        [SerializeField] private Color attackTint = new Color(0.82f, 0.32f, 0.30f, 1f);
        [SerializeField] private Color afflictTintBuff = new Color(0.30f, 0.70f, 0.40f, 1f);
        [SerializeField] private Color afflictTintDebuff = new Color(0.62f, 0.41f, 0.78f, 1f);

        private Unit linkedEnemy;
        private UnitDisplay display;
        private readonly List<ChanceIconUI> spawned = new();
        private EnemyIntent[] currentIntents;

        public void Initialize(Unit enemy)
        {
            linkedEnemy = enemy;
            display = (enemy != null) ? UnitDisplay.GetDisplay(enemy) : null;

            // Hidden by default — ShowHover / HideHover called by UnitDisplay's
            // hover handlers drive visibility.
            gameObject.SetActive(false);

            // Reparent to the shared WorldCanvas + place at the hitbox corner.
            PositionToHitboxTopRight();

            // Register with UnitDisplay so its hover handlers can find us
            // after the reparent (GetComponentInChildren no longer works).
            if (display != null) display.RegisterChanceBox(this);
        }

        private void OnEnable()
        {
            // Rebuild when the active player changes — hit % is computed
            // against the active unit, so cycling turns must update the
            // visible numbers without the player needing to re-hover.
            CombatEvents.OnUnitTurnStart += HandleAnyUnitTurnStart;
        }

        private void OnDisable()
        {
            CombatEvents.OnUnitTurnStart -= HandleAnyUnitTurnStart;
        }

        private void HandleAnyUnitTurnStart(Unit _)
        {
            if (gameObject.activeSelf) Rebuild();
        }

        public void Refresh(IReadOnlyList<EnemyIntent> intents)
        {
            currentIntents = new EnemyIntent[intents?.Count ?? 0];
            if (intents != null)
                for (int i = 0; i < intents.Count; i++) currentIntents[i] = intents[i];

            // If currently visible, rebuild icons with new numbers.
            if (gameObject.activeSelf) Rebuild();
        }

        /// <summary>Show the box — called by CombatUIManager on enemy hover.</summary>
        public void ShowHover()
        {
            if (!HasTargetingIntent(currentIntents)) return;
            gameObject.SetActive(true);
            Rebuild();
        }

        public void HideHover() => gameObject.SetActive(false);

        // ─── Build ───────────────────────────────────────────────────────────

        private void Rebuild()
        {
            // Clear
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i].gameObject);
            spawned.Clear();

            if (currentIntents == null || chanceIconPrefab == null) return;

            var target = PickPreviewTarget();
            if (target == null) return;

            // Spawn attack icons first (they end up rightmost due to
            // reverseArrangement = true), then afflict icons (piled to left).
            for (int i = 0; i < currentIntents.Length; i++)
            {
                var intent = currentIntents[i];
                if (intent == null) continue;
                if (HasAttackEffect(intent))
                    SpawnChanceIcon(attackIcon, attackTint, ComputeHitPercent(target));
            }
            for (int i = 0; i < currentIntents.Length; i++)
            {
                var intent = currentIntents[i];
                if (intent == null) continue;
                var afflict = FindOpposingAfflict(intent);
                if (afflict != null)
                {
                    var tint = ResolveAfflictTint(afflict.conditionID);
                    SpawnChanceIcon(afflictIcon, tint, ComputeAfflictPercent(afflict, target));
                }
            }
        }

        private void SpawnChanceIcon(Sprite sprite, Color tint, float percent01)
        {
            var go = Instantiate(chanceIconPrefab, transform);
            var ui = go.GetComponent<ChanceIconUI>();
            if (ui != null)
            {
                ui.Bind(sprite, tint, percent01);
                spawned.Add(ui);
            }
        }

        // ─── Classification ─────────────────────────────────────────────────

        private static bool HasTargetingIntent(EnemyIntent[] intents)
        {
            if (intents == null) return false;
            for (int i = 0; i < intents.Length; i++)
            {
                if (intents[i] == null) continue;
                if (HasAttackEffect(intents[i])) return true;
                if (FindOpposingAfflict(intents[i]) != null) return true;
            }
            return false;
        }

        /// <summary>Any Attack-typed effect with positive magnitude.</summary>
        private static bool HasAttackEffect(EnemyIntent intent)
        {
            if (intent == null || intent.effects == null) return false;
            for (int i = 0; i < intent.effects.Length; i++)
            {
                var e = intent.effects[i];
                if (e == null) continue;
                if (e.effectType == SkillEffectType.Attack && e.magnitude > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// First condition-applying effect in the intent that targets the
        /// opposing side (i.e. NOT Self / AllAllies — those are self-buffs and
        /// shouldn't show as a chance icon over the targeted player).
        /// </summary>
        private static SkillEffectData FindOpposingAfflict(EnemyIntent intent)
        {
            if (intent == null || intent.effects == null) return null;
            for (int i = 0; i < intent.effects.Length; i++)
            {
                var e = intent.effects[i];
                if (e == null) continue;
                if (!e.AppliesCondition) continue;
                if (e.conditionStacks <= 0) continue;
                if (e.targetMode == TargetMode.Self || e.targetMode == TargetMode.AllAllies) continue;
                return e;
            }
            return null;
        }

        // ─── Math ────────────────────────────────────────────────────────────

        /// <summary>Attack-roll hit percentage vs a specific target, clamped to [0.05, 0.95].</summary>
        private float ComputeHitPercent(Unit target)
        {
            int atk = linkedEnemy != null ? linkedEnemy.EffectiveATK : 0;
            int def = target.EffectiveDEF;
            int needed = Mathf.Max(2, def - atk); // need at least 2 (nat-1 auto-miss)
            int successFaces = 21 - needed;
            return Mathf.Clamp(successFaces / 20f, 0.05f, 0.95f);
        }

        /// <summary>Probability the afflict lands (i.e. target FAILS the WIL save).</summary>
        private float ComputeAfflictPercent(SkillEffectData effect, Unit target)
        {
            int casterWIL = linkedEnemy != null ? linkedEnemy.EffectiveWIL : 0;
            int targetWIL = target.EffectiveWIL;
            int dc = effect != null && effect.saveDC > 0 ? effect.saveDC : 10 + casterWIL;
            // Save succeeds when targetRoll + targetWIL >= dc, i.e. roll >= dc - targetWIL.
            int saveNeeded = Mathf.Max(2, dc - targetWIL);
            int saveSuccessFaces = 21 - saveNeeded;
            float saveChance = Mathf.Clamp(saveSuccessFaces / 20f, 0.05f, 0.95f);
            return 1f - saveChance;
        }

        private Color ResolveAfflictTint(ConditionID id)
        {
            var lib = ConditionLibrary.Instance;
            var data = lib != null ? lib.Get(id) : null;
            if (data == null) return afflictTintDebuff;
            return data.isDebuff ? afflictTintDebuff : afflictTintBuff;
        }

        // ─── Target heuristic ───────────────────────────────────────────────

        /// <summary>
        /// The enemy AI picks its real target at resolve time. For the preview:
        ///   1. Prefer the player currently taking their turn — answers the
        ///      "if this enemy attacks me right now, what's the chance?"
        ///      question that the active player intuitively expects.
        ///   2. Fall back to the lowest-HP alive player when no player turn
        ///      is active (e.g. during enemy phase) — worst-case for the
        ///      party, useful as a "is this dangerous?" signal.
        /// Using ActiveUnit also fixes the previous bug where hit % stayed
        /// frozen on a single target across the whole combat: every turn
        /// change now naturally retargets and HandleAnyUnitTurnStart triggers
        /// a rebuild while the box is visible.
        /// </summary>
        private Unit PickPreviewTarget()
        {
            var mgr = CombatManager.Instance;
            if (mgr == null || mgr.PlayerUnits == null) return null;

            var active = mgr.ActiveUnit;
            if (active != null && active.isPlayerControlled && active.IsAlive)
                return active;

            Unit pick = null;
            int lowestHP = int.MaxValue;
            var list = mgr.PlayerUnits;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                if (u == null || !u.IsAlive) continue;
                if (u.currentHP < lowestHP)
                {
                    lowestHP = u.currentHP;
                    pick = u;
                }
            }
            return pick;
        }

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

            // Top-right of hitbox in unit-local coordinates → converted to
            // a world-space offset for WorldFollow to add to the unit's
            // transform.position each frame.
            Vector3 cornerOffset = new Vector3(
                hb.x + size.x * 0.5f,
                hb.y + size.y * 0.5f,
                0f);

            rt.pivot = new Vector2(1f, 1f); // top-right pivot — grows left & down

            var sharedCanvas = CombatUIManager.WorldCanvas;
            if (sharedCanvas != null)
            {
                rt.SetParent(sharedCanvas.transform, worldPositionStays: false);
                rt.anchoredPosition = Vector2.zero;

                // Attach a follower that tracks the hitbox corner in world coords.
                var follow = GetComponent<WorldFollow>();
                if (follow == null) follow = gameObject.AddComponent<WorldFollow>();
                follow.Bind(display.transform, cornerOffset, display.LinkedUnit);
            }
            else
            {
                // Legacy fallback: parent to UnitDisplay so HUD-canvas-local
                // coordinates still resolve.
                rt.SetParent(display.transform, worldPositionStays: false);
                rt.anchoredPosition = new Vector2(cornerOffset.x, cornerOffset.y);
            }
        }
    }
}
