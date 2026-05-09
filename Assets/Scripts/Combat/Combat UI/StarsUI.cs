// StarsUI.cs
// -----------------------------------------------------------------------------
// HUD widget that displays the current Stars count for the Regent (or any
// future character with a Star pool). Subscribes to CombatEvents.OnCombatStart
// to locate the unit flagged hasStarSystem-equivalent (currently identified by
// CharacterData.startingStars > 0, since there's no dedicated boolean flag —
// non-Star characters keep startingStars = 0).
//
// Layout: a single label (TMP_Text or UnityEngine.UI.Text) plus an optional
// icon. Updates live via Unit.OnStarsChanged.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DarkSpire
{
    public class StarsUI : MonoBehaviour
    {
        [Header("Display targets — one of these will be used")]
        [Tooltip("Preferred label. Receives the current Stars count as text.")]
        [SerializeField] private TMP_Text countLabelTMP;
        [Tooltip("Fallback label if no TMP_Text is wired.")]
        [SerializeField] private Text countLabelLegacy;

        [Header("Optional icon")]
        [SerializeField] private Image starIcon;

        [Header("Visibility")]
        [Tooltip("If true, the entire widget hides when no Star-bearer is in " +
                 "the party (or the bearer is dead). Recommended on so the " +
                 "Regent's HUD doesn't show on non-Regent runs.")]
        [SerializeField] private bool hideWhenNoBearer = true;

        private Unit boundUnit;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            CombatEvents.OnCombatStart += HandleCombatStart;
            CombatEvents.OnCombatEnd   += HandleCombatEnd;
            HandleCombatStart();
        }

        private void OnDisable()
        {
            CombatEvents.OnCombatStart -= HandleCombatStart;
            CombatEvents.OnCombatEnd   -= HandleCombatEnd;
            UnbindUnit();
        }

        private void HandleCombatStart()
        {
            UnbindUnit();
            BindUnit(FindStarBearer());
            Refresh();
        }

        private void HandleCombatEnd(bool _)
        {
            UnbindUnit();
            Refresh();
        }

        /// <summary>
        /// Locate a player unit whose CharacterData defines a Star pool.
        /// Identified by startingStars > 0 (no dedicated hasStarSystem flag —
        /// the field doubles as gate + value).
        /// </summary>
        private static Unit FindStarBearer()
        {
            var mgr = CombatManager.Instance;
            if (mgr == null) return null;
            foreach (var u in mgr.PlayerUnits)
            {
                if (u != null && u.characterData != null
                    && u.characterData.startingStars > 0)
                    return u;
            }
            return null;
        }

        private void BindUnit(Unit u)
        {
            boundUnit = u;
            if (boundUnit != null)
            {
                boundUnit.OnStarsChanged += HandleStarsChanged;
                boundUnit.OnDeath        += HandleDeath;
            }
        }

        private void UnbindUnit()
        {
            if (boundUnit != null)
            {
                boundUnit.OnStarsChanged -= HandleStarsChanged;
                boundUnit.OnDeath        -= HandleDeath;
            }
            boundUnit = null;
        }

        private void HandleStarsChanged(Unit _) => Refresh();
        private void HandleDeath() => Refresh();

        private void Refresh()
        {
            bool hasBearer = boundUnit != null && boundUnit.IsAlive;

            if (canvasGroup != null && hideWhenNoBearer)
            {
                canvasGroup.alpha = hasBearer ? 1f : 0f;
                canvasGroup.interactable = hasBearer;
                canvasGroup.blocksRaycasts = hasBearer;
            }

            int stars = hasBearer ? boundUnit.currentStars : 0;
            string text = stars.ToString();

            if (countLabelTMP != null)        countLabelTMP.text = text;
            else if (countLabelLegacy != null) countLabelLegacy.text = text;
        }
    }
}
