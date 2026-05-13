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

        public EnemyIntent Intent => intent;
        public Unit Source => source;

        public void Bind(EnemyIntent intent, Unit source = null)
        {
            this.intent = intent;
            this.source = source;
            Refresh();
        }

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
