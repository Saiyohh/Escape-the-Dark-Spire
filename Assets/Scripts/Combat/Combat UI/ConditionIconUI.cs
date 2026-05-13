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
        private int stacks;
        private Material runtimeBWMaterial;

        public ConditionData Data => data;

        public int Stacks => stacks;

        private static readonly int PropWR = Shader.PropertyToID("_WR");
        private static readonly int PropWY = Shader.PropertyToID("_WY");
        private static readonly int PropWG = Shader.PropertyToID("_WG");
        private static readonly int PropWC = Shader.PropertyToID("_WC");
        private static readonly int PropWB = Shader.PropertyToID("_WB");
        private static readonly int PropWM = Shader.PropertyToID("_WM");

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
            this.stacks = stacks;
            if (stackText == null) return;
            // Show the bare count whenever the condition is active. Bare number
            // (no "×" prefix) matches standard card-game convention and reads
            // unambiguously — players just see "3" overlaid on the Guard icon.
            // Hidden only when stacks drop to 0 (about to be removed).
            stackText.text = stacks >= 1 ? stacks.ToString() : "";
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
