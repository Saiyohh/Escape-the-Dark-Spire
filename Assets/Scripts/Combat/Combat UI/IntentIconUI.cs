// IntentIconUI.cs
// -----------------------------------------------------------------------------
// One intent icon in the EnemyWorldHUD above-region strip. Bound to a single
// EnemyIntent and renders what the enemy is going to do: an attack icon
// with damage, a defense icon with shields gained, a buff/debuff icon, etc.
//
// The intent's behavior is a chain of SkillEffectData[] effects (the same
// fidelity skills use). The label is derived by inspecting those effects
// rather than reading flat fields — designers compose damage/shields/
// conditions through the standard effect data structure.
//
// Prefab setup:
//   IntentIcon (GameObject) — LayoutElement preferredWidth/Height = 48
//     Image (intent sprite, swapped per intent type at bind time)
//     └─ Label (TMP, shows damage "8", multi-hit "3×4", shields gained, etc.)
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    public class IntentIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label;

        [Header("Icons per Intent Type (swap at bind)")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite defendIcon;
        [SerializeField] private Sprite buffIcon;
        [SerializeField] private Sprite debuffIcon;
        [SerializeField] private Sprite unknownIcon;

        private EnemyIntent intent;
        private Unit source;

        /// <summary>The intent this icon is bound to. Null until Bind() runs.</summary>
        public EnemyIntent Intent => intent;
        /// <summary>The enemy that authored the intent. Null until Bind() runs.</summary>
        public Unit Source => source;

        public void Bind(EnemyIntent intent, Unit source = null)
        {
            this.intent = intent;
            this.source = source;
            Refresh();
        }

        /// <summary>
        /// Re-render the icon + label from the currently bound intent. Called
        /// when the source's conditions change so a Weak / Strength landing
        /// mid-turn updates the previewed damage number.
        /// </summary>
        public void Refresh()
        {
            if (intent == null)
            {
                if (iconImage != null) iconImage.enabled = false;
                if (label != null) label.text = "";
                return;
            }

            switch (intent.intentType)
            {
                case EnemyIntentType.Attack:
                    SetIcon(attackIcon);
                    if (label != null) label.text = ComputeAttackLabel(intent.effects, source);
                    break;

                case EnemyIntentType.Guard:
                    SetIcon(defendIcon);
                    if (label != null) label.text = ComputeShieldsLabel(intent.effects);
                    break;

                case EnemyIntentType.Buff:
                    SetIcon(buffIcon);
                    if (label != null) label.text = ComputeStacksLabel(intent.effects);
                    break;

                case EnemyIntentType.Debuff:
                    SetIcon(debuffIcon);
                    if (label != null) label.text = ComputeStacksLabel(intent.effects);
                    break;

                default:
                    SetIcon(unknownIcon);
                    if (label != null) label.text = "?";
                    break;
            }
        }

        /// <summary>
        /// Inspect the effect chain for Attack effects and produce the icon
        /// label. Multi-hit comes from two sources — a single Attack effect
        /// with hitCount > 1 ("3×6"), OR multiple identical Attack effects
        /// authored in series. Both fold into the same total-hit count, and
        /// when every contributing strike shares the same magnitude+stat we
        /// render as "N×M" (with N = total strikes, M = post-modifier per-hit
        /// damage). Mixed magnitudes fall back to a single total-damage
        /// number. Damage is previewed through DamageCalculator so a Weak /
        /// Strength stack landing on the source updates the label live.
        /// </summary>
        private static string ComputeAttackLabel(SkillEffectData[] effects, Unit src)
        {
            if (effects == null || effects.Length == 0) return "";

            int contributingEffects = 0;
            int totalHits = 0;
            int firstMag = 0;
            DamageStat firstStat = DamageStat.POW;
            bool allSame = true;
            int totalDamage = 0;

            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                if (e == null || e.effectType != SkillEffectType.Attack) continue;

                int hits = Mathf.Max(1, e.hitCount);

                if (contributingEffects == 0)
                {
                    firstMag = e.magnitude;
                    firstStat = e.damageStat;
                }
                else if (e.magnitude != firstMag || e.damageStat != firstStat)
                {
                    allSame = false;
                }

                int perHit = src != null
                    ? DamageCalculator.PreviewOutgoingDamage(e.magnitude, src, e.damageStat)
                    : e.magnitude;
                totalDamage += perHit * hits;
                totalHits += hits;
                contributingEffects++;
            }

            if (contributingEffects == 0) return "";

            if (totalHits > 1 && allSame)
            {
                int perPreview = src != null
                    ? DamageCalculator.PreviewOutgoingDamage(firstMag, src, firstStat)
                    : firstMag;
                return $"{totalHits}×{perPreview}";
            }
            return totalDamage.ToString();
        }

        /// <summary>
        /// Sum stacks across all effects that apply the Shields condition to
        /// Self (the canonical "Guard for N" authoring).
        /// </summary>
        private static string ComputeShieldsLabel(SkillEffectData[] effects)
        {
            if (effects == null) return "";
            int total = 0;
            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                if (e == null) continue;
                bool isShields = e.conditionID == ConditionID.Shields
                    && (e.effectType == SkillEffectType.Apply
                        || e.effectType == SkillEffectType.ApplyCondition);
                if (isShields)
                    total += Mathf.Max(0, e.conditionStacks);
            }
            return total > 0 ? total.ToString() : "";
        }

        /// <summary>
        /// Total condition stacks across condition-applying effects. Used for
        /// Buff / Debuff icons so a "+1 Strength + 1 Tough" intent still
        /// shows a meaningful number.
        /// </summary>
        private static string ComputeStacksLabel(SkillEffectData[] effects)
        {
            if (effects == null) return "";
            int total = 0;
            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                if (e == null) continue;
                if (!e.AppliesCondition) continue;
                total += Mathf.Max(0, e.conditionStacks);
            }
            return total > 1 ? $"×{total}" : "";
        }

        private void SetIcon(Sprite s)
        {
            if (iconImage == null) return;
            iconImage.sprite = s;
            iconImage.enabled = s != null;
        }

        // ─── Hover → DangerPreviewController ────────────────────────────────
        // Drives the red/purple "in danger" auras over players this intent
        // would target. Per-intent precision: hover each intent icon
        // independently to preview that intent alone.

        public void OnPointerEnter(PointerEventData _)
        {
            if (intent == null || source == null || !source.IsAlive) return;
            DangerPreviewController.Instance?.ShowFor(source, intent);
        }

        public void OnPointerExit(PointerEventData _)
        {
            DangerPreviewController.Instance?.Hide();
        }
    }
}
