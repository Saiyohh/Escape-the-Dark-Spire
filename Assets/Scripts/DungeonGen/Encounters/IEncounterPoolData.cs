namespace DarkSpire
{
    // Marker interface so a Sub-Manager can accept its pool SO via the
    // shared SetupForFloor signature without naming a concrete pool type.
    // Each pool SO (MonsterEncounterPoolDataSO, EliteEncounterPoolDataSO,
    // BossEncounterPoolDataSO, TextEncounterPoolDataSO) implements this.
    public interface IEncounterPoolData
    {
    }
}
