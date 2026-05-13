using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class RankHelper
    {
        public const int MinRank = 1;
        public const int MaxRank = 4;

        public static int Distance(Unit a, Unit b)
        {
            if (a == null || b == null) return int.MaxValue;
            return a.currentRank + b.currentRank - 1;
        }

        public static bool InRange(Unit caster, Unit target, int rangeMin, int rangeMax)
        {
            int d = Distance(caster, target);
            return d >= rangeMin && d <= rangeMax;
        }

        public static int MoveUnit(List<Unit> sideLineup, Unit mover, int targetRank)
        {
            if (mover == null || sideLineup == null) return -1;

            int clamped = Mathf.Clamp(targetRank, MinRank, MaxRank);
            int oldRank = mover.currentRank;
            if (clamped == oldRank) return oldRank;

            // Direction of travel (-1 = toward front, +1 = toward back)
            int step = clamped > oldRank ? 1 : -1;

            foreach (var u in sideLineup)
            {
                if (u == null || u == mover) continue;
                int r = u.currentRank;
                // Is this unit strictly between old and new rank?
                bool between =
                    (step > 0 && r > oldRank && r <= clamped) ||
                    (step < 0 && r < oldRank && r >= clamped);
                if (!between) continue;

                // Shift this unit TOWARD the mover's old position (opposite step).
                u.SetRank(r - step);
            }

            mover.SetRank(clamped);
            return clamped;
        }

        // ── Convenience wrappers matching the GDD's movement keywords ──────────

        public static int Advance(List<Unit> sideLineup, Unit mover, int steps)
            => MoveUnit(sideLineup, mover, mover.currentRank - Mathf.Max(0, steps));

        public static int Withdraw(List<Unit> sideLineup, Unit mover, int steps)
            => MoveUnit(sideLineup, mover, mover.currentRank + Mathf.Max(0, steps));

        public static int Pull(List<Unit> targetSideLineup, Unit target, int steps)
            => MoveUnit(targetSideLineup, target, target.currentRank - Mathf.Max(0, steps));

        public static int Knockback(List<Unit> targetSideLineup, Unit target, int steps)
            => MoveUnit(targetSideLineup, target, target.currentRank + Mathf.Max(0, steps));

        public static int Shuffle(List<Unit> targetSideLineup, Unit target)
        {
            int r = Random.Range(MinRank, MaxRank + 1);
            return MoveUnit(targetSideLineup, target, r);
        }

        public static void CompactRanks(List<Unit> sideLineup)
        {
            if (sideLineup == null) return;

            var alive = new List<Unit>();
            foreach (var u in sideLineup)
                if (u != null && u.IsAlive) alive.Add(u);
            alive.Sort((a, b) => a.currentRank.CompareTo(b.currentRank));

            for (int i = 0; i < alive.Count; i++)
            {
                int newRank = MinRank + i;
                if (newRank > MaxRank) break;
                if (alive[i].currentRank != newRank)
                    alive[i].SetRank(newRank);
            }
        }
    }
}
