namespace DarkSpire
{
    // Lightweight bundle passed to interaction handlers so they can reach
    // the party token and the registry without each being a singleton.
    public readonly struct InteractionContext
    {
        public readonly PartyToken party;
        public readonly DungeonRegistry registry;

        public InteractionContext(PartyToken party, DungeonRegistry registry)
        {
            this.party = party;
            this.registry = registry;
        }
    }
}
