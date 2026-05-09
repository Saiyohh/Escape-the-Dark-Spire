using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Flat shuffled pool with seen-pool reshuffle on exhaustion. No priority.
    public class EliteEncounterSubManager : IEncounterSubManager
    {
        private readonly Queue<EncounterSO> activeQueue = new();
        private readonly List<EncounterSO> seenPool = new();
        private int slotCounter;
        private int totalAuthoredCount;
        public int TotalAuthoredCount => totalAuthoredCount;

        public void SetupForFloor(IEncounterPoolData poolData)
        {
            activeQueue.Clear();
            seenPool.Clear();
            slotCounter = 0;
            totalAuthoredCount = 0;

            var data = poolData as EliteEncounterPoolDataSO;
            if (data?.elitePool == null) return;

            var pool = new List<EncounterSO>();
            foreach (var e in data.elitePool) if (e != null) pool.Add(e);
            Shuffle(pool);
            foreach (var e in pool) activeQueue.Enqueue(e);

            totalAuthoredCount = activeQueue.Count;
        }

        public EncounterResult GetNext()
        {
            if (activeQueue.Count == 0) ReshuffleAll();
            if (activeQueue.Count == 0)
            {
                return new EncounterResult
                {
                    encounter = null,
                    type = EncounterType.Elite,
                    slotIndex = slotCounter++,
                };
            }
            var enc = activeQueue.Dequeue();
            seenPool.Add(enc);
            return new EncounterResult
            {
                encounter = enc,
                type = EncounterType.Elite,
                slotIndex = slotCounter++,
            };
        }

        public EncounterResult Peek(int slotIndex)
        {
            int rel = slotIndex - slotCounter;
            if (rel < 0 || rel >= activeQueue.Count)
                return new EncounterResult { type = EncounterType.Elite, slotIndex = slotIndex };
            var arr = activeQueue.ToArray();
            return new EncounterResult
            {
                encounter = arr[rel],
                type = EncounterType.Elite,
                slotIndex = slotIndex,
            };
        }

        public bool IsExhausted() => activeQueue.Count == 0;
        public int RemainingCount() => activeQueue.Count;

        private void ReshuffleAll()
        {
            if (seenPool.Count == 0) return;
            var combined = new List<EncounterSO>(seenPool);
            Shuffle(combined);
            seenPool.Clear();
            foreach (var e in combined) activeQueue.Enqueue(e);
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
