using System.Collections.Generic;

namespace DarkSpire
{
    public class SeededRandom
    {
        private readonly System.Random rng;
        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        public int NextInt(int maxExclusive) => rng.Next(maxExclusive);

        public int NextRange(int minInclusive, int maxExclusive) =>
            rng.Next(minInclusive, maxExclusive);

        public int NextRangeInclusive(int minInclusive, int maxInclusive) =>
            rng.Next(minInclusive, maxInclusive + 1);

        public float NextFloat() => (float)rng.NextDouble();

        public bool RollChance(float chance) => rng.NextDouble() < chance;

        public T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
