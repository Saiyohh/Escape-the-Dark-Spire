// ApplyLibraryColor.cs
// -----------------------------------------------------------------------------
// Drop on a Graphic (Image, RawImage, TMP_Text, etc.) to tint it from
// ColorLibrary by Category + Name at runtime. Lets HUD bars, badges, and
// any other "needs a project-wide color" Graphic stay in sync with the
// library without bespoke wiring.
//
//   ApplyLibraryColor:
//     Category = "HUD"
//     Name     = "HpFill"
//
// Runtime Awake reads the library and sets graphic.color.
// In edit mode (ExecuteAlways) the color updates live too — useful while
// authoring prefabs against the library.
//
// Falls back gracefully when the library or key is missing — in that case
// the Graphic's authored color stays as-is.
// -----------------------------------------------------------------------------
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

            // Fallback to the existing color so a missing library / key doesn't
            // silently flash white over authored prefab colors.
            Color fallback = target != null ? target.color
                             : textTarget != null ? textTarget.color
                             : Color.white;

            if (!ColorLibrary.TryGet(category, colorName, out var c))
                c = fallback;

            if (target != null)     target.color = c;
            if (textTarget != null) textTarget.color = c;
        }

        /// <summary>Re-fetch from the library — call after editing the library at runtime.</summary>
        public void Refresh() => Apply();
    }
}
