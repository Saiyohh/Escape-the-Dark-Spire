namespace DarkSpire
{
    // How the most recent combat ended. Drives the return-to-floor branch
    // (mark monster defeated + apply rewards) and the loss branch
    // (Phase 11 wires Game Over).
    public enum CombatOutcome
    {
        Unknown = 0,
        Victory,
        Flee,
        Wipe,
    }
}
