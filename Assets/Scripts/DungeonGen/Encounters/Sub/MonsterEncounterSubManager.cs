using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class MonsterEncounterSubManager : IEncounterSubManager
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

            var data = poolData as MonsterEncounterPoolDataSO;
            if (data == null) return;

            if (data.priorityPool != null)
            {
                var priority = new List<EncounterSO>();
                foreach (var e in data.priorityPool) if (e != null) priority.Add(e);
                if (data.randomizePriorityOrder) Shuffle(priority);
                foreach (var e in priority) activeQueue.Enqueue(e);
            }

            if (data.generalPool != null)
            {
                var general = new List<EncounterSO>();
                foreach (var e in data.generalPool) if (e != null) general.Add(e);
                Shuffle(general);
                foreach (var e in general) activeQueue.Enqueue(e);
            }

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
                    type = EncounterType.Monster,
                    slotIndex = slotCounter++,
                };
            }

            var enc = activeQueue.Dequeue();
            seenPool.Add(enc);
            return new EncounterResult
            {
                encounter = enc,
                type = EncounterType.Monster,
                slotIndex = slotCounter++,
            };
        }

        public EncounterResult Peek(int slotIndex)
        {
            int rel = slotIndex - slotCounter;
            if (rel < 0 || rel >= activeQueue.Count)
            {
                return new EncounterResult { type = EncounterType.Monster, slotIndex = slotIndex };
            }
            var arr = activeQueue.ToArray();
            return new EncounterResult
            {
                encounter = arr[rel],
                type = EncounterType.Monster,
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
