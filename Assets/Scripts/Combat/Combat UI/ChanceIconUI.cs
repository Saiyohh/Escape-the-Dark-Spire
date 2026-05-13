using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class ChanceIconUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text percentLabel;

        public void Bind(Sprite sprite, Color tint, float percent01)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = tint;
                iconImage.enabled = sprite != null;
            }
            if (percentLabel != null)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(percent01) * 100f);
                percentLabel.text = $"{pct}%";
            }
        }
    }
}
