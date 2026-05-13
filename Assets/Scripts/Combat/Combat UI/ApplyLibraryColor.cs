using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ApplyLibraryColor : MonoBehaviour
    {
        [Tooltip("ColorLibrary category, e.g. 'HUD' or 'UI'.")]
        [SerializeField] private string category = "HUD";

        [Tooltip("ColorLibrary entry name within the category, e.g. 'HpFill'.")]
        [SerializeField] private string colorName = "HpFill";

        [Tooltip("Optional: target Graphic. Auto-resolved from this GameObject if empty.")]
        [SerializeField] private Graphic target;

        [Tooltip("Optional: target TMP_Text. Auto-resolved from this GameObject if empty. " +
                 "Useful when the same component should drive both an Image and a label.")]
        [SerializeField] private TMP_Text textTarget;

        private void OnEnable()  => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            if (this == null) return;
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(colorName)) return;

            if (target == null) target = GetComponent<Graphic>();
            if (textTarget == null) textTarget = GetComponent<TMP_Text>();

            Color fallback = target != null ? target.color
                             : textTarget != null ? textTarget.color
                             : Color.white;

            if (!ColorLibrary.TryGet(category, colorName, out var c))
                c = fallback;

            if (target != null)     target.color = c;
            if (textTarget != null) textTarget.color = c;
        }

        public void Refresh() => Apply();
    }
}
