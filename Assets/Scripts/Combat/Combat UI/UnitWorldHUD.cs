using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class UnitWorldHUD : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Slider hpBar;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("Seconds the HP fill takes to lerp to the new value. Set to 0 to snap.")]
        [SerializeField] private float hpTweenDuration = 0.3f;
        private Coroutine hpTweenCo;

        [Header("SP")]
        [SerializeField] private Slider spBar;
        [SerializeField] private TMP_Text spText;

        [Header("Conditions")]
        [Tooltip("RectTransform with a HorizontalLayoutGroup — condition icons are spawned as children.")]
        [SerializeField] private RectTransform conditionsContainer;
        [Tooltip("Prefab for each condition (needs a ConditionIconUI component).")]
        [SerializeField] private GameObject conditionIconPrefab;

        [Header("Death Fade")]
        [SerializeField] private float deathFadeOutDuration = 0.25f;

        [Header("Hover Name Overlay")]
        [Tooltip("Wrap HP / SP / text under one CanvasGroup so they fade together " +
                 "when the player hovers this unit. Leave null to disable the fade.")]
        [SerializeField] private CanvasGroup statsGroup;
        [Tooltip("CanvasGroup containing the name label + drop-shadow image. Fades " +
                 "in while hovered, out otherwise. Leave null to disable.")]
        [SerializeField] private CanvasGroup nameGroup;
        [Tooltip("Optional text label inside nameGroup. Auto-populated from " +
                 "linkedUnit.unitName at Initialize.")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Seconds for the hover fade between bars and name overlay.")]
        [SerializeField] private float hoverFadeDuration = 0.18f;
        private Coroutine hoverFadeCo;
        private bool isHovered;

        private Unit linkedUnit;
        private readonly Dictionary<ConditionID, ConditionIconUI> conditionIcons = new();

        private Action<int> onDamageTakenHandler;
        private Action<int> onHealReceivedHandler;
        private Action onStatsChangedHandler;
        private Action onDeathHandler;
        private Action<ConditionID, int> onConditionAppliedHandler;
        private Action<ConditionID> onConditionRemovedHandler;
        private Action<ConditionID, int> onConditionChangedHandler;

        public void Initialize(Unit unit)
        {
            linkedUnit = unit;
            if (unit == null) return;

            RefreshHP();
            RefreshSP();
            RebuildConditions();
            InitializeHoverOverlay(unit);

            onDamageTakenHandler       = _ => { if (!IsSuppressed) RefreshHP(); };
            onHealReceivedHandler      = _ => { if (!IsSuppressed) RefreshHP(); };
            onStatsChangedHandler      = () => { if (!IsSuppressed) RefreshAll(); };
            onDeathHandler             = FadeOutAndHide;
            onConditionAppliedHandler  = HandleConditionApplied;
            onConditionRemovedHandler  = HandleConditionRemoved;
            onConditionChangedHandler  = HandleConditionChanged;

            unit.OnDamageTaken  += onDamageTakenHandler;
            unit.OnHealReceived += onHealReceivedHandler;
            unit.OnStatsChanged += onStatsChangedHandler;
            unit.OnDeath        += onDeathHandler;

            unit.conditions.OnConditionApplied += onConditionAppliedHandler;
            unit.conditions.OnConditionRemoved += onConditionRemovedHandler;
            unit.conditions.OnConditionChanged += onConditionChangedHandler;

            var display = UnitDisplay.GetDisplay(unit);
            if (display != null)
            {
                display.OnUISuppressionLifted          += HandleSuppressionLifted;
                display.OnConditionUISuppressionLifted += HandleConditionUISuppressionLifted;
            }
        }

        private bool IsSuppressed
        {
            get
            {
                if (linkedUnit == null) return false;
                var d = UnitDisplay.GetDisplay(linkedUnit);
                return d != null && d.SuppressUIUpdates;
            }
        }

        private bool IsConditionUISuppressed
        {
            get
            {
                if (linkedUnit == null) return false;
                var d = UnitDisplay.GetDisplay(linkedUnit);
                return d != null && d.SuppressConditionUI;
            }
        }

        private void HandleSuppressionLifted()
        {
            if (linkedUnit == null) return;
            RefreshAll();
        }

        private void HandleConditionUISuppressionLifted()
        {
            if (linkedUnit == null) return;
            RebuildConditions();
        }

        public void Hide() => gameObject.SetActive(false);
        public void Show() => gameObject.SetActive(true);

        private void FadeOutAndHide()
        {
            if (!gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
            StartCoroutine(FadeOutAndHideCo());
        }

        private System.Collections.IEnumerator FadeOutAndHideCo()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            float t = 0f;
            float dur = Mathf.Max(0.0001f, deathFadeOutDuration);
            float startAlpha = cg.alpha;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void RefreshAll()
        {
            RefreshHP();
            RefreshSP();
        }

        private void RefreshHP()
        {
            if (linkedUnit == null) return;
            if (hpBar != null)
            {
                hpBar.maxValue = Mathf.Max(1, linkedUnit.maxHP);
                float target = linkedUnit.currentHP;
                if (hpTweenDuration <= 0f || !gameObject.activeInHierarchy)
                {
                    if (hpTweenCo != null) { StopCoroutine(hpTweenCo); hpTweenCo = null; }
                    hpBar.value = target;
                }
                else
                {
                    if (hpTweenCo != null) StopCoroutine(hpTweenCo);
                    hpTweenCo = StartCoroutine(TweenHpBar(hpBar.value, target, hpTweenDuration));
                }
            }
            if (hpText != null)
                hpText.text = $"{linkedUnit.currentHP}";
        }

        private System.Collections.IEnumerator TweenHpBar(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (hpBar != null) hpBar.value = Mathf.Lerp(from, to, k);
                yield return null;
            }
            if (hpBar != null) hpBar.value = to;
            hpTweenCo = null;
        }

        private void RefreshSP()
        {
            if (linkedUnit == null) return;
            if (spBar != null)
            {
                spBar.maxValue = Mathf.Max(1, linkedUnit.maxSP);
                spBar.value = linkedUnit.currentSP;
            }
            if (spText != null)
                spText.text = $"{linkedUnit.currentSP}";
        }

        private void HandleConditionApplied(ConditionID id, int stacks)
        {
            if (IsConditionUISuppressed) return; // catch-up via HandleConditionUISuppressionLifted
            if (!conditionIcons.TryGetValue(id, out var icon))
            {
                icon = SpawnIcon(id);
                if (icon != null) conditionIcons[id] = icon;
            }
            if (icon != null) icon.SetStacks(stacks);
        }

        private void HandleConditionChanged(ConditionID id, int stacks)
        {
            if (IsConditionUISuppressed) return;
            if (conditionIcons.TryGetValue(id, out var icon))
                icon.SetStacks(stacks);
        }

        private void HandleConditionRemoved(ConditionID id)
        {
            if (IsConditionUISuppressed) return;
            if (conditionIcons.TryGetValue(id, out var icon) && icon != null)
            {
                Destroy(icon.gameObject);
                conditionIcons.Remove(id);
            }
        }

        private void RebuildConditions()
        {
            if (conditionsContainer == null) return;

            for (int i = conditionsContainer.childCount - 1; i >= 0; i--)
                Destroy(conditionsContainer.GetChild(i).gameObject);
            conditionIcons.Clear();

            if (linkedUnit == null) return;
            var all = linkedUnit.conditions.GetAllConditions();
            for (int i = 0; i < all.Count; i++)
            {
                var inst = all[i];
                var icon = SpawnIcon(inst.data.conditionID);
                if (icon == null) continue;
                icon.SetStacks(inst.stacks);
                conditionIcons[inst.data.conditionID] = icon;
            }
        }

        private ConditionIconUI SpawnIcon(ConditionID id)
        {
            if (conditionIconPrefab == null || conditionsContainer == null) return null;

            var data = ConditionLibrary.Instance != null
                ? ConditionLibrary.Instance.Get(id)
                : null;
            if (data == null) return null;

            var go = Instantiate(conditionIconPrefab, conditionsContainer);
            var ui = go.GetComponent<ConditionIconUI>();
            if (ui != null) ui.Bind(data);
            return ui;
        }

        private void OnDestroy()
        {
            if (linkedUnit == null) return;
            if (onDamageTakenHandler != null)  linkedUnit.OnDamageTaken  -= onDamageTakenHandler;
            if (onHealReceivedHandler != null) linkedUnit.OnHealReceived -= onHealReceivedHandler;
            if (onStatsChangedHandler != null) linkedUnit.OnStatsChanged -= onStatsChangedHandler;
            if (onDeathHandler != null)        linkedUnit.OnDeath        -= onDeathHandler;
            if (linkedUnit.conditions != null)
            {
                if (onConditionAppliedHandler != null) linkedUnit.conditions.OnConditionApplied -= onConditionAppliedHandler;
                if (onConditionRemovedHandler != null) linkedUnit.conditions.OnConditionRemoved -= onConditionRemovedHandler;
                if (onConditionChangedHandler != null) linkedUnit.conditions.OnConditionChanged -= onConditionChangedHandler;
            }
        }

        private CanvasGroup rootCanvasGroup;
        private Coroutine introFadeCo;

        private CanvasGroup GetOrAddRootCanvasGroup()
        {
            if (rootCanvasGroup != null) return rootCanvasGroup;
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            return rootCanvasGroup;
        }

        public void PrepareForIntro()
        {
            var cg = GetOrAddRootCanvasGroup();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        public Coroutine FadeIn(float duration, float startDelay = 0f)
        {
            if (introFadeCo != null) StopCoroutine(introFadeCo);
            introFadeCo = StartCoroutine(IntroFadeInCo(duration, startDelay));
            return introFadeCo;
        }

        private System.Collections.IEnumerator IntroFadeInCo(float duration, float startDelay)
        {
            var cg = GetOrAddRootCanvasGroup();
            if (startDelay > 0f)
            {
                float d = 0f;
                while (d < startDelay) { d += Time.deltaTime; yield return null; }
            }
            float dur = Mathf.Max(0.0001f, duration);
            float from = cg.alpha;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
            introFadeCo = null;
        }

        private void InitializeHoverOverlay(Unit unit)
        {
            if (nameLabel != null && unit != null)
                nameLabel.text = unit.unitName;
            if (statsGroup != null) statsGroup.alpha = 1f;
            if (nameGroup  != null) nameGroup.alpha  = 0f;
            isHovered = false;
        }

        public void OnUnitHoverEnter()
        {
            if (isHovered) return;
            isHovered = true;
            StartHoverFade(targetBars: 0f, targetName: 1f);
        }

        public void OnUnitHoverExit()
        {
            if (!isHovered) return;
            isHovered = false;
            StartHoverFade(targetBars: 1f, targetName: 0f);
        }

        private void StartHoverFade(float targetBars, float targetName)
        {
            if (statsGroup == null && nameGroup == null) return;
            if (hoverFadeCo != null) StopCoroutine(hoverFadeCo);
            if (!gameObject.activeInHierarchy)
            {
                if (statsGroup != null) statsGroup.alpha = targetBars;
                if (nameGroup  != null) nameGroup.alpha  = targetName;
                return;
            }
            hoverFadeCo = StartCoroutine(HoverFadeCo(targetBars, targetName));
        }

        private System.Collections.IEnumerator HoverFadeCo(float targetBars, float targetName)
        {
            float fromBars = statsGroup != null ? statsGroup.alpha : 0f;
            float fromName = nameGroup  != null ? nameGroup.alpha  : 0f;
            float dur = Mathf.Max(0.0001f, hoverFadeDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (statsGroup != null) statsGroup.alpha = Mathf.Lerp(fromBars, targetBars, k);
                if (nameGroup  != null) nameGroup.alpha  = Mathf.Lerp(fromName, targetName, k);
                yield return null;
            }
            if (statsGroup != null) statsGroup.alpha = targetBars;
            if (nameGroup  != null) nameGroup.alpha  = targetName;
            hoverFadeCo = null;
        }
    }
}
