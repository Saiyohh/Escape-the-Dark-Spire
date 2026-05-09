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

            var data = poolData as BossEncounterPoolDataSO;
            if (data?.bossPool == null || data.bossPool.Count == 0) return;

            int idx = Random.Range(0, data.bossPool.Count);
            selectedBoss = data.bossPool[idx];
        }

        public EncounterResult GetNext()
        {
            if (consumed)
            {
                Debug.LogError("[Boss] GetNext called twice on the same floor.");
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
