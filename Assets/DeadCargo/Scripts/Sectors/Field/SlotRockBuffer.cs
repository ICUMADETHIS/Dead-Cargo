using Unity.Collections;

namespace DeadCargo.Sectors.Field
{
    /// <summary>A run of a native rock array that one job index fills: one cell's slot, or a cell's scratch.</summary>
    public struct SlotRockBuffer : IRockBuffer
    {
        private NativeArray<FieldRock> rocks;
        private readonly int start;
        private readonly int capacity;
        private int count;

        public SlotRockBuffer(NativeArray<FieldRock> rocks, int start, int capacity)
        {
            this.rocks = rocks;
            this.start = start;
            this.capacity = capacity;
            count = 0;
        }

        public int Count => count;

        public FieldRock Get(int index)
        {
            return rocks[start + index];
        }

        public bool Add(in FieldRock rock)
        {
            if (count >= capacity)
                return false;

            rocks[start + count] = rock;
            ++count;
            return true;
        }
    }
}
