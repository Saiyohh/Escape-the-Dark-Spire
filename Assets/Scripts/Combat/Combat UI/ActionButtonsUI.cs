using System;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ActionButtonsUI : MonoBehaviour
    {
        [SerializeField] private Button attackButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button itemsButton;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button withdrawButton;
        [SerializeField] private Button endTurnButton;

        [Header("Arrow Origin (optional)")]
        [Tooltip("Transform that the targeting arrow draws FROM when Attack/Skill is chosen. " +
                 "Usually the attack button's transform so the arrow visibly originates there.")]
        [SerializeField] private Transform arrowOrigin;

        [Header("Optional — routes Skill clicks to a submenu")]
        [SerializeField] private SkillSubmenuUI skillSubmenu;

        [Header("Optional — routes Items clicks to a submenu")]
        [SerializeField] private ItemSubmenuUI itemSubmenu;

        // Visibility state — independent signals; visible only when all are favorable.
        private bool playerTurnActive;
        private bool skillSubmenuOpen;
        private bool itemSubmenuOpen;
        private bool isTargeting;

        private Unit boundUnit;
        private CanvasGroup canvasGroup;

        // Stored handlers so we can unsubscribe cleanly even if external
        // singletons get rebuilt across domain reloads.
        private Action submenuOpenedHandler;
        private Action submenuClosedHandler;
        private Action itemSubmenuOpenedHandler;
        private Action itemSubmenuClosedHandler;
        private Action<bool> targetingStateHandler;
        private Action<int, int> rankChangedHandler;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (attackButton != null) attackButton.onClick.AddListener(OnAttackClicked);
            if (guardButton != null)  guardButton.onClick.AddListener(OnGuardClicked);
            if (skillButton != null)  skillButton.onClick.AddListener(OnSkillClicked);
            if (itemsButton != null)  itemsButton.onClick.AddListener(OnItemsClicked);
            if (advanceButton != null) advanceButton.onClick.AddListener(OnAdvanceClicked);
            if (withdrawButton != null) withdrawButton.onClick.AddListener(OnWithdrawClicked);
            if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);

            SetInteractable(false);
            ApplyVisibility();
        }

        private void OnEnable()
        {
            CombatEvents.OnUnitTurnStart      += HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd        += HandleUnitTurnEnd;
            CombatEvents.OnActionStateChanged += HandleActionStateChanged;
            CombatEvents.OnPlayerPhaseEnd     += HandlePhaseEnd;

            // Skill submenu open/close — wire if the submenu reference exists.
            if (skillSubmenu != null)
            {
                submenuOpenedHandler = HandleSubmenuOpened;
                submenuClosedHandler = HandleSubmenuClosed;
                skillSubmenu.OnOpened += submenuOpenedHandler;
                skillSubmenu.OnClosed += submenuClosedHandler;

                // Initial sync — the submenu may already be in some state.
                skillSubmenuOpen = skillSubmenu.IsOpen;
            }

            // Item submenu open/close — same wiring as skill submenu.
            if (itemSubmenu != null)
            {
                itemSubmenuOpenedHandler = HandleItemSubmenuOpened;
                itemSubmenuClosedHandler = HandleItemSubmenuClosed;
                itemSubmenu.OnOpened += itemSubmenuOpenedHandler;
                itemSubmenu.OnClosed += itemSubmenuClosedHandler;
                itemSubmenuOpen = itemSubmenu.IsOpen;
            }

            // Targeting state — TargetingSystem may not exist yet at scene
            // start; resubscribe on first frame in Update if so.
            TrySubscribeTargetingSystem();

            ApplyVisibility();
        }

        private void OnDisable()
        {
            CombatEvents.OnUnitTurnStart      -= HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd        -= HandleUnitTurnEnd;
            CombatEvents.OnActionStateChanged -= HandleActionStateChanged;
            CombatEvents.OnPlayerPhaseEnd     -= HandlePhaseEnd;

            if (skillSubmenu != null)
            {
                if (submenuOpenedHandler != null) skillSubmenu.OnOpened -= submenuOpenedHandler;
                if (submenuClosedHandler != null) skillSubmenu.OnClosed -= submenuClosedHandler;
            }

            if (itemSubmenu != null)
            {
                if (itemSubmenuOpenedHandler != null) itemSubmenu.OnOpened -= itemSubmenuOpenedHandler;
                if (itemSubmenuClosedHandler != null) itemSubmenu.OnClosed -= itemSubmenuClosedHandler;
            }

            if (TargetingSystem.Instance != null && targetingStateHandler != null)
                TargetingSystem.Instance.OnTargetingStateChanged -= targetingStateHandler;

            UnbindRankSubscription();
        }

        private void Update()
        {
            // TargetingSystem is created at scene start but not necessarily
            // before this component's OnEnable. Late-subscribe once available.
            if (targetingStateHandler == null)
                TrySubscribeTargetingSystem();
        }

        private void TrySubscribeTargetingSystem()
        {
            var ts = TargetingSystem.Instance;
            if (ts == null) return;
            if (targetingStateHandler != null) return;

            targetingStateHandler = HandleTargetingStateChanged;
            ts.OnTargetingStateChanged += targetingStateHandler;
            isTargeting = ts.IsTargeting;
            ApplyVisibility();
        }

        // ─── Event handlers ──────────────────────────────────────────────────

        private void HandleUnitTurnStart(Unit unit)
        {
            UnbindRankSubscription();

            boundUnit = unit;
            playerTurnActive = unit != null && unit.isPlayerControlled && unit.IsAlive;

            if (!playerTurnActive)
            {
                SetInteractable(false);
            }
            else
            {
                // Re-gate Move buttons whenever rank changes mid-turn (e.g. an
                // ally Pulls/Knockbacks the active unit before they act).
                rankChangedHandler = (oldR, newR) => RefreshInteractability();
                boundUnit.OnRankChanged += rankChangedHandler;
                RefreshInteractability();
            }

            ApplyVisibility();
        }

        private void HandleUnitTurnEnd(Unit unit)
        {
            if (boundUnit == unit)
            {
                UnbindRankSubscription();
                boundUnit = null;
            }
            playerTurnActive = false;
            SetInteractable(false);
            ApplyVisibility();
        }

        private void UnbindRankSubscription()
        {
            if (boundUnit != null && rankChangedHandler != null)
                boundUnit.OnRankChanged -= rankChangedHandler;
            rankChangedHandler = null;
        }

        private void HandleActionStateChanged(Unit unit)
        {
            if (unit == boundUnit) RefreshInteractability();
        }

        private void HandlePhaseEnd()
        {
            playerTurnActive = false;
            SetInteractable(false);
            ApplyVisibility();
        }

        private void HandleSubmenuOpened()
        {
            skillSubmenuOpen = true;
            ApplyVisibility();
        }

        private void HandleSubmenuClosed()
        {
            skillSubmenuOpen = false;
            ApplyVisibility();
        }

        private void HandleItemSubmenuOpened()
        {
            itemSubmenuOpen = true;
            ApplyVisibility();
        }

        private void HandleItemSubmenuClosed()
        {
            itemSubmenuOpen = false;
            ApplyVisibility();
        }

        private void HandleTargetingStateChanged(bool nowTargeting)
        {
            isTargeting = nowTargeting;
            ApplyVisibility();
        }

        // ─── Visibility ──────────────────────────────────────────────────────

        private void ApplyVisibility()
        {
            bool show = playerTurnActive && !skillSubmenuOpen && !itemSubmenuOpen && !isTargeting;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) return;
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }

        // ─── Button clicks ───────────────────────────────────────────────────

        private void OnAttackClicked()
        {
            if (TrySurfaceBasicRefusal()) return;

            // Channel-on-attack weapons (Defect's WPN_OrbBeam) auto-pick the
            // orb to channel inside the Attack resolver — no UI choice needed.
            var mgr = CombatManager.Instance;
            if (mgr != null) mgr.OnPlayerChooseAttack(arrowOrigin);
        }

        private void OnGuardClicked()
        {
            if (TrySurfaceBasicRefusal()) return;

            var mgr = CombatManager.Instance;
            if (mgr != null) mgr.OnPlayerChooseGuard();
        }

        private void OnSkillClicked()
        {
            if (skillSubmenu == null)
            {
                Debug.LogWarning(
                    "[ActionButtonsUI] Skill button clicked but Skill Submenu " +
                    "is not wired in the inspector. Drag the SkillSubmenu " +
                    "GameObject into ActionButtonsUI.skillSubmenu.", this);
                return;
            }
            if (boundUnit == null)
            {
                Debug.LogWarning(
                    "[ActionButtonsUI] Skill button clicked but no active unit " +
                    "is bound — should not be possible since the button should " +
                    "be disabled outside of a player turn.", this);
                return;
            }
            if (TrySurfaceBasicRefusal()) return;

            skillSubmenu.OpenFor(boundUnit, arrowOrigin);
        }

        private void OnItemsClicked()
        {
            if (itemSubmenu == null)
            {
                Debug.LogWarning(
                    "[ActionButtonsUI] Items button clicked but Item Submenu " +
                    "is not wired in the inspector. Drag the ItemSubmenu " +
                    "GameObject into ActionButtonsUI.itemSubmenu.", this);
                return;
            }
            if (boundUnit == null) return;
            // Items refusal isn't a single basic-action refusal — different
            // items have different action costs (Action / FreeAction /
            // ZeroCost). The submenu still opens; per-item refusal is shown
            // on the cards via Unit.GetItemRefusal.
            // Immobilized still blocks opening though — same as skill submenu.
            if (boundUnit.IsStunned)
            {
                var disp = UnitDisplay.GetDisplay(boundUnit);
                if (disp != null)
                    disp.ShowSpeechBubble(ActionRefusalMessages.For(ActionRefusalReason.Immobilized, boundUnit));
                return;
            }
            itemSubmenu.OpenFor(boundUnit, arrowOrigin);
        }

        private void OnAdvanceClicked()
        {
            if (TrySurfaceBasicRefusal()) return;

            // Advance: move toward the front (lower rank, toward the enemy).
            // OnPlayerChooseMove(direction): negative = advance, positive = withdraw.
            var mgr = CombatManager.Instance;
            if (mgr != null) mgr.OnPlayerChooseMove(-1);
        }

        private void OnWithdrawClicked()
        {
            if (TrySurfaceBasicRefusal()) return;

            // Withdraw: move toward the back (higher rank, away from the enemy).
            var mgr = CombatManager.Instance;
            if (mgr != null) mgr.OnPlayerChooseMove(+1);
        }

        // Returns true if a refusal bubble was shown (caller should bail).
        // Used by Attack / Guard / Skill / Advance / Withdraw — they all
        // share the same basic-action gate (Immobilized, NoAction).
        // Rank-edge unavailability (already at MinRank/MaxRank for Move) is
        // intentionally NOT surfaced as a bubble — it's a positional limit,
        // not an action cost the player needs explained.
        private bool TrySurfaceBasicRefusal()
        {
            if (boundUnit == null) return false;
            var refusal = boundUnit.GetBasicActionRefusal();
            if (refusal == ActionRefusalReason.None) return false;

            var disp = UnitDisplay.GetDisplay(boundUnit);
            if (disp != null)
                disp.ShowSpeechBubble(ActionRefusalMessages.For(refusal, boundUnit));
            return true;
        }

        private void OnEndTurnClicked()
        {
            // OnPlayerEndTurn advances the turn unconditionally.
            // (OnPlayerChoosePass would only resolve a "Pass" action and bail
            // if the player already acted — wrong target for the End Turn button.)
            var mgr = CombatManager.Instance;
            if (mgr != null) mgr.OnPlayerEndTurn();
        }

        // ─── Interactability ─────────────────────────────────────────────────

        // Visual-only dim for buttons that can't currently be used. The buttons
        // stay clickable so we can intercept the click and show a refusal
        // bubble instead of silently swallowing the input. The whole-bar
        // hide path (not-your-turn / submenu open / targeting) is handled
        // separately by ApplyVisibility() via the parent CanvasGroup, which
        // sets interactable=false at the bar level.
        [Tooltip("Alpha applied to a button's per-button CanvasGroup when its " +
                 "action gate (out of action / out of rank / immobilized) is " +
                 "currently failing. Buttons stay clickable in that state — " +
                 "click routes to a refusal speech bubble.")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledAlpha = 0.45f;

        // Per-button CanvasGroups, lazily created in EnsureCanvasGroup so
        // designers don't have to add them manually to existing prefabs.
        private CanvasGroup EnsureCanvasGroup(Component target)
        {
            if (target == null) return null;
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private void SetButtonAvailable(Button btn, bool available)
        {
            if (btn == null) return;
            // Always interactable so the click reaches our handler — the
            // handler decides whether to fire the action or show a refusal
            // bubble. The parent CanvasGroup (set in ApplyVisibility) blocks
            // clicks when the bar should be fully hidden.
            btn.interactable = true;
            var cg = EnsureCanvasGroup(btn);
            if (cg != null) cg.alpha = available ? 1f : disabledAlpha;
        }

        private void RefreshInteractability()
        {
            if (boundUnit == null || !boundUnit.IsAlive)
            {
                SetInteractable(false);
                return;
            }

            bool hasAction = !boundUnit.hasActedThisTurn;
            SetButtonAvailable(attackButton,   hasAction);
            SetButtonAvailable(guardButton,    hasAction);
            SetButtonAvailable(skillButton,    hasAction);
            // Items button has its own gate — usable as long as Immobilized
            // isn't blocking everything. Per-item action-cost gating happens
            // inside the submenu (some items are FreeAction or ZeroCost so
            // the user may still have legal items even after acting).
            SetButtonAvailable(itemsButton,    !boundUnit.IsStunned && HasAnyUsableItem());
            SetButtonAvailable(advanceButton,  hasAction && boundUnit.currentRank > RankHelper.MinRank);
            SetButtonAvailable(withdrawButton, hasAction && boundUnit.currentRank < RankHelper.MaxRank);
            if (endTurnButton != null) endTurnButton.interactable = true;
        }

        // Cheap check used by RefreshInteractability: does the active unit
        // have ANY item available right now (pouch or party)? Avoids a full
        // submenu open when there's nothing to show.
        private bool HasAnyUsableItem()
        {
            if (boundUnit == null) return false;
            foreach (var _ in Inventory.GetUsableFor(boundUnit)) return true;
            return false;
        }

        // Whole-bar enable/disable, used at non-player turns. Sets every
        // button's interactable to the same value — paired with the parent
        // CanvasGroup hide. Per-button visual dim is reset to full alpha so
        // the bar paints clean when it next becomes visible on a player turn.
        private void SetInteractable(bool on)
        {
            if (attackButton != null)     attackButton.interactable     = on;
            if (guardButton != null)      guardButton.interactable      = on;
            if (skillButton != null)      skillButton.interactable      = on;
            if (itemsButton != null)      itemsButton.interactable      = on;
            if (advanceButton != null)    advanceButton.interactable    = on;
            if (withdrawButton != null)   withdrawButton.interactable   = on;
            if (endTurnButton != null)    endTurnButton.interactable    = on;

            if (on)
            {
                // Reset per-button alpha so a stale dim from a prior turn
                // doesn't carry over. RefreshInteractability re-dims them
                // immediately afterward as needed.
                var ag = EnsureCanvasGroup(attackButton);   if (ag != null) ag.alpha = 1f;
                var gg = EnsureCanvasGroup(guardButton);    if (gg != null) gg.alpha = 1f;
                var sg = EnsureCanvasGroup(skillButton);    if (sg != null) sg.alpha = 1f;
                var ig = EnsureCanvasGroup(itemsButton);    if (ig != null) ig.alpha = 1f;
                var av = EnsureCanvasGroup(advanceButton);  if (av != null) av.alpha = 1f;
                var wd = EnsureCanvasGroup(withdrawButton); if (wd != null) wd.alpha = 1f;
            }
        }
    }
}
