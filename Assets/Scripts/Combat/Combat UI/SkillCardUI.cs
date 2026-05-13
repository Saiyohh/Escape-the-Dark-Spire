using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    public class SkillCardUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("Optional Stars-cost label. Shown only when skill.starCost > 0.")]
        [SerializeField] private TMP_Text starCostLabel;
        [Tooltip("Optional container for the Stars-cost pill — toggled visible " +
                 "alongside starCostLabel. If unset, only the label is toggled.")]
        [SerializeField] private GameObject starCostContainer;
        [SerializeField] private Button button;

        [Tooltip("Optional CanvasGroup used to visually 'disable' the card when " +
                 "the skill can't be played. Lowered alpha communicates the " +
                 "greyed-out state while keeping the button clickable so we " +
                 "can surface a refusal speech bubble on click. If unset, the " +
                 "button stays full-bright when unaffordable.")]
        [SerializeField] private CanvasGroup interactCanvasGroup;

        [Tooltip("Alpha applied via interactCanvasGroup when the skill can't be played.")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledAlpha = 0.45f;

        public SkillData Skill { get; private set; }
        public Unit Caster { get; private set; }

        private Action onHovered;
        private Action onUnhovered;

        public void Bind(
            Unit caster, SkillData skill,
            Action onClicked, Action onHovered, Action onUnhovered)
        {
            Skill = skill;
            Caster = caster;
            this.onHovered   = onHovered;
            this.onUnhovered = onUnhovered;

            if (skill == null) return;

            if (nameLabel != null) nameLabel.text = skill.skillName;
            if (costLabel != null) costLabel.text = skill.spCost.ToString();

            bool showStars = skill.starCost > 0;
            if (starCostLabel != null)
            {
                starCostLabel.text = skill.starCost.ToString();
                starCostLabel.gameObject.SetActive(showStars);
            }
            if (starCostContainer != null) starCostContainer.SetActive(showStars);

            ActionRefusalReason refusal = caster != null
                ? caster.GetSkillRefusal(skill)
                : ActionRefusalReason.None;
            bool canAfford = refusal == ActionRefusalReason.None;

            if (interactCanvasGroup != null)
                interactCanvasGroup.alpha = canAfford ? 1f : disabledAlpha;

            if (button != null)
            {
                button.interactable = true;
                button.onClick.RemoveAllListeners();
                Action capturedOnClicked = onClicked;
                Unit capturedCaster = caster;
                ActionRefusalReason capturedRefusal = refusal;
                button.onClick.AddListener(() =>
                {
                    if (capturedRefusal != ActionRefusalReason.None)
                    {
                        if (capturedCaster != null)
                        {
                            var disp = UnitDisplay.GetDisplay(capturedCaster);
                            if (disp != null)
                                disp.ShowSpeechBubble(ActionRefusalMessages.For(capturedRefusal, capturedCaster));
                        }
                        return;
                    }
                    capturedOnClicked?.Invoke();
                });
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke();
        public void OnPointerExit(PointerEventData eventData)  => onUnhovered?.Invoke();
    }
}
