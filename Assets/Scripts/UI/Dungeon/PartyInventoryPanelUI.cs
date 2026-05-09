// PartyInventoryPanelUI.cs
// -----------------------------------------------------------------------------
// Read-only dungeon HUD panel listing the party's inventory:
//   • Section: Party Inventory  — RunContext.partyInventory (shared bag)
//   • Section: Pouches           — RunContext.partyState[i].pouchItems per char
//
// V1 scope: read-only display. The player can see what items they have but
// can't drop, transfer, or rearrange — those interactions land in a future
// pass. Hover on any row populates a paired ItemInfoPanelUI with the full
// details (banner + cost + description).
//
// Toggle visibility via Open() / Close() (or Toggle()) — bind a HUD button
// or hotkey at the project level. The panel doesn't subscribe to any combat
// events; it pulls fresh state from RunContext / PartyMemberRuntime each
// time Open() is called.
//
// Sibling of FloorHUD's PartyHpBar / Minimap on the dungeon canvas.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DarkSpire
{
    public class PartyInventoryPanelUI : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("VerticalLayoutGroup that hosts the Party Inventory header + " +
                 "row instances. Cleared and repopulated on every Open().")]
        [SerializeField] private RectTransform partySectionContainer;

        [Tooltip("VerticalLayoutGroup that hosts one collapsible block per " +
                 "party member's pouch.")]
        [SerializeField] private RectTransform pouchesSectionContainer;

        [Tooltip("Prefab for an item row — must have an ItemRowUI component on " +
                 "its root. Used in both the party section and pouch sections.")]
        [SerializeField] private GameObject itemRowPrefab;

        [Tooltip("Prefab for a pouch header — a TMP_Text label root displaying " +
                 "the character name. Spawned once per party member that has a " +
                 "pouch (even if empty), to make ownership clear.")]
        [SerializeField] private GameObject pouchHeaderPrefab;

        [Tooltip("Optional info panel that updates on hover. Reuses the combat " +
                 "ItemInfoPanelUI prefab.")]
        [SerializeField] private ItemInfoPanelUI infoPanel;

        [Tooltip("Optional 'inventory is empty' label shown when both the " +
                 "party bag and every pouch are empty.")]
        [SerializeField] private GameObject emptyState;

        private readonly List<GameObject> spawned = new();

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (panelRoot != gameObject) panelRoot.SetActive(false);
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }
        }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        public void Open()
        {
            if (panelRoot == null) panelRoot = gameObject;
            Repopulate();
            panelRoot.SetActive(true);
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(true);
            }
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // ── Population ──────────────────────────────────────────────────────
        private void Repopulate()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();

            int itemsRendered = 0;

            // Party Inventory section
            if (partySectionContainer != null)
            {
                var party = RunContext.partyInventory;
                if (party != null)
                {
                    for (int i = 0; i < party.Count; i++)
                    {
                        var inst = party[i];
                        if (inst == null) continue;
                        var data = inst.Resolve();
                        if (data == null) continue;
                        SpawnRow(partySectionContainer, data, inst);
                        itemsRendered++;
                    }
                }
            }

            // Pouches section — one block per party member.
            if (pouchesSectionContainer != null && RunContext.partyState != null)
            {
                for (int p = 0; p < RunContext.partyState.Length; p++)
                {
                    var pm = RunContext.partyState[p];
                    if (pm == null || pm.characterData == null) continue;

                    SpawnPouchHeader(pm.characterData.characterName);

                    if (pm.pouchItems == null || pm.pouchItems.Count == 0)
                    {
                        SpawnPouchEmptyLabel();
                        continue;
                    }
                    for (int i = 0; i < pm.pouchItems.Count; i++)
                    {
                        var inst = pm.pouchItems[i];
                        if (inst == null) continue;
                        var data = inst.Resolve();
                        if (data == null) continue;
                        SpawnRow(pouchesSectionContainer, data, inst);
                        itemsRendered++;
                    }
                }
            }

            if (emptyState != null) emptyState.SetActive(itemsRendered == 0);
        }

        private void SpawnRow(RectTransform parent, ItemData data, ItemInstance inst)
        {
            if (itemRowPrefab == null) return;
            var go = Instantiate(itemRowPrefab, parent);
            spawned.Add(go);

            var row = go.GetComponent<ItemRowUI>();
            if (row != null)
            {
                row.Bind(data, inst,
                    onHovered: () => infoPanel?.Show(data, inst),
                    onUnhovered: () => { /* keep last-hovered showing */ });
            }
            else
            {
                // Fallback: if the prefab is just a TMP, fill it in plain text.
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = $"{data.itemName}{(inst.charges > 1 ? $" ×{inst.charges}" : "")}";
            }
        }

        private void SpawnPouchHeader(string characterName)
        {
            if (pouchHeaderPrefab == null) return;
            var go = Instantiate(pouchHeaderPrefab, pouchesSectionContainer);
            spawned.Add(go);
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{characterName}'s Pouch";
        }

        private void SpawnPouchEmptyLabel()
        {
            if (pouchesSectionContainer == null) return;
            var go = new GameObject("PouchEmpty", typeof(RectTransform));
            go.transform.SetParent(pouchesSectionContainer, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = "  (empty)";
            label.fontSize = 12f;
            label.color = new Color(1f, 1f, 1f, 0.5f);
            spawned.Add(go);
        }
    }

    /// <summary>
    /// Row component for a single inventory entry. Designer authors a prefab
    /// with an Image (icon), TMP_Text (name + optional charges), and this
    /// component on the root. Bind() wires up hover callbacks for the info
    /// panel.
    /// </summary>
    public class ItemRowUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text chargesLabel;

        private System.Action onHovered;
        private System.Action onUnhovered;

        public void Bind(ItemData data, ItemInstance inst,
            System.Action onHovered, System.Action onUnhovered)
        {
            this.onHovered   = onHovered;
            this.onUnhovered = onUnhovered;

            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = data.icon != null;
            }
            if (nameLabel != null) nameLabel.text = data.itemName;
            if (chargesLabel != null)
            {
                bool show = data.stackable && inst.charges > 1;
                chargesLabel.gameObject.SetActive(show);
                if (show) chargesLabel.text = $"×{inst.charges}";
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke();
        public void OnPointerExit(PointerEventData eventData)  => onUnhovered?.Invoke();
    }
}
