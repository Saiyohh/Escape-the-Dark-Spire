// ConditionIconUI.cs
// -----------------------------------------------------------------------------
// One condition icon rendered inside a HorizontalLayoutGroup on a unit's HUD.
// Bound to a ConditionData SO (for icon + colors) and shows the current stack
// count as a small number badge.
//
// If the ConditionData has iconGrayscale = true, the icon is rendered through
// the DarkSpire/UI/BlackAndWhite shader with the data's six channel weights —
// same per-hue mix used by the skill info panel. Keep placeholder art
// desaturated cheaply without a Photoshop pass.
//
// Prefab setup:
//   ConditionIcon (GameObject) — LayoutElement preferredWidth/Height = 32
//     Image    (the icon sprite; swapped at bind time from ConditionData.icon)
//     ├─ StackText (TMP, bottom-right, shows "×3")
//     └─ Background (optional — Image with buff/debuff tint)
//
// Wire iconImage + stackText + backgroundImage + blackAndWhiteMaterial, save.
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class ConditionIconUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text stackText;

        [Tooltip("Source material for the per-hue Black & White adjust. Create a material " +
                 "from DarkSpire/UI/BlackAndWhite and drop it here. The icon clones it on " +
                 "first use so per-condition weights don't bleed into the shared asset. " +
                 "Leave null to skip grayscale entirely.")]
        [SerializeField] private Material blackAndWhiteMaterial;

        private ConditionData data;
        private Material runtimeBWMaterial;

        /// <summary>The currently-bound condition (or null before Bind has run).
        /// Read by ConditionTooltipTrigger to populate the hover tooltip.</summary>
        public ConditionData Data => data;

        private static readonly int PropWR = Shader.PropertyToID("_WR");
        private static readonly int PropWY = Shader.PropertyToID("_WY");
        private static readonly int PropWG = Shader.PropertyToID("_WG");
        private static readonly int PropWC = Shader.PropertyToID("_WC");
        private static readonly int PropWB = Shader.PropertyToID("_WB");
        private static readonly int PropWM = Shader.PropertyToID("_WM");

        /// <summary>Assign the SO — updates sprite, tint, grayscale, and tooltip data.</summary>
        public void Bind(ConditionData data)
        {
            this.data = data;
            if (data == null) return;

            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = data.icon != null;

                // Apply the BlackAndWhite material when the condition opts in.
                // Clone once so each icon instance has its own per-hue weights.
                if (data.iconGrayscale && blackAndWhiteMaterial != null)
                {
                    if (runtimeBWMaterial == null)
                        runtimeBWMaterial = new Material(blackAndWhiteMaterial) { hideFlags = HideFlags.DontSave };

                    runtimeBWMaterial.SetFloat(PropWR, data.iconBwReds);
                    runtimeBWMaterial.SetFloat(PropWY, data.iconBwYellows);
                    runtimeBWMaterial.SetFloat(PropWG, data.iconBwGreens);
                    runtimeBWMaterial.SetFloat(PropWC, data.iconBwCyans);
                    runtimeBWMaterial.SetFloat(PropWB, data.iconBwBlues);
                    runtimeBWMaterial.SetFloat(PropWM, data.iconBwMagentas);

                    iconImage.material = runtimeBWMaterial;
                }
                else
                {
                    iconImage.material = null;
                }
            }
            // backgroundImage color is intentionally NOT overwritten — the
            // prefab's authored color is the source of truth. If you want
            // buff/debuff tinting from the library, drop an ApplyLibraryColor
            // component on the background Image (or split prefabs by isDebuff).
        }

        public void SetStacks(int stacks)
        {
            if (stackText == null) return;
            // Hide the label entirely if stacks ≤ 1 (single-instance buffs don't need "×1").
            stackText.text = stacks > 1 ? $"×{stacks}" : "";
        }

        private void OnDestroy()
        {
            if (runtimeBWMaterial != null)
            {
                Destroy(runtimeBWMaterial);
                runtimeBWMaterial = null;
            }
        }
    }
}
