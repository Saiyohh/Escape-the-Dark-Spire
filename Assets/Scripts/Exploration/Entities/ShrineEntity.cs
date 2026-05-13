using UnityEngine;

namespace DarkSpire
{
    public class ShrineEntity : MapEntityBase, IInteractable
    {
        public string PromptText => used ? "" : "[E] Pray at shrine";
        public bool CanInteract(in InteractionContext ctx) => !used;

        private bool used;

        public override void Initialize(EntityPlacement placement, SpriteRenderer sr, MapEntitySpriteLibrary library)
        {
            base.Initialize(placement, sr, library);
            if (RunStateHolder.Instance != null && RunStateHolder.Instance.IsShrineUsed(GridPos))
            {
                used = true;
                if (sr != null)
                {
                    var c = sr.color;
                    c *= 0.6f;
                    c.a = 1f;
                    sr.color = c;
                }
            }
        }

        public void Interact(in InteractionContext ctx)
        {
            if (used) return;
            used = true;
            RunStateHolder.Instance?.usedShrines.Add(GridPos);

            var em = EncounterManager.Instance;
            if (em != null)
            {
                var result = em.GetShrineEncounter();
                string text = string.IsNullOrEmpty(result.displayText)
                    ? "The shrine glows, then falls silent."
                    : result.displayText;
                Debug.Log($"[Shrine] (slot {result.slotIndex}) {text}");
                DungeonEvents.InvokeFlashMessage(text);
            }
            else
            {
                Debug.LogWarning("[Shrine] No EncounterManager.Instance — falling back to stub log.");
            }

            if (sr != null)
            {
                var c = sr.color;
                c *= 0.6f;
                c.a = 1f;
                sr.color = c;
            }
        }
    }
}
