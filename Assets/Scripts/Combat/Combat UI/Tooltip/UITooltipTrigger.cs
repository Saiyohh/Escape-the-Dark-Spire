// UITooltipTrigger.cs
// -----------------------------------------------------------------------------
// Concrete tooltip trigger base for UGUI elements. Detects hover via Unity's
// EventSystem pointer interfaces — works on any RectTransform under a Canvas
// with a GraphicRaycaster, provided the element has a Graphic with Raycast
// Target on (or an ancestor that captures the ray).
//
// For world-space sprite elements (the Defect's orb slots are SpriteRenderers,
// not UI), use WorldTooltipTrigger instead — it polls Physics2D.OverlapPoint
// because IPointerEnterHandler doesn't fire on world sprites without a
// Physics2DRaycaster, which the project doesn't use.
// -----------------------------------------------------------------------------
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
