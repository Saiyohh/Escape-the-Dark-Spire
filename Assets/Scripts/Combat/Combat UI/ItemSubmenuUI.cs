using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class ItemSubmenuUI : MonoBehaviour
    {
        public event Action OnOpened;
        public event Action OnClosed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        [SerializeField] private RectTransform cardsContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private ItemInfoPanelUI infoPanel;

        [Tooltip("The panel root — toggled active on Open / Close. Defaults to this gameObject if null.")]
        [SerializeField] private GameObject panelRoot;

        private readonly List<ItemCardUI> spawned = new();
        private Unit boundUnit;
        private Transform arrowOrigin;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }
            if (panelRoot != gameObject)
                panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            CombatEvents.OnUnitTurnEnd              += HandleTurnEnd;
            CombatEvents.OnSkillTargetingCancelled  += HandleTargetingCancelled;
            CombatEvents.OnActionStateChanged       += HandleActionStateChanged;
            CombatEvents.OnActionResolved           += HandleActionResolved;
        }

        private void OnDisable()
        {
            CombatEvents.OnUnitTurnEnd              -= HandleTurnEnd;
            CombatEvents.OnSkillTargetingCancelled  -= HandleTargetingCancelled;
            CombatEvents.OnActionStateChanged       -= HandleActionStateChanged;
            CombatEvents.OnActionResolved           -= HandleActionResolved;
        }

        public void OpenFor(Unit unit, Transform arrowOrigin)
        {
            if (panelRoot == null) panelRoot = gameObject;

            if (unit == null)
            {
                Debug.LogWarning("[ItemSubmenuUI] OpenFor called with a null unit.", this);
                return;
            }
            if (cardsContainer == null)
            {
                Debug.LogWarning(
                    "[ItemSubmenuUI] cardsContainer is not wired — assign the " +
                    "VerticalLayoutGroup that holds item cards.", this);
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning(
                    "[ItemSubmenuUI] cardPrefab is not wired — assign the " +
                    "ItemCard prefab.", this);
                return;
            }

            boundUnit = unit;
            this.arrowOrigin = arrowOrigin;

            Repopulate();

            bool wasOpen = panelRoot.activeSelf;
            panelRoot.SetActive(true);

            BackButton.Instance?.Bind(this, Close);

            if (infoPanel != null)
            {
                infoPanel.gameObject.SetActive(true);
                ItemData firstItem = spawned.Count > 0 ? spawned[0].Item : null;
                if (firstItem != null) infoPanel.Show(firstItem);
                else infoPanel.ShowEmpty();
            }

            if (!wasOpen) OnOpened?.Invoke();
        }

        private void Repopulate()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i].gameObject);
            spawned.Clear();

            if (boundUnit == null) return;

            foreach (var entry in Inventory.GetUsableFor(boundUnit))
            {
                var item = entry.instance != null ? entry.instance.Resolve() : null;
                if (item == null) continue;

                var go = Instantiate(cardPrefab, cardsContainer);
                var card = go.GetComponent<ItemCardUI>();
                if (card == null)
                {
                    Debug.LogWarning(
                        "[ItemSubmenuUI] Spawned card prefab is missing the " +
                        "ItemCardUI component on its root.", go);
                    continue;
                }

                var capturedSource = entry.source;
                var capturedIndex  = entry.index;
                var capturedItem   = item;
                card.Bind(
                    boundUnit, item, entry.instance,
                    entry.source, entry.index,
                    onClicked:   () => OnCardClicked(capturedSource, capturedIndex),
                    onHovered:   () => ShowInfo(capturedItem),
                    onUnhovered: () => {  });
                spawned.Add(card);
            }
        }

        public void Close()
        {
            bool wasOpen = panelRoot != null && panelRoot.activeSelf;
            BackButton.Instance?.Unbind(this);
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }
            if (panelRoot != null) panelRoot.SetActive(false);
            boundUnit = null;
            if (wasOpen) OnClosed?.Invoke();
        }

        public void ShowInfo(ItemData item)
        {
            if (infoPanel == null)
            {
                if (!_warnedNoInfoPanel)
                {
                    _warnedNoInfoPanel = true;
                    Debug.LogWarning(
                        "[ItemSubmenuUI] ShowInfo called but infoPanel is not " +
                        "wired in the inspector.", this);
                }
                return;
            }
            infoPanel.Show(item);
        }
        private bool _warnedNoInfoPanel;

        // ─── Internal ───────────────────────────────────────────────────────

        private void OnCardClicked(ItemSource source, int index)
        {
            var mgr = CombatManager.Instance;
            if (mgr == null || boundUnit == null) return;

            // Don't Close() yet — leave the submenu visible while the player
            // is selecting targets. Auto-closes on cancel / resolve like the
            // skill submenu.
            mgr.OnPlayerChooseItem(source, index, arrowOrigin);
        }

        private void HandleTurnEnd(Unit u) { if (u == boundUnit) Close(); }
        private void HandleTargetingCancelled(Unit u) => Close();

        private void HandleActionStateChanged(Unit u)
        {
            if (u == boundUnit && u.hasActedThisTurn) Close();
        }

        private void HandleActionResolved(CombatActionResult result)
        {
            if (result == null) return;
            // Close on item resolution. Skill resolution is unrelated; if the
            // submenu happens to be open during a skill resolve (shouldn't
            // happen given mutual exclusion), leave it alone.
            if (result.actionType != ActionType.Item) return;
            if (panelRoot != null && panelRoot.activeSelf) Close();
        }
    }
}
