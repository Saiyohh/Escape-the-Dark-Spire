// SkillSubmenuUI.cs
// -----------------------------------------------------------------------------
// Popup that lists the active player's equipped skills as compact buttons
// (SkillCardUI, one per skill, stacked vertically) alongside a SkillInfoPanelUI
// that shows the banner + name + range + description for whichever skill is
// currently hovered. Clicking a button picks that skill and routes it into
// CombatManager.OnPlayerChooseSkill, which kicks off targeting.
//
// Hover behavior:
//   • First skill is shown by default when the submenu opens.
//   • Hovering any card updates the info panel immediately.
//   • Moving the pointer off a card does NOT revert to empty — the info panel
//     stays on the last-hovered skill so the player can read it + click.
//
// Opens via ActionButtonsUI when the Skill button is clicked; closes after
// selection, on right-click (cancel), or when the player's turn ends.
//
// Prefab setup:
//   SkillSubmenu (Canvas child)
//     Panel (Image background)
//       HorizontalLayoutGroup or two-column grid
//       ├─ CardsContainer (VerticalLayoutGroup)
//       │    └─ (SkillCardUI children spawn at runtime)
//       └─ InfoPanel (SkillInfoPanelUI)
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class SkillSubmenuUI : MonoBehaviour
    {
        /// <summary>Fired after the panel opens (already populated with cards).</summary>
        public event Action OnOpened;

        /// <summary>Fired after the panel closes (selection picked, turn ended, cancelled).</summary>
        public event Action OnClosed;

        /// <summary>True while the panel root is active and showing skill cards.</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        [SerializeField] private RectTransform cardsContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private SkillInfoPanelUI infoPanel;

        [Tooltip("The panel root — toggled active on Open / Close. Defaults to this gameObject if null.")]
        [SerializeField] private GameObject panelRoot;

        // Back button: a single shared BackButton lives on the Combat canvas
        // (singleton). OpenFor claims it via Bind(this, Close); Close releases
        // it via Unbind(this). No serialized reference needed.

        private readonly List<SkillCardUI> spawned = new();
        private Unit boundUnit;
        private Transform arrowOrigin;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            // Info panel mirrors the submenu's open state — hidden any time
            // the submenu isn't visible.
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }

            // Auto-hide the panel ONLY when panelRoot is a child GameObject.
            // When panelRoot == gameObject (the script's own GO), calling
            // SetActive(false) here creates a race: if the GameObject was
            // authored inactive in the scene (or deactivated by another
            // component's Awake), Awake hasn't run yet. The first OpenFor
            // triggers SetActive(true) → Awake fires → Close → SetActive(false),
            // overriding OpenFor's intent and the panel never appears.
            // Initial closed state for self-rooted panels is the designer's
            // responsibility (author the GameObject inactive) or the
            // CombatUIBootstrap's (Start-time ResetAll, runs after all Awakes).
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

        /// <summary>Open the submenu populated with the unit's equipped skills.</summary>
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

            // Clear old cards
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Destroy(spawned[i].gameObject);
            spawned.Clear();

            // Spawn one button per equipped skill
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
                    onUnhovered: () => { /* keep last-hovered showing */ });
                spawned.Add(card);

                if (firstSkill == null) firstSkill = skill;
            }

            bool wasOpen = panelRoot.activeSelf;
            panelRoot.SetActive(true);

            // Claim the shared back button. Bind activates it and routes its
            // click to our Close method. Released in Close() via Unbind.
            BackButton.Instance?.Bind(this, Close);

            // Info panel becomes visible alongside the submenu and loads the
            // first skill so it isn't empty.
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
            // Hide the info panel in lockstep — it should never appear while
            // the submenu is closed.
            if (infoPanel != null)
            {
                infoPanel.ShowEmpty();
                infoPanel.gameObject.SetActive(false);
            }
            if (panelRoot != null) panelRoot.SetActive(false);
            boundUnit = null;
            if (wasOpen) OnClosed?.Invoke();
        }

        /// <summary>Called by SkillCardUI on hover. Also usable externally (e.g. keyboard nav).</summary>
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

        // ─── Internal ───────────────────────────────────────────────────────

        private void OnCardClicked(int skillIndex)
        {
            var mgr = CombatManager.Instance;
            if (mgr == null || boundUnit == null) return;

            // Don't Close() yet — leave the submenu visible while the player
            // is selecting targets. The submenu auto-closes when targeting
            // cancels (HandleTargetingCancelled) or the skill resolves
            // (HandleActionResolved). That keeps the info panel and card
            // list on screen during the targeting phase as part of the cast.
            mgr.OnPlayerChooseSkill(skillIndex, arrowOrigin);
        }

        private void HandleTurnEnd(Unit u) { if (u == boundUnit) Close(); }
        private void HandleTargetingCancelled(Unit u) => Close();
        private void HandleActionStateChanged(Unit u)
        {
            // If this unit already used its action, the submenu is stale — close.
            if (u == boundUnit && u.hasActedThisTurn) Close();
        }

        /// <summary>
        /// Close the submenu once a skill actually resolves. Close() hides
        /// the info panel in lockstep, so the player sees the cards + info
        /// during the entire targeting phase, then both vanish together when
        /// the skill commits. Cancelled targeting goes through HandleTargetingCancelled.
        /// </summary>
        private void HandleActionResolved(CombatActionResult result)
        {
            if (result == null) return;
            if (result.actionType != ActionType.Skill) return;
            if (panelRoot != null && panelRoot.activeSelf) Close();
        }
    }
}
