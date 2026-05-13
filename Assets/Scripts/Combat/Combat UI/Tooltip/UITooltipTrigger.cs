using UnityEngine.EventSystems;

namespace DarkSpire
{
    public abstract class UITooltipTrigger : TooltipTrigger,
        IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => ShowTooltip();
        public void OnPointerExit(PointerEventData eventData)  => HideTooltip();
    }
}
