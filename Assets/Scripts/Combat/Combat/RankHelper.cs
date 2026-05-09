// RankHelper.cs
// -----------------------------------------------------------------------------
// Static utilities for the 4-rank position system. Each side of combat has
// four ranks numbered 1 (front, closest to the enemy) through 4 (back).
// Players occupy ranks 1-4 on the left; enemies occupy ranks 1-4 on the right.
// Per-side rank spaces are INDEPENDENT — moving a player doesn't affect enemy
// ranks and vice versa.
//
// Distance rule (Combat GDD):
//   Distance(playerUnit, enemyUnit) = playerUnit.currentRank + enemyUnit.currentRank - 1
// which gives distance 1 when both are at the front, up to 7 when both are at
// the back. Skills check a `rangeMin..rangeMax` window against this distance.
//
// Movement rule:
//   When a unit moves from rank A to rank B on their side, every other unit
//   whose current rank lies strictly between A and B shifts by one step in the
//   opposite direction to fill the mover's vacated slot. This matches the
//   "adjacent characters shift to fill or make room" behavior spelled out in
//   the Combat GDD.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class RankHelper
    {
        public const int MinRank = 1;
        public const int MaxRank = 4;

        /// <summary>
        /// Distance between a unit on one side and a unit on the other side.
        /// Caller's responsibility to pass cross-side units; a same-side call
        /// doesn't carry meaning in this model.
        /// </summary>
        public static int Distance(Unit a, Unit b)
        {
            if (a == null || b == null) return int.MaxValue;
            return a.currentRank + b.currentRank - 1;
        }

        /// <summary>
        /// True if `target` sits within [rangeMin, rangeMax] distance of `caster`.
        /// Same-side targets (ally-targeting skills) use their own rank diff +1
        /// convention which is equivalent when both sides share the calculation.
        /// </summary>
        public static bool InRange(Unit caster, Unit target, int rangeMin, int rangeMax)
        {
            int d = Distance(caster, target);
            return d >= rangeMin && d <= rangeMax;
        }

        /// <summary>
        /// Move `mover` to `targetRank` within `sideLineup` (the mover's own side).
        /// Intervening units slide by one step to fill/make room. Clamps the
        /// target to [MinRank, MaxRank]. No-op if the mover is already there.
        ///
        /// Returns the actual target rank used (post-clamp), so callers that
        /// passed a relative offset (e.g. Advance 3 from rank 4) can log the
        /// effective distance traveled.
        /// </summary>
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

        /// <summary>Advance: user moves toward front (lower rank). Auto-succeeds.</summary>
        public static int Advance(List<Unit> sideLineup, Unit mover, int steps)
            => MoveUnit(sideLineup, mover, mover.currentRank - Mathf.Max(0, steps));

        /// <summary>Withdraw: user moves toward back (higher rank). Auto-succeeds.</summary>
        public static int Withdraw(List<Unit> sideLineup, Unit mover, int steps)
            => MoveUnit(sideLineup, mover, mover.currentRank + Mathf.Max(0, steps));

        /// <summary>
        /// Pull: target moves toward its side's front (lower rank), i.e. closer
        /// to the caster's side. WIL-save resistance is the caller's job —
        /// SkillResolver.ResolveMovement does the roll via DiceRoller.SaveRoll
        /// when the Move effect carries a saveDC > 0.
        /// </summary>
        public static int Pull(List<Unit> targetSideLineup, Unit target, int steps)
            => MoveUnit(targetSideLineup, target, target.currentRank - Mathf.Max(0, steps));

        /// <summary>
        /// Knockback: target moves toward its side's back (higher rank), i.e.
        /// away from the caster's side.
        /// </summary>
        public static int Knockback(List<Unit> targetSideLineup, Unit target, int steps)
            => MoveUnit(targetSideLineup, target, target.currentRank + Mathf.Max(0, steps));

        /// <summary>Shuffle: target teleports to a random rank within its side.</summary>
        public static int Shuffle(List<Unit> targetSideLineup, Unit target)
        {
            int r = Random.Range(MinRank, MaxRank + 1);
            return MoveUnit(targetSideLineup, target, r);
        }

        /// <summary>
        /// After a death: walk every alive unit on this side in current rank
        /// order and reassign them to ranks MinRank, MinRank+1, ... so the
        /// front-most rank stays at MinRank and the gap left by the dead unit
        /// closes. SetRank fires OnRankChanged and the UnitDisplay lerps into
        /// position automatically.
        /// </summary>
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
