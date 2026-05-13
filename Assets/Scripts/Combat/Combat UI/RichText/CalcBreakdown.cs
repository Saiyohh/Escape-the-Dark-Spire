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

    public struct CalcBreakdown
    {
        public List<BreakdownStep> Steps;
        public int Total;
        public int CritTotal;
        public bool ShowCritLine;
    }
}
