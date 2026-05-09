// WorldSpaceHealthBar.cs
// -----------------------------------------------------------------------------
// Drop-in world-space HP + SP bar for any UnitDisplay. Auto-builds its own
// Canvas + Image hierarchy in Awake so no prefab wiring is required — add the
// component to a UnitDisplay (or call SetUnit manually) and the bars appear
// under the sprite.
//
// Intended use: visual feedback for the Pass 1 combat smoke test, BEFORE the
// real UnitWorldHUD UI port in Pass 3. Delete when the real HUD lands.
//
// Subscribes to Unit.OnStatsChanged for live updates. Enemy units with
// maxSP == 0 hide the SP bar automatically.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tools/World Space Health Bar")]
    public class WorldSpaceHealthBar : MonoBehaviour
    {
        [Tooltip("World-space Y offset from the owning Transform (negative = below feet).")]
        public Vector3 offset = new Vector3(0f, -1.2f, 0f);

        [Tooltip("Size of the bar group in world units (width, height per bar).")]
        public Vector2 barSize = new Vector2(1.3f, 0.12f);

        [Tooltip("Spacing between HP and SP bars in world units.")]
        public float barSpacing = 0.04f;

        [Header("Colors")]
        public Color hpColor = new Color(0.85f, 0.18f, 0.18f);
        public Color hpBgColor = new Color(0.15f, 0.05f, 0.05f, 0.85f);
        public Color spColor = new Color(0.22f, 0.55f, 1.00f);
        public Color spBgColor = new Color(0.05f, 0.10f, 0.25f, 0.85f);

        private Unit linkedUnit;
        private Image hpFill, spFill, hpBg, spBg;
        private RectTransform spRow;
        private Canvas canvas;

        private void Awake()
        {
            BuildUI();
            // Try to auto-link if sitting on a UnitDisplay
            var display = GetComponent<UnitDisplay>();
            if (display != null && display.LinkedUnit != null)
                SetUnit(display.LinkedUnit);
        }

        private void OnDestroy()
        {
            if (linkedUnit != null)
                linkedUnit.OnStatsChanged -= Refresh;
        }

        public void SetUnit(Unit unit)
        {
            if (linkedUnit != null)
                linkedUnit.OnStatsChanged -= Refresh;
            linkedUnit = unit;
            if (linkedUnit != null)
            {
                linkedUnit.OnStatsChanged += Refresh;
                // Hide SP row for enemies / units with no SP pool
                if (spRow != null) spRow.gameObject.SetActive(linkedUnit.maxSP > 0);
            }
            Refresh();
        }

        private void Refresh()
        {
            if (linkedUnit == null || hpFill == null) return;

            float hpPct = linkedUnit.maxHP > 0
                ? (float)linkedUnit.currentHP / linkedUnit.maxHP
                : 0f;
            hpFill.fillAmount = Mathf.Clamp01(hpPct);

            if (linkedUnit.maxSP > 0 && spFill != null)
            {
                float spPct = (float)linkedUnit.currentSP / linkedUnit.maxSP;
                spFill.fillAmount = Mathf.Clamp01(spPct);
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("__HealthBarCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = offset;
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();

            var canvasRT = (RectTransform)canvas.transform;
            canvasRT.sizeDelta = new Vector2(barSize.x, barSize.y * 2 + barSpacing);
            canvasRT.localScale = Vector3.one * 0.01f;

            var hpRow = CreateBarRow(canvasRT, "HP", hpBgColor, hpColor,
                new Vector2(0f, (barSize.y + barSpacing) * 0.5f));
            hpBg = hpRow.bg;
            hpFill = hpRow.fill;

            var sp = CreateBarRow(canvasRT, "SP", spBgColor, spColor,
                new Vector2(0f, -(barSize.y + barSpacing) * 0.5f));
            spRow = sp.rt;
            spBg = sp.bg;
            spFill = sp.fill;
        }

        private (RectTransform rt, Image bg, Image fill) CreateBarRow(
            RectTransform parent, string name, Color bgCol, Color fillCol, Vector2 pos)
        {
            var rowGO = new GameObject(name);
            rowGO.transform.SetParent(parent, false);
            var rt = rowGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(barSize.x * 100f, barSize.y * 100f);
            rt.anchoredPosition = pos * 100f;

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            bg.color = bgCol;

            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(rt, false);
            var fillRT = fill.rectTransform;
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            fill.color = fillCol;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;

            return (rt, bg, fill);
        }
    }
}
