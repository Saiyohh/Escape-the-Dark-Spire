using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class TextEncounterSubManager : IEncounterSubManager
    {
        private readonly Queue<string> activeQueue = new();
        private readonly List<string> seenPool = new();
        private int slotCounter;
        private int totalAuthoredCount;
        public int TotalAuthoredCount => totalAuthoredCount;

        private readonly EncounterType type;

        public TextEncounterSubManager(EncounterType type)
        {
            this.type = type;
        }

        public void SetupForFloor(IEncounterPoolData poolData)
        {
            activeQueue.Clear();
            seenPool.Clear();
            slotCounter = 0;
            totalAuthoredCount = 0;

            var data = poolData as TextEncounterPoolDataSO;
            if (data?.textPool == null) return;

            var pool = new List<string>();
            foreach (var s in data.textPool) if (!string.IsNullOrEmpty(s)) pool.Add(s);
            Shuffle(pool);
            foreach (var s in pool) activeQueue.Enqueue(s);

            totalAuthoredCount = activeQueue.Count;
        }

        public EncounterResult GetNext()
        {
            if (activeQueue.Count == 0) ReshuffleAll();

            if (activeQueue.Count == 0)
            {
                return new EncounterResult
                {
                    displayText = "(no text in pool)",
                    type = type,
                    slotIndex = slotCounter++,
                };
            }

            var s = activeQueue.Dequeue();
            seenPool.Add(s);
            return new EncounterResult
            {
                displayText = s,
                type = type,
                slotIndex = slotCounter++,
            };
        }

        public EncounterResult Peek(int slotIndex)
        {
            int rel = slotIndex - slotCounter;
            if (rel < 0 || rel >= activeQueue.Count)
                return new EncounterResult { type = type, slotIndex = slotIndex };
            var arr = activeQueue.ToArray();
            return new EncounterResult
            {
                displayText = arr[rel],
                type = type,
                slotIndex = slotIndex,
            };
        }

        public bool IsExhausted() => activeQueue.Count == 0;
        public int RemainingCount() => activeQueue.Count;

        private void ReshuffleAll()
        {
            if (seenPool.Count == 0) return;
            var combined = new List<string>(seenPool);
            Shuffle(combined);
            seenPool.Clear();
            foreach (var s in combined) activeQueue.Enqueue(s);
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
