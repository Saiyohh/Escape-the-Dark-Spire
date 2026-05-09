// SkillInfoPanelUI.cs
// -----------------------------------------------------------------------------
// Detail readout for whichever skill is currently hovered in the SkillSubmenuUI.
// Shows the banner art (masked to a wide rectangle), skill name, Range badge,
// SP badge, and description.
//
// Driven by SkillSubmenuUI.ShowInfo(skill). The panel is a sibling of the
// card list, not a child — so hovering a card in the list doesn't de-hover
// itself by moving the pointer onto the info panel.
//
// Prefab setup:
//   SkillInfoPanel (GameObject)
//     Image (background)
//     ├─ BannerMask (RectTransform + Image + Mask) — wide rectangle
//     │   └─ Banner (Image — sprite set at Bind from SkillData.GetArtBanner)
//     ├─ Header (horizontal)
//     │   ├─ Name (TMP, left)
//     │   ├─ RangeBadge (rounded Image tinted + "Range: 4-6" TMP inside)
//     │   └─ SpBadge (rounded Image tinted + "SP: 3" TMP inside)
//     └─ Description (TMP, wordwrap)
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class SkillInfoPanelUI : MonoBehaviour
    {
        [Header("Banner")]
        [Tooltip("The Image that holds the skill's art. Gets resized to mask width and " +
                 "offset vertically per skill.bannerFocalY.")]
        [SerializeField] private Image bannerImage;

        [Tooltip("The RectTransform of the mask wrapping bannerImage. Used to measure " +
                 "the available mask size for auto-scaling + focal offset.")]
        [SerializeField] private RectTransform bannerMask;

        [Tooltip("Optional upgrade-progress fill behind the banner. Image.type should " +
                 "be Filled (vertical bottom-to-top is the natural choice). Driven by " +
                 "masteryProgress01 in Show() / SetMasteryProgress().")]
        [SerializeField] private Image upgradeFill;

        [Tooltip("Source material for the per-hue Black & White adjust. Create a " +
                 "material from DarkSpire/UI/BlackAndWhite and drop it here. The " +
                 "panel clones it on first use so each skill's 6 channel weights " +
                 "don't bleed into a shared asset. Leave null to disable BW entirely.")]
        [SerializeField] private Material blackAndWhiteMaterial;

        // Runtime clone — mutated per skill with that skill's channel weights.
        private Material runtimeBWMaterial;

        private static readonly int PropWR = Shader.PropertyToID("_WR");
        private static readonly int PropWY = Shader.PropertyToID("_WY");
        private static readonly int PropWG = Shader.PropertyToID("_WG");
        private static readonly int PropWC = Shader.PropertyToID("_WC");
        private static readonly int PropWB = Shader.PropertyToID("_WB");
        private static readonly int PropWM = Shader.PropertyToID("_WM");

        [Header("Header")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text rangeLabel;
        [SerializeField] private TMP_Text spLabel;
        [Tooltip("Optional Stars-cost label. Shown only when skill.starCost > 0.")]
        [SerializeField] private TMP_Text starCostLabel;
        [Tooltip("Optional pill container shown alongside starCostLabel.")]
        [SerializeField] private GameObject starCostContainer;

        [Header("Description")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Header("Empty State")]
        [Tooltip("Root that toggles on when no skill is selected. Typically a " +
                 "small 'hover a skill to see details' label.")]
        [SerializeField] private GameObject emptyState;

        [Tooltip("Root that toggles on when a skill IS selected. The banner + " +
                 "header + description.")]
        [SerializeField] private GameObject contentRoot;

        public void Show(SkillData skill, float masteryProgress01 = 0f) =>
            Show(skill, caster: null, masteryProgress01);

        /// <summary>
        /// Detail-render variant that wires the rich-text DescriptionRenderer
        /// against a caster unit so number values, color coding, keyword
        /// tooltips, and live target-hover updates all work. Pass <c>null</c>
        /// for caster from non-combat contexts; numbers fall back to their
        /// magnitude with no scaling and default coloring.
        /// </summary>
        public void Show(SkillData skill, Unit caster, float masteryProgress01 = 0f)
        {
            if (skill == null) { ShowEmpty(); return; }

            if (emptyState != null)   emptyState.SetActive(false);
            if (contentRoot != null)  contentRoot.SetActive(true);

            SetMasteryProgress(masteryProgress01);

            if (bannerImage != null)
            {
                var banner = skill.GetArtBanner();
                bannerImage.sprite = banner;
                bannerImage.enabled = banner != null;
                ApplyBannerFit(banner, skill.bannerFocalX, skill.bannerFocalY);

                // Material swap: apply the 6-channel B&W adjust when the skill
                // opts in. Clone the source material once so per-skill weight
                // changes don't stomp the shared asset, then update its floats.
                if (skill.bannerGrayscale && blackAndWhiteMaterial != null)
                {
                    if (runtimeBWMaterial == null)
                        runtimeBWMaterial = new Material(blackAndWhiteMaterial) { hideFlags = HideFlags.DontSave };

                    runtimeBWMaterial.SetFloat(PropWR, skill.bwReds);
                    runtimeBWMaterial.SetFloat(PropWY, skill.bwYellows);
                    runtimeBWMaterial.SetFloat(PropWG, skill.bwGreens);
                    runtimeBWMaterial.SetFloat(PropWC, skill.bwCyans);
                    runtimeBWMaterial.SetFloat(PropWB, skill.bwBlues);
                    runtimeBWMaterial.SetFloat(PropWM, skill.bwMagentas);

                    bannerImage.material = runtimeBWMaterial;
                }
                else
                {
                    bannerImage.material = null;
                }
            }
            if (nameLabel != null)
                nameLabel.text = skill.skillName;

            if (rangeLabel != null)
            {
                string r = string.IsNullOrEmpty(skill.rangeDisplay)
                    ? $"{skill.rangeMin}-{skill.rangeMax}"
                    : skill.rangeDisplay;
                rangeLabel.text = r;
            }

            if (spLabel != null)
                spLabel.text = skill.spCost.ToString();

            // Stars cost — only shown for skills that actually cost stars.
            bool showStars = skill.starCost > 0;
            if (starCostLabel != null)
            {
                starCostLabel.text = skill.starCost.ToString();
                starCostLabel.gameObject.SetActive(showStars);
            }
            if (starCostContainer != null) starCostContainer.SetActive(showStars);

            if (descriptionLabel != null)
            {
                // Bind the structured-token description through DescriptionRenderer.
                // Auto-attach if the prefab wasn't already authored with one — keeps
                // existing prefab assets working without a re-save.
                var renderer = descriptionLabel.GetComponent<DescriptionRenderer>();
                if (renderer == null)
                    renderer = descriptionLabel.gameObject.AddComponent<DescriptionRenderer>();
                renderer.SetSkill(skill, caster);
            }
        }

        /// <summary>
        /// Adjust the banner Image's anchored position so the focal point
        /// (focalX, focalY) controls which slice of the authored image shows
        /// through the mask. Both the mask AND the banner image's authored
        /// size are respected — this method does NOT resize anything.
        ///
        /// Only the axis where the authored image exceeds the mask gets
        /// offset; the other axis stays at 0.
        ///   image taller than mask → focalY picks vertical slice
        ///   image wider than mask  → focalX picks horizontal slice
        /// </summary>
        private void ApplyBannerFit(Sprite sprite, float focalX, float focalY)
        {
            if (bannerImage == null || bannerMask == null || sprite == null) return;

            var bannerRT = bannerImage.rectTransform;
            float maskW  = bannerMask.rect.width;
            float maskH  = bannerMask.rect.height;
            float imageW = bannerRT.rect.width;
            float imageH = bannerRT.rect.height;

            // Per-axis focal offset based on overflow. Negative sign on the
            // focal so +1 exposes the top/right edge of the sprite (image
            // scrolls down/left to reveal the corresponding side).
            float overflowX = imageW - maskW;
            float overflowY = imageH - maskH;
            float xOff = overflowX > 0f
                ? -Mathf.Clamp(focalX, -1f, 1f) * overflowX * 0.5f
                : 0f;
            float yOff = overflowY > 0f
                ? -Mathf.Clamp(focalY, -1f, 1f) * overflowY * 0.5f
                : 0f;
            bannerRT.anchoredPosition = new Vector2(xOff, yOff);
        }

        /// <summary>
        /// Drive the upgrade-progress fill amount (0..1). Wires straight into
        /// Image.fillAmount on the upgradeFill slot. Caller is responsible for
        /// computing the ratio from current mastery count / threshold.
        /// </summary>
        public void SetMasteryProgress(float progress01)
        {
            if (upgradeFill == null) return;
            upgradeFill.fillAmount = Mathf.Clamp01(progress01);
        }

        public void ShowEmpty()
        {
            if (emptyState != null)   emptyState.SetActive(true);
            if (contentRoot != null)  contentRoot.SetActive(false);

            // Belt + suspenders: clear every field even when emptyState /
            // contentRoot slots aren't wired, so a fresh panel reads as
            // 'no skill selected' instead of stale authored placeholder text.
            if (bannerImage != null)
            {
                bannerImage.sprite = null;
                bannerImage.enabled = false;
                bannerImage.material = null;
            }
            if (nameLabel != null)        nameLabel.text = "";
            if (rangeLabel != null)       rangeLabel.text = "";
            if (spLabel != null)          spLabel.text = "";
            if (descriptionLabel != null)
            {
                var renderer = descriptionLabel.GetComponent<DescriptionRenderer>();
                if (renderer != null) renderer.SetPlainText(string.Empty);
                else descriptionLabel.text = "";
            }
            SetMasteryProgress(0f);
        }
    }
}
