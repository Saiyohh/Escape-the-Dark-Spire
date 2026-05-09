// ItemInfoPanelUI.cs
// -----------------------------------------------------------------------------
// Detail readout for whichever item is currently hovered in the ItemSubmenuUI.
// Shows the banner art (masked), item name, action-cost badge, target badge,
// optional charges badge, and the effect text.
//
// Driven by ItemSubmenuUI.ShowInfo(item). Sibling of the card list, not a
// child — so hovering a card doesn't de-hover by moving onto the info panel.
//
// Mirrors SkillInfoPanelUI structurally — same banner-fit math, same empty
// state — but with item-specific badges (action cost / target / charges)
// instead of skill-specific (SP / range / stars).
//
// Prefab setup:
//   ItemInfoPanel (GameObject)
//     Image (background)
//     ├─ BannerMask (RectTransform + Image + Mask) — wide rectangle
//     │   └─ Banner (Image — sprite set at Bind from ItemData.GetArtBanner)
//     ├─ Header (horizontal)
//     │   ├─ Name (TMP, left)
//     │   ├─ ActionCostBadge (rounded Image tinted + glyph TMP inside)
//     │   ├─ TargetBadge (rounded Image tinted + target label inside)
//     │   └─ ChargesBadge (rounded Image tinted + "×3" TMP — optional)
//     └─ Description (TMP, wordwrap)
// -----------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class ItemInfoPanelUI : MonoBehaviour
    {
        [Header("Banner")]
        [SerializeField] private Image bannerImage;
        [SerializeField] private RectTransform bannerMask;

        [Header("Header")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text actionCostLabel;
        [SerializeField] private TMP_Text targetLabel;

        [Tooltip("Optional charges pill — shown when the item is stackable and " +
                 "the held instance has charges > 1.")]
        [SerializeField] private TMP_Text chargesLabel;
        [SerializeField] private GameObject chargesContainer;

        [Tooltip("Optional category chip — Pouch / Party / Brewed. Helps " +
                 "designers QA the inventory category at a glance.")]
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private GameObject categoryContainer;

        [Header("Description")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Header("Empty State")]
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject contentRoot;

        public void Show(ItemData item) => Show(item, instance: null);

        /// <summary>
        /// Show details for the given item. Pass an ItemInstance when known
        /// to surface the live charges count; pass null in non-combat
        /// contexts (e.g. the dungeon inventory panel can call the simpler
        /// overload).
        /// </summary>
        public void Show(ItemData item, ItemInstance instance)
        {
            if (item == null) { ShowEmpty(); return; }

            if (emptyState != null)  emptyState.SetActive(false);
            if (contentRoot != null) contentRoot.SetActive(true);

            if (bannerImage != null)
            {
                var banner = item.GetArtBanner();
                bannerImage.sprite = banner;
                bannerImage.enabled = banner != null;
                ApplyBannerFit(banner, item.bannerFocalX, item.bannerFocalY);
            }

            if (nameLabel != null)
                nameLabel.text = item.itemName;

            if (actionCostLabel != null)
                actionCostLabel.text = ActionCostName(item.actionCostType);

            if (targetLabel != null)
                targetLabel.text = TargetName(item.primaryTargetMode);

            int charges = instance != null ? instance.charges : 1;
            bool showCharges = item.stackable && charges > 1;
            if (chargesLabel != null)
            {
                chargesLabel.text = $"×{charges}";
                chargesLabel.gameObject.SetActive(showCharges);
            }
            if (chargesContainer != null) chargesContainer.SetActive(showCharges);

            if (categoryLabel != null) categoryLabel.text = CategoryName(item.category);
            if (categoryContainer != null) categoryContainer.SetActive(true);

            if (descriptionLabel != null)
            {
                string text = string.IsNullOrEmpty(item.effectText)
                    ? item.BuildEffectText()
                    : item.effectText;
                descriptionLabel.text = text;
            }
        }

        public void ShowEmpty()
        {
            if (emptyState != null)  emptyState.SetActive(true);
            if (contentRoot != null) contentRoot.SetActive(false);

            if (bannerImage != null)
            {
                bannerImage.sprite = null;
                bannerImage.enabled = false;
                bannerImage.material = null;
            }
            if (nameLabel != null)        nameLabel.text = "";
            if (actionCostLabel != null)  actionCostLabel.text = "";
            if (targetLabel != null)      targetLabel.text = "";
            if (descriptionLabel != null) descriptionLabel.text = "";
            if (chargesContainer != null) chargesContainer.SetActive(false);
        }

        // ── Banner fit (mirrors SkillInfoPanelUI.ApplyBannerFit) ────────────
        private void ApplyBannerFit(Sprite sprite, float focalX, float focalY)
        {
            if (bannerImage == null || bannerMask == null || sprite == null) return;

            var bannerRT = bannerImage.rectTransform;
            float maskW  = bannerMask.rect.width;
            float maskH  = bannerMask.rect.height;
            float imageW = bannerRT.rect.width;
            float imageH = bannerRT.rect.height;

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

        // ── Display helpers ─────────────────────────────────────────────────
        private static string ActionCostName(ItemActionCostType c) => c switch
        {
            ItemActionCostType.Action     => "Action",
            ItemActionCostType.FreeAction => "Free Action",
            ItemActionCostType.ZeroCost   => "Zero Cost",
            _ => c.ToString(),
        };

        private static string TargetName(TargetMode t) => t switch
        {
            TargetMode.Self        => "Self",
            TargetMode.SingleAlly  => "Ally",
            TargetMode.AllAllies   => "All Allies",
            TargetMode.RandomAlly  => "Random Ally",
            TargetMode.WholeParty  => "Whole Party",
            TargetMode.SingleEnemy => "Enemy",
            TargetMode.AllEnemies  => "All Enemies",
            TargetMode.RandomEnemy => "Random Enemy",
            _ => t.ToString(),
        };

        private static string CategoryName(ItemCategory c) => c switch
        {
            ItemCategory.Pouch          => "Pouch",
            ItemCategory.PartyInventory => "Party",
            ItemCategory.BrewedPotion   => "Brewed",
            _ => c.ToString(),
        };
    }
}
