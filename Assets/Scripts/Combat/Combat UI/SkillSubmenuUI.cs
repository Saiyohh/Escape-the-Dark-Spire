using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class SkillSubmenuUI : MonoBehaviour
    {
        public event Action OnOpened;

        public event Action OnClosed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        [SerializeField] private RectTransform cardsContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private SkillInfoPanelUI infoPanel;

        [Tooltip("The panel root — toggled active on Open / Close. Defaults to this gameObject if null.")]
        [SerializeField] private GameObject panelRoot;

        private readonly List<SkillCardUI> spawned = new();
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
            if (panelRoot == null) panelRoot = gameObject; // lazy init if Awake never ran

            if (unit == null)
            {
                Debug.LogWarning("[SkillSubmenuUI] OpenFor called with a null unit.", this);
                return;
            }
            if (cardsContainer == null)
            {
                Debug.LogWarning(
                    "[SkillSubmenuUI] cardsContainer is not wired — assign the " +
                    "VerticalLayoutGroup that holds skill cards.", this);
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning(
                    "[SkillSubmenuUI] cardPrefab is not wired — assign the " +
                    "SkillCard prefab.", this);
                return;
            }

            boundUnit = unit;
            this.arrowOrigin = arrowOrigin;

            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i].gameObject);
            spawned.Clear();

            SkillData firstSkill = null;
            for (int i = 0; i < unit.equippedSkills.Count; i++)
            {
                var skill = unit.equippedSkills[i];
                if (skill == null) continue;

                var go = Instantiate(cardPrefab, cardsContainer);
                var card = go.GetComponent<SkillCardUI>();
                if (card == null)
                {
                    Debug.LogWarning(
                        "[SkillSubmenuUI] Spawned card prefab is missing the " +
                        "SkillCardUI component on its root. Hover updates won't " +
                        "fire and the click handler won't bind. Add SkillCardUI " +
                        "to the SkillCard prefab's root GameObject.", go);
                    continue;
                }

                int capturedIndex = i;
                var capturedSkill = skill;
                card.Bind(
                    unit, skill,
                    onClicked:   () => OnCardClicked(capturedIndex),
                    onHovered:   () => ShowInfo(capturedSkill),
                    onUnhovered: () => {  });
                spawned.Add(card);

                if (firstSkill == null) firstSkill = skill;
            }

            bool wasOpen = panelRoot.activeSelf;
            panelRoot.SetActive(true);

            BackButton.Instance?.Bind(this, Close);

            if (infoPanel != null)
            {
                infoPanel.gameObject.SetActive(true);
                if (firstSkill != null) infoPanel.Show(firstSkill, boundUnit);
                else infoPanel.ShowEmpty();
            }

            if (!wasOpen) OnOpened?.Invoke();
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

        public void ShowInfo(SkillData skill)
        {
            if (infoPanel == null)
            {
                if (!_warnedNoInfoPanel)
                {
                    _warnedNoInfoPanel = true;
                    Debug.LogWarning(
                        "[SkillSubmenuUI] ShowInfo called but infoPanel is not " +
                        "wired in the inspector. Drag the persistent SkillInfoPanel " +
                        "GameObject into SkillSubmenuUI.infoPanel.", this);
                }
                return;
            }
            infoPanel.Show(skill, boundUnit);
        }
        private bool _warnedNoInfoPanel;

        private void OnCardClicked(int skillIndex)
        {
            var mgr = CombatManager.Instance;
            if (mgr == null || boundUnit == null) return;

            mgr.OnPlayerChooseSkill(skillIndex, arrowOrigin);
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
            if (result.actionType != ActionType.Skill) return;
            if (panelRoot != null && panelRoot.activeSelf) Close();
        }
    }
}
