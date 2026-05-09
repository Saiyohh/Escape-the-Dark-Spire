using System.Collections.Generic;

namespace DarkSpire
{
    // Deterministic random source. All generation-time randomness funnels
    // through one instance of this so a stored seed reproduces a floor.
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

        // [minInclusive, maxExclusive)
        public int NextRange(int minInclusive, int maxExclusive) =>
            rng.Next(minInclusive, maxExclusive);

        // [minInclusive, maxInclusive] — convenience for ranges authored as
        // inclusive in the GDD ("3-6 tiles", "6-8 rooms").
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
