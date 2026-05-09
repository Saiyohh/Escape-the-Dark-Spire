using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Persistent run-scoped service. Owns the active floor's roadmap
    // annotations and applies them to every EncounterResult flowing through
    // EncounterManager dispatch. Lives outside the EM so the knowledge
    // boundary is enforced: only this class (and editor debug overlays)
    // can Peek Sub-Manager queues. Gameplay code never calls Peek.
    public class DungeonManager : MonoBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        private FloorRoadmapAnnotationsSO activeAnnotations;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetupForFloor(FloorRoadmapAnnotationsSO annotations)
        {
            activeAnnotations = annotations;
        }

        // Called by EncounterManager.Apply for every Get*Encounter result.
        // Walks the annotation list for a matching (type, slotIndex) pair and
        // populates result.rewardOverride if found. Annotation lookup is
        // linear; floors realistically have <20 annotations so this is fine.
        public EncounterResult ApplyAnnotations(EncounterResult result)
        {
            if (activeAnnotations?.annotations == null) return result;

            for (int i = 0; i < activeAnnotations.annotations.Count; i++)
            {
                var a = activeAnnotations.annotations[i];
                if (a.targetType == result.type && a.slotIndex == result.slotIndex)
                {
                    result.rewardOverride = a.reward;
                    if (a.reward.HasAny)
                    {
                        Debug.Log($"[DM] Annotation applied: {result.type} slot {result.slotIndex} -> " +
                                  $"key={a.reward.guaranteedKey}, gold+{a.reward.bonusGold}, " +
                                  $"item={(a.reward.bonusItemDrop != null ? a.reward.bonusItemDrop.name : "(none)")}");
                    }
                    return result;
                }
            }
            return result;
        }

        // Debug-only access for the overlay. Returns the active annotations
        // (may be null) and per-Sub-Manager queue inspection via the EM.
        public IReadOnlyList<RoadmapAnnotation> PeekRoadmap() =>
            activeAnnotations != null ? activeAnnotations.annotations : null;

        public IEncounterSubManager PeekSub(EncounterType type) =>
            EncounterManager.Instance != null ? EncounterManager.Instance.PeekSub(type) : null;
    }
}
