using UnityEngine;

namespace DarkSpire
{
    // The small "!" pop that appears above a monster on Patrol -> Alert.
    // Auto-hides after `duration` seconds. Sprite comes from
    // MapEntitySpriteLibrary.alertIndicator with a fallback yellow square.
    public class AlertIndicator : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float hideAt;
        private Sprite whiteSprite;

        public void Setup(MapEntitySpriteLibrary library, int sortingOrder)
        {
            transform.localPosition = new Vector3(0f, 0.7f, 0f);

            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;

            var authored = library != null ? library.alertIndicator : null;
            if (authored != null)
            {
                sr.sprite = authored;
                sr.color = Color.white;
            }
            else
            {
                EnsureWhiteSprite();
                sr.sprite = whiteSprite;
                sr.color = new Color(1f, 0.9f, 0.2f);
                transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            }

            sr.enabled = false;
        }

        public void Show(float duration)
        {
            if (sr == null) return;
            sr.enabled = true;
            hideAt = Time.time + duration;
        }

        public void Hide()
        {
            if (sr != null) sr.enabled = false;
        }

        private void Update()
        {
            if (sr != null && sr.enabled && Time.time >= hideAt) sr.enabled = false;
        }

        private void EnsureWhiteSprite()
        {
            if (whiteSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(
                tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            whiteSprite.name = "RuntimeAlert";
        }
    }
}
