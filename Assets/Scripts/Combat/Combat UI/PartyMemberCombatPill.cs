using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class PartyMemberCombatPill : MonoBehaviour
    {
        [Header("Portrait")]
        [Tooltip("The Mask child whose sizeDelta.y tweens between collapsed and expanded heights.")]
        [SerializeField] private RectTransform mask;
        [Tooltip("The Portrait Image child of the Mask. Sprite, scale, and offset are bound from CharacterData.")]
        [SerializeField] private Image portrait;

        [Header("Bars")]
        [SerializeField] private Slider hpBar;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Slider spBar;
        [SerializeField] private TMP_Text spLabel;
        [SerializeField] private Slider expBar;
        [SerializeField] private TMP_Text expLabel;

        [Header("Mask Animation")]
        [SerializeField] private float maskCollapsedHeight = 80f;
        [SerializeField] private float maskExpandedHeight  = 130f;
        [SerializeField] private float maskTweenDuration   = 0.18f;

        [Header("HP Tween")]
        [Tooltip("Duration of the HP-bar value lerp on damage/heal. Mirrors UnitWorldHUD.")]
        [SerializeField] private float hpTweenDuration = 0.3f;

        [Header("EXP Stub")]
        [SerializeField] private int expCurrentStub = 0;
        [SerializeField] private int expMaxStub     = 100;

        public Unit BoundUnit { get; private set; }
        public bool IsExpanded { get; private set; }

        private CharacterData boundData;
        private Coroutine hpTweenCo;
        private Coroutine maskTweenCo;

        private System.Action<int> onDamageHandler;
        private System.Action<int> onHealHandler;
        private System.Action      onStatsHandler;

        public void Bind(Unit unit, CharacterData data)
        {
            if (BoundUnit != null) Unbind();

            BoundUnit  = unit;
            boundData  = data;

            ApplyPortrait();
            RefreshHP(animate: false);
            RefreshSP();
            RefreshEXP();

            if (unit != null)
            {
                onDamageHandler = _  => RefreshHP(animate: true);
                onHealHandler   = _  => RefreshHP(animate: true);
                onStatsHandler  = () => { RefreshHP(animate: true); RefreshSP(); };
                unit.OnDamageTaken  += onDamageHandler;
                unit.OnHealReceived += onHealHandler;
                unit.OnStatsChanged += onStatsHandler;
            }

            SetExpanded(false, animate: false);
        }

        public void Unbind()
        {
            if (BoundUnit != null)
            {
                if (onDamageHandler != null) BoundUnit.OnDamageTaken  -= onDamageHandler;
                if (onHealHandler   != null) BoundUnit.OnHealReceived -= onHealHandler;
                if (onStatsHandler  != null) BoundUnit.OnStatsChanged -= onStatsHandler;
            }
            onDamageHandler = null;
            onHealHandler   = null;
            onStatsHandler  = null;

            if (hpTweenCo  != null) { StopCoroutine(hpTweenCo);  hpTweenCo  = null; }
            if (maskTweenCo != null) { StopCoroutine(maskTweenCo); maskTweenCo = null; }

            BoundUnit = null;
            boundData = null;
        }

        public void SetExpanded(bool expanded, bool animate = true)
        {
            IsExpanded = expanded;
            if (mask == null) return;

            float target = expanded ? maskExpandedHeight : maskCollapsedHeight;

            if (!animate || maskTweenDuration <= 0f || !gameObject.activeInHierarchy)
            {
                if (maskTweenCo != null) { StopCoroutine(maskTweenCo); maskTweenCo = null; }
                var size = mask.sizeDelta;
                size.y = target;
                mask.sizeDelta = size;
                return;
            }

            if (maskTweenCo != null) StopCoroutine(maskTweenCo);
            maskTweenCo = StartCoroutine(TweenMaskHeight(mask.sizeDelta.y, target, maskTweenDuration));
        }

        private void OnDestroy() => Unbind();

        private void ApplyPortrait()
        {
            if (portrait == null || boundData == null) return;

            portrait.sprite = boundData.combatSprite;
            portrait.enabled = boundData.combatSprite != null;

            var rt = portrait.rectTransform;
            float s = boundData.partyTrayPortraitScale;
            if (s <= 0f) s = 1f;
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = boundData.partyTrayPortraitOffset;
        }

        private void RefreshHP(bool animate)
        {
            if (BoundUnit == null) return;

            if (hpBar != null)
            {
                hpBar.maxValue = Mathf.Max(1, BoundUnit.maxHP);
                float target = BoundUnit.currentHP;

                if (!animate || hpTweenDuration <= 0f || !gameObject.activeInHierarchy)
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

            if (hpLabel != null)
                hpLabel.text = $"{BoundUnit.currentHP}/{BoundUnit.maxHP}";
        }

        private void RefreshSP()
        {
            if (BoundUnit == null) return;

            if (spBar != null)
            {
                spBar.maxValue = Mathf.Max(1, BoundUnit.maxSP);
                spBar.value    = BoundUnit.currentSP;
            }
            if (spLabel != null)
                spLabel.text = $"{BoundUnit.currentSP}/{BoundUnit.maxSP}";
        }

        private void RefreshEXP()
        {
            int max = Mathf.Max(1, expMaxStub);
            int cur = Mathf.Clamp(expCurrentStub, 0, max);
            if (expBar != null)
            {
                expBar.maxValue = max;
                expBar.value    = cur;
            }
            if (expLabel != null)
                expLabel.text = $"{cur}/{max}";
        }

        private IEnumerator TweenHpBar(float from, float to, float duration)
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

        private IEnumerator TweenMaskHeight(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                var size = mask.sizeDelta;
                size.y = Mathf.Lerp(from, to, k);
                mask.sizeDelta = size;
                yield return null;
            }
            var final = mask.sizeDelta;
            final.y = to;
            mask.sizeDelta = final;
            maskTweenCo = null;
        }
    }
}
