using System;

namespace DarkSpire
{
    [Serializable]
    public struct GenerationStats
    {
        public int totalTiles;
        public int floorTiles;
        public int wallTiles;
        public int roomCount;
        public int deadEndCount;
        public int extrasPlaced;
        public int criticalPathLength;
        public float generationTimeMs;
        public int regenerationAttempts;
    }
}
