using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Lays the frame's drawn rocks out batch by batch: from the per-slot counts of <see cref="FieldCullJob"/>, where
    /// each slot's run of each batch starts in the shared matrix array, and where each batch starts and how long it
    /// is. Rocks past the matrix array's size are dropped.
    /// </summary>
    [BurstCompile]
    public struct FieldOffsetJob : IJob
    {
        [ReadOnly] public NativeArray<int> BatchCounts;
        [WriteOnly] public NativeArray<int> BatchOffsets;

        /// <summary>Per batch: first matrix, and how many.</summary>
        [WriteOnly] public NativeArray<int2> BatchRanges;

        public int Slots;
        public int Batches;
        public int MaxDrawn;

        public void Execute()
        {
            int next = 0;
            for (int b = 0; b < Batches; ++b)
            {
                int first = next;
                for (int s = 0; s < Slots; ++s)
                {
                    int index = s * Batches + b;
                    BatchOffsets[index] = next;
                    next += BatchCounts[index];
                }

                int start = math.min(first, MaxDrawn);
                BatchRanges[b] = new int2(start, math.min(next, MaxDrawn) - start);
            }
        }
    }
}
