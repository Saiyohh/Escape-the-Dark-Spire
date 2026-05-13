using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class NumberCalcBoxView : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [Tooltip("Legacy header label slot. The new format has no Total line; if " +
                 "this is wired we hide it at runtime. Safe to leave null on new prefabs.")]
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        public RectTransform PanelRoot => panelRoot;

        private readonly StringBuilder sb = new(128);
        private CanvasGroup canvasGroup;
        private bool headerHiddenOnce;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetBreakdown(in CalcBreakdown breakdown)
        {
            if (headerLabel != null && !headerHiddenOnce)
            {
                headerLabel.gameObject.SetActive(false);
                headerHiddenOnce = true;
            }

            if (bodyLabel == null) return;

            sb.Clear();

            if (breakdown.Steps != null)
            {
                bool wroteAddRow = false;
                for (int i = 0; i < breakdown.Steps.Count; i++)
                {
                    var s = breakdown.Steps[i];
                    if (s.Op == BreakdownOp.Mult) continue;

                    if (s.Op == BreakdownOp.Initial)
                    {
                        AppendLabelValue(s.Label, s.Value, isFirst: !wroteAddRow);
                    }
                    else if (s.Op == BreakdownOp.Add)
                    {
                        AppendLabelValue(s.Label, s.Value, isFirst: !wroteAddRow);
                    }
                    wroteAddRow = true;
                }

                for (int i = 0; i < breakdown.Steps.Count; i++)
                {
                    var s = breakdown.Steps[i];
                    if (s.Op != BreakdownOp.Mult) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("× ");
                    sb.Append(string.IsNullOrEmpty(s.Label) ? "—" : s.Label);
                    sb.Append(" (");
                    sb.Append(s.Multiplier.ToString("0.##", CultureInfo.InvariantCulture));
                    sb.Append("x)");
                }
            }

            if (breakdown.ShowCritLine)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<color=#888888>CRIT: 2x</color>");
            }

            bodyLabel.text = sb.ToString();
        }

        private void AppendLabelValue(string label, int value, bool isFirst)
        {
            if (!isFirst)
            {
                sb.Append(value >= 0 ? " + " : " - ");
            }
            else if (value < 0)
            {
                sb.Append('-');
            }

            sb.Append(string.IsNullOrEmpty(label) ? "—" : label);
            sb.Append(" (");
            int absVal = value < 0 ? -value : value;
            sb.Append(absVal.ToString(CultureInfo.InvariantCulture));
            sb.Append(')');
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (canvasGroup != null && visible) canvasGroup.alpha = 1f;
        }

        public void SetScreenPosition(Vector2 screenPoint)
        {
            if (panelRoot == null) return;
            panelRoot.pivot = new Vector2(0.5f, 0.5f);

            var parentRT = panelRoot.parent as RectTransform;
            if (parentRT == null)
            {
                panelRoot.position = screenPoint;
                return;
            }

            Camera cam = null;
            var canvas = parentRT.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                if (root.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = root.worldCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRT, screenPoint, cam, out Vector2 localPoint))
            {
                panelRoot.localPosition = localPoint;
            }
        }
    }
}
