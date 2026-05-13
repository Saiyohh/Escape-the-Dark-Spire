namespace DarkSpire
{
    public interface IEncounterSubManager
    {
        void SetupForFloor(IEncounterPoolData poolData);
        EncounterResult GetNext();
        EncounterResult Peek(int slotIndex);
        bool IsExhausted();
        int RemainingCount();

        int TotalAuthoredCount { get; }
    }
}
