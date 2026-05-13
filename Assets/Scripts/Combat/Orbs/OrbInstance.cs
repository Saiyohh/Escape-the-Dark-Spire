namespace DarkSpire
{
    public class OrbInstance
    {
        public readonly OrbDataSO data;

        public int stacks;

        public OrbInstance(OrbDataSO data)
        {
            this.data = data;
            this.stacks = data != null ? data.startingStacks : 0;
        }

        public OrbType Type => data != null ? data.orbType : OrbType.Lightning;
    }
}
