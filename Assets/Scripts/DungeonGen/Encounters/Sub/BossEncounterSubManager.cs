using UnityEngine;

namespace DarkSpire
{
    // One boss per floor. Picked at floor setup, locked for the run on this
    // floor, fired exactly once via GetNext. No reshuffle path.
    public class BossEncounterSubManager : IEncounterSubManager
    {
        private EncounterSO selectedBoss;
        private bool consumed;
        public int TotalAuthoredCount => selectedBoss != null ? 1 : 0;

        public void SetupForFloor(IEncounterPoolData poolData)
        {
            consumed = false;
            selectedBoss = null;

            if (poolData == null)
            {
                Debug.LogWarning("[Boss] SetupForFloor: poolData is null. " +
                    "FED_Floor1.bosses is not wired, or floor.encounterPool is null. " +
                    "No boss encounter will load when the player reaches the boss room.");
                return;
            }

            var data = poolData as BossEncounterPoolDataSO;
            if (data == null)
            {
                Debug.LogError($"[Boss] SetupForFloor: poolData is a " +
                    $"'{poolData.GetType().Name}', expected BossEncounterPoolDataSO. " +
                    "Check FED_Floor1.bosses points to a BEP_*.asset (Boss Pool).");
                return;
            }

            if (data.bossPool == null || data.bossPool.Count == 0)
            {
                Debug.LogError($"[Boss] SetupForFloor: '{data.name}' has an empty " +
                    "bossPool list. Drag an ENC_Boss_*.asset into it and save.");
                return;
            }

            int idx = Random.Range(0, data.bossPool.Count);
            selectedBoss = data.bossPool[idx];

            if (selectedBoss == null)
            {
                Debug.LogError($"[Boss] SetupForFloor: '{data.name}'.bossPool[{idx}] " +
                    "is a null/missing reference. The pool has an empty slot — " +
                    "click the empty entry and re-assign an ENC_Boss_*.asset.");
            }
            else
            {
                Debug.Log($"[Boss] SetupForFloor: selected '{selectedBoss.name}' " +
                    $"(pool index {idx} of {data.bossPool.Count}).");
            }
        }

        public EncounterResult GetNext()
        {
            if (consumed)
            {
                Debug.LogError("[Boss] GetNext called twice on the same floor.");
            }
            if (selectedBoss == null)
            {
                Debug.LogError("[Boss] GetNext: selectedBoss is null — " +
                    "SetupForFloor was never called, was called with bad data, " +
                    "or this is a resume path that lost EM state. Combat will " +
                    "fall back to the mock-destroy path.");
            }
            consumed = true;
            return new EncounterResult
            {
                encounter = selectedBoss,
                type = EncounterType.Boss,
                slotIndex = 0,
            };
        }

        public EncounterResult Peek(int slotIndex) =>
            new EncounterResult
            {
                encounter = selectedBoss,
                type = EncounterType.Boss,
                slotIndex = 0,
            };

        public bool IsExhausted() => consumed;
        public int RemainingCount() => consumed ? 0 : (selectedBoss != null ? 1 : 0);
    }
}
