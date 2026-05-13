using System;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public static class LinkRectMath
    {
        public static int FindIntersectingLinkPadded(
            TMP_Text text,
            Vector2 screenPos,
            Camera cam,
            float inflateFraction,
            Func<int, bool> shouldInflate)
        {
            if (text == null || text.textInfo == null) return -1;
            var info = text.textInfo;

            int linkCount = info.linkCount;
            if (linkCount > info.linkInfo.Length) linkCount = info.linkInfo.Length;

            for (int i = 0; i < linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (link.linkTextLength <= 0) continue;
                int firstChar = link.linkTextfirstCharacterIndex;
                int charCount = link.linkTextLength;
                if (firstChar < 0 || firstChar >= info.characterInfo.Length) continue;

                bool first = true;
                float minX = 0, maxX = 0, minY = 0, maxY = 0;

                for (int c = 0; c < charCount; c++)
                {
                    int idx = firstChar + c;
                    if (idx >= info.characterInfo.Length) break;
                    var ci = info.characterInfo[idx];

                    Vector3 wbl = text.transform.TransformPoint(ci.bottomLeft);
                    Vector3 wtr = text.transform.TransformPoint(ci.topRight);
                    Vector2 sbl = RectTransformUtility.WorldToScreenPoint(cam, wbl);
                    Vector2 str = RectTransformUtility.WorldToScreenPoint(cam, wtr);

                    float x0 = Mathf.Min(sbl.x, str.x);
                    float x1 = Mathf.Max(sbl.x, str.x);
                    float y0 = Mathf.Min(sbl.y, str.y);
                    float y1 = Mathf.Max(sbl.y, str.y);

                    if (first) { minX = x0; maxX = x1; minY = y0; maxY = y1; first = false; }
                    else
                    {
                        if (x0 < minX) minX = x0;
                        if (x1 > maxX) maxX = x1;
                        if (y0 < minY) minY = y0;
                        if (y1 > maxY) maxY = y1;
                    }
                }
                if (first) continue;

                bool inflate = shouldInflate?.Invoke(i) ?? true;
                if (inflate && inflateFraction > 0f)
                {
                    float pw = (maxX - minX) * inflateFraction * 0.5f;
                    float ph = (maxY - minY) * inflateFraction * 0.5f;
                    minX -= pw; maxX += pw;
                    minY -= ph; maxY += ph;
                }

                if (screenPos.x >= minX && screenPos.x <= maxX &&
                    screenPos.y >= minY && screenPos.y <= maxY)
                    return i;
            }

            return -1;
        }

        public static bool TryGetLinkScreenMidTop(TMP_Text text, int linkIdx, out Vector2 screenMidTop)
        {
            screenMidTop = default;
            if (text == null || text.textInfo == null) return false;
            if (linkIdx < 0 || linkIdx >= text.textInfo.linkInfo.Length) return false;

            var info = text.textInfo.linkInfo[linkIdx];
            int firstChar = info.linkTextfirstCharacterIndex;
            int charCount = info.linkTextLength;
            if (charCount <= 0) return false;
            if (firstChar < 0 || firstChar >= text.textInfo.characterInfo.Length) return false;

            int lastChar = firstChar + charCount - 1;
            if (lastChar >= text.textInfo.characterInfo.Length)
                lastChar = text.textInfo.characterInfo.Length - 1;

            // Use the first character's top edge — if the link wraps to a
            // second line we still want the popup pinned to the first line so
            // the cursor doesn't have to chase down a moved popup mid-hover.
            var firstInfo = text.textInfo.characterInfo[firstChar];
            var lastInfo = text.textInfo.characterInfo[lastChar];

            // If the link wraps lines, lastInfo.topRight may be far below
            // firstInfo.topLeft. Clamp horizontally to the first line's width.
            float topY = firstInfo.topLeft.y;
            float minX = firstInfo.topLeft.x;
            float maxX = lastInfo.topRight.x;
            if (lastInfo.topRight.y < firstInfo.topLeft.y - 1f)
                maxX = firstInfo.topRight.x; // wrapped — fall back to single char

            Vector3 localMid = new Vector3((minX + maxX) * 0.5f, topY, 0f);
            Vector3 worldMid = text.transform.TransformPoint(localMid);

            // Find UI camera via the text's parent canvas, then walk to the root
            // — sub-canvases inherit render mode but may have null worldCamera,
            // so trusting the immediate parent here would mis-convert under
            // Camera-mode setups.
            var canvas = text.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null)
            {
                var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                if (root.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = root.worldCamera;
            }

            screenMidTop = RectTransformUtility.WorldToScreenPoint(cam, worldMid);
            return true;
        }
    }
}
