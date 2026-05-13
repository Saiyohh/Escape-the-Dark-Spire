using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
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

        public IReadOnlyList<RoadmapAnnotation> PeekRoadmap() =>
            activeAnnotations != null ? activeAnnotations.annotations : null;

        public IEncounterSubManager PeekSub(EncounterType type) =>
            EncounterManager.Instance != null ? EncounterManager.Instance.PeekSub(type) : null;
    }
}
