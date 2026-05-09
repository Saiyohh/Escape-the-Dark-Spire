namespace DarkSpire
{
    // Common shape for every Sub-Manager. SetupForFloor wipes runtime state
    // and rebuilds queues from the supplied pool data. GetNext pops the
    // head of the active queue and returns an EncounterResult tagged with
    // its 0-indexed slotIndex (= number of GetNext calls before this one).
    //
    // Knowledge boundary: ONLY the Dungeon Manager and debug code may call
    // Peek. Map entities and gameplay code use GetNext exclusively, which
    // mutates state.
    public interface IEncounterSubManager
    {
        void SetupForFloor(IEncounterPoolData poolData);
        EncounterResult GetNext();
        EncounterResult Peek(int slotIndex);
        bool IsExhausted();
        int RemainingCount();

        // Total entries loaded at SetupForFloor (priority + general for the
        // monster pool, flat pool count for elite/boss/text). 0 means no
        // pool was authored — distinct from "exhausted with N seen".
        int TotalAuthoredCount { get; }
    }
}
