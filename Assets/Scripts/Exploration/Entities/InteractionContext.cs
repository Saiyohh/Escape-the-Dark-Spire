namespace DarkSpire
{
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
