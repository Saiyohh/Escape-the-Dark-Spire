// InteractPromptIndicator.cs
// -----------------------------------------------------------------------------
// Floating "[E] ..." prompt that appears above an interactable tile while the
// party is in range. Modeled after AlertIndicator but text-driven (TMP 3D) so
// each entity can author its own PromptText.
//
// Two ranges:
//   adjacency  — show when party is on the anchor tile OR a cardinal neighbour.
//                Used by Chest / Shrine / BossGate (which call Interact via E
//                from the four neighbours plus the anchor tile itself).
//   own-tile   — show only when party is exactly on the anchor tile. Used by
//                the Rest overlay (E is captured by DungeonWalkoverDispatcher
//                from the party's own position, not adjacent).
//
// Lifecycle: the indicator polls PartyToken.Instance in Update because entities
// are spawned BEFORE the party token exists (EntitySpawner.SpawnEntities runs
// before SpawnParty in DungeonBootstrap). Polling is one Vector2Int diff per
// frame per indicator — trivial cost for clarity gains.
// -----------------------------------------------------------------------------
using System;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    public class InteractPromptIndicator : MonoBehaviour
    {
        public enum Range { Adjacency, OwnTile }

        private Func<string> promptProvider;
        private Vector2Int anchorPos;
        private Range range;
        private TextMeshPro label;
        private PartyToken party;

        /// <summary>
        /// Configure and attach to whatever transform this component lives on.
        /// </summary>
        public void Setup(
            Vector2Int anchorPos,
            Func<string> promptProvider,
            Range range,
            int sortingOrder)
        {
            this.anchorPos = anchorPos;
            this.promptProvider = promptProvider;
            this.range = range;

            label = GetComponent<TextMeshPro>();
            if (label == null) label = gameObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            // World-space TMP fontSize is in world units (≈ line height). 0.5
            // = half-tile-tall text, which reads well above a 1-unit tile.
            // Auto-sizing within a band so the rect dictates fit, not the
            // hardcoded size — keeps the label legible even when a long
            // prompt ("[E] Open gate (0/2)") would otherwise overflow.
            label.enableAutoSizing = true;
            label.fontSizeMin = 1.6f;
            label.fontSizeMax = 2.8f;
            label.color = new Color(1f, 0.92f, 0.45f);
            label.outlineColor = new Color(0f, 0f, 0f, 1f);
            label.outlineWidth = 0.18f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingOrder = sortingOrder;
            label.text = string.Empty;
            UIFonts.Apply(label);

            // Generous rect so even long prompts have room. Centered above
            // the tile via localPosition.
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(6f, 3f);
            rt.localPosition = Vector3.zero;

            // Counteract the parent entity's sprite-scale override so the
            // prompt always reads at the same world size regardless of how
            // the icon underneath is scaled. The entity's scale is set by
            // EntitySpawner BEFORE Initialize fires (which is when AttachTo
            // runs), so parent.localScale is already final here. Without this
            // a chest scaled to 0.5 would render its prompt at half-size.
            var parent = transform.parent;
            Vector3 parentScale = parent != null ? parent.localScale : Vector3.one;
            float invX = Mathf.Abs(parentScale.x) > 0.001f ? 1f / parentScale.x : 1f;
            float invY = Mathf.Abs(parentScale.y) > 0.001f ? 1f / parentScale.y : 1f;
            transform.localScale = new Vector3(invX, invY, 1f);
            // localPosition is multiplied by parent scale to get world offset,
            // so push the indicator up by 0.85 WORLD units (= 0.85 / parentScale
            // local units) so it sits the same distance above every entity
            // regardless of the entity's sprite scale.
            transform.localPosition = new Vector3(0f, 1.2f * invY, 0f);

            label.enabled = false;
        }

        private void Update()
        {
            if (party == null)
            {
                party = PartyToken.Instance;
                if (party == null) return;
            }
            Refresh(party.GridPos);
        }

        private void Refresh(Vector2Int partyPos)
        {
            if (label == null) return;

            bool inRange = range == Range.OwnTile
                ? partyPos == anchorPos
                : Manhattan(partyPos, anchorPos) <= 1;

            if (!inRange)
            {
                if (label.enabled) label.enabled = false;
                return;
            }

            string text = promptProvider != null ? promptProvider() : null;
            if (string.IsNullOrEmpty(text))
            {
                if (label.enabled) label.enabled = false;
                return;
            }

            if (!label.enabled) label.enabled = true;
            if (label.text != text) label.text = text;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>
        /// Spawn a child GameObject with an InteractPromptIndicator attached
        /// and configured. Returns the indicator so callers can hold a ref if
        /// they need to retune later.
        /// </summary>
        public static InteractPromptIndicator AttachTo(
            Transform parent,
            Vector2Int anchorPos,
            Func<string> promptProvider,
            Range range,
            int sortingOrder)
        {
            var go = new GameObject("InteractPrompt");
            go.transform.SetParent(parent, worldPositionStays: false);
            var ind = go.AddComponent<InteractPromptIndicator>();
            ind.Setup(anchorPos, promptProvider, range, sortingOrder);
            return ind;
        }
    }
}
