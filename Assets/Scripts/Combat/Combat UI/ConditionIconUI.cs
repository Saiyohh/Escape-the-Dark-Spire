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
        }

        public void SetStacks(int stacks)
        {
            this.stacks = stacks;
            if (stackText == null) return;
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
