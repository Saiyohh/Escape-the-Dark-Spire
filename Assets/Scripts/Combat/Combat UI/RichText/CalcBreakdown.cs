// CalcBreakdown.cs
// -----------------------------------------------------------------------------
// Step-by-step accounting for one ComputedNumber. Every contribution to the
// final value gets its own row so the NumberCalcBox can render the whole math
// chain ("Base 2 + POW 3 + Strength 1 = 6").
//
// Steps reduce left-to-right with their op:
//   Add    : running += step.Value (rendered as "+N" or "-N")
//   Mult   : running = floor(running × step.Multiplier) (rendered as "×1.5")
//   Initial: sets the running total (used for Base — first step always Initial)
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace DarkSpire
{
    public enum BreakdownOp
    {
        Initial,    // Replaces running total — used for the Base row.
        Add,        // running += value
        Mult,       // running = floor(running * multiplier)
    }

    public struct BreakdownStep
    {
        public string Label;
        public int Value;          // Used for Initial / Add ops (final integer contribution).
        public float Multiplier;   // Used for Mult op.
        public BreakdownOp Op;

        public static BreakdownStep MakeInitial(string label, int value) =>
            new BreakdownStep { Label = label, Value = value, Op = BreakdownOp.Initial };

        public static BreakdownStep MakeAdd(string label, int value) =>
            new BreakdownStep { Label = label, Value = value, Op = BreakdownOp.Add };

        public static BreakdownStep MakeMult(string label, float multiplier) =>
            new BreakdownStep { Label = label, Multiplier = multiplier, Op = BreakdownOp.Mult };
    }

    /// <summary>
    /// Snapshot of one number's calculation. Steps are authored in order and the
    /// running total is computed during Display evaluation; CritTotal is computed
    /// separately by re-running with isCrit=true. Total may be 0 if BaseMagnitude
    /// is 0 with no scaling — that's still a valid display.
    /// </summary>
    public struct CalcBreakdown
    {
        public List<BreakdownStep> Steps;
        public int Total;
        /// <summary>Total assuming a crit (base × 2 before stat add). 0 when not applicable (heals, etc.).</summary>
        public int CritTotal;
        public bool ShowCritLine;
    }
}
