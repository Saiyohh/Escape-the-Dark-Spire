using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [Serializable]
    public struct MonsterSpawn
    {
        public MonsterTier tier;
        public Vector2Int start;
        public List<Vector2Int> patrolRoute;

        public MonsterSpawn(MonsterTier tier, Vector2Int start, List<Vector2Int> patrolRoute)
        {
            this.tier = tier;
            this.start = start;
            this.patrolRoute = patrolRoute;
        }
    }
}
