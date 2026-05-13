using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class PartyHpBar : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image spFill;
        [SerializeField] private TMP_Text label;

        public void Bind(PartyMemberRuntime pm)
        {
            if (pm == null || pm.characterData == null) return;
            if (portrait != null)
            {
                portrait.sprite = pm.characterData.headIcon;
                portrait.enabled = pm.characterData.headIcon != null;
            }
            float hpFrac = pm.characterData.maxHP > 0
                ? Mathf.Clamp01((float)pm.currentHP / pm.characterData.maxHP)
                : 0f;
            float spFrac = pm.characterData.maxSP > 0
                ? Mathf.Clamp01((float)pm.currentSP / pm.characterData.maxSP)
                : 0f;
            if (hpFill != null) hpFill.fillAmount = hpFrac;
            if (spFill != null) spFill.fillAmount = spFrac;
            if (label  != null)
                label.text = $"{pm.characterData.characterName}\n{pm.currentHP}/{pm.characterData.maxHP}";
        }

        public static PartyHpBar BuildRuntime(Transform parent)
        {
            var go = new GameObject("PartyHpBar");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 56f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220f;
            le.preferredHeight = 56f;
            le.minWidth = 220f;
            le.minHeight = 56f;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var portraitGO = new GameObject("Portrait");
            portraitGO.transform.SetParent(go.transform, false);
            var prt = portraitGO.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot     = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(48f, 0f);
            prt.anchoredPosition = new Vector2(4f, 0f);
            var portrait = portraitGO.AddComponent<Image>();
            portrait.preserveAspect = true;

            var hpBgGO = new GameObject("HPBg");
            hpBgGO.transform.SetParent(go.transform, false);
            var hpBgRT = hpBgGO.AddComponent<RectTransform>();
            hpBgRT.anchorMin = new Vector2(0f, 0.5f);
            hpBgRT.anchorMax = new Vector2(1f, 0.5f);
            hpBgRT.pivot     = new Vector2(0f, 0.5f);
            hpBgRT.offsetMin = new Vector2(58f, 4f);
            hpBgRT.offsetMax = new Vector2(-8f, 22f);
            var hpBgImg = hpBgGO.AddComponent<Image>();
            hpBgImg.color = new Color(0.15f, 0.05f, 0.05f, 1f);

            var hpFillGO = new GameObject("HPFill");
            hpFillGO.transform.SetParent(hpBgGO.transform, false);
            var hpFillRT = hpFillGO.AddComponent<RectTransform>();
            hpFillRT.anchorMin = Vector2.zero;
            hpFillRT.anchorMax = Vector2.one;
            hpFillRT.offsetMin = Vector2.zero;
            hpFillRT.offsetMax = Vector2.zero;
            var hpFillImg = hpFillGO.AddComponent<Image>();
            hpFillImg.color = new Color(0.85f, 0.20f, 0.20f, 1f);
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImg.fillAmount = 1f;

            var spBgGO = new GameObject("SPBg");
            spBgGO.transform.SetParent(go.transform, false);
            var spBgRT = spBgGO.AddComponent<RectTransform>();
            spBgRT.anchorMin = new Vector2(0f, 0f);
            spBgRT.anchorMax = new Vector2(1f, 0f);
            spBgRT.pivot     = new Vector2(0f, 0f);
            spBgRT.offsetMin = new Vector2(58f, 4f);
            spBgRT.offsetMax = new Vector2(-8f, 12f);
            var spBgImg = spBgGO.AddComponent<Image>();
            spBgImg.color = new Color(0.05f, 0.10f, 0.18f, 1f);

            var spFillGO = new GameObject("SPFill");
            spFillGO.transform.SetParent(spBgGO.transform, false);
            var spFillRT = spFillGO.AddComponent<RectTransform>();
            spFillRT.anchorMin = Vector2.zero;
            spFillRT.anchorMax = Vector2.one;
            spFillRT.offsetMin = Vector2.zero;
            spFillRT.offsetMax = Vector2.zero;
            var spFillImg = spFillGO.AddComponent<Image>();
            spFillImg.color = new Color(0.30f, 0.55f, 0.90f, 1f);
            spFillImg.type = Image.Type.Filled;
            spFillImg.fillMethod = Image.FillMethod.Horizontal;
            spFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            spFillImg.fillAmount = 1f;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot     = new Vector2(0f, 1f);
            labelRT.offsetMin = new Vector2(58f, -28f);
            labelRT.offsetMax = new Vector2(-8f, -2f);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            var bar = go.AddComponent<PartyHpBar>();
            bar.portrait = portrait;
            bar.hpFill = hpFillImg;
            bar.spFill = spFillImg;
            bar.label = tmp;
            return bar;
        }
    }
}
