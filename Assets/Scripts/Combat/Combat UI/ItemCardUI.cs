using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkSpire
{
    public class ItemCardUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text costLabel;

        [Tooltip("Optional charges pill. Shown when item.stackable && charges > 1.")]
        [SerializeField] private TMP_Text chargesLabel;
        [SerializeField] private GameObject chargesContainer;

        [Tooltip("Optional source chip (Pouch icon for character-locked items, " +
                 "Party icon for shared bag items). Lets the player tell at a " +
                 "glance whether an item is on this character only.")]
        [SerializeField] private GameObject pouchSourceIndicator;
        [SerializeField] private GameObject partySourceIndicator;

        [SerializeField] private Button button;

        [Tooltip("CanvasGroup used to dim the card when the user can't use the " +
                 "item. Lowered alpha communicates the greyed-out state while " +
                 "keeping the button clickable so the refusal bubble can fire.")]
        [SerializeField] private CanvasGroup interactCanvasGroup;

        [Tooltip("Alpha applied via interactCanvasGroup when the item can't be used.")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledAlpha = 0.45f;

        public ItemData Item { get; private set; }
        public Unit User { get; private set; }
        public ItemSource Source { get; private set; }
        public int Index { get; private set; }

        private Action onHovered;
        private Action onUnhovered;

        public void Bind(
            Unit user, ItemData item, ItemInstance instance,
            ItemSource source, int index,
            Action onClicked, Action onHovered, Action onUnhovered)
        {
            Item = item;
            User = user;
            Source = source;
            Index = index;
            this.onHovered   = onHovered;
            this.onUnhovered = onUnhovered;

            if (item == null) return;

            if (nameLabel != null) nameLabel.text = item.itemName;
            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = item.icon != null;
            }

            if (costLabel != null) costLabel.text = ActionCostGlyph(item.actionCostType);

            int charges = instance != null ? instance.charges : 1;
            bool showCharges = item.stackable && charges > 1;
            if (chargesLabel != null)
            {
                chargesLabel.text = $"×{charges}";
                chargesLabel.gameObject.SetActive(showCharges);
            }
            if (chargesContainer != null) chargesContainer.SetActive(showCharges);

            if (pouchSourceIndicator != null) pouchSourceIndicator.SetActive(source == ItemSource.Pouch);
            if (partySourceIndicator != null) partySourceIndicator.SetActive(source == ItemSource.Party);

            ActionRefusalReason refusal = user != null
                ? user.GetItemRefusal(item)
                : ActionRefusalReason.None;
            bool canUse = refusal == ActionRefusalReason.None;

            if (interactCanvasGroup != null)
                interactCanvasGroup.alpha = canUse ? 1f : disabledAlpha;

            if (button != null)
            {
                button.interactable = true;
                button.onClick.RemoveAllListeners();
                Action capturedOnClicked = onClicked;
                Unit capturedUser = user;
                ActionRefusalReason capturedRefusal = refusal;
                button.onClick.AddListener(() =>
                {
                    if (capturedRefusal != ActionRefusalReason.None)
                    {
                        if (capturedUser != null)
                        {
                            var disp = UnitDisplay.GetDisplay(capturedUser);
                            if (disp != null)
                                disp.ShowSpeechBubble(ActionRefusalMessages.For(capturedRefusal, capturedUser));
                        }
                        return;
                    }
                    capturedOnClicked?.Invoke();
                });
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke();
        public void OnPointerExit(PointerEventData eventData)  => onUnhovered?.Invoke();

        private static string ActionCostGlyph(ItemActionCostType cost) => cost switch
        {
            ItemActionCostType.Action     => "A",
            ItemActionCostType.FreeAction => "F",
            ItemActionCostType.ZeroCost   => "0",
            _ => "?",
        };
    }
}
