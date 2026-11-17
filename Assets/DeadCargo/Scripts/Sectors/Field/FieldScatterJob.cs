using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Writes the matrix of every drawn rock into its batch's place, one cell slot per index. Each slot writes only its
    /// own run of each batch, laid out by <see cref="FieldOffsetJob"/>, so slots never touch the same entry.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    public struct FieldScatterJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FieldRock> Rocks;
        [ReadOnly] public NativeArray<byte> RockBatches;

        /// <summary>Per slot: how many of its rocks, from the first, the culling job gave a batch or culled.</summary>
        [ReadOnly] public NativeArray<int> Examined;

        /// <summary>Each slot's next free place in each batch. Advanced as the slot writes.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<int> BatchOffsets;

        public FieldSettings Settings;
        public int Capacity;
        public int Batches;
        public int MaxDrawn;
        public float Time;
        public float3 OriginOffset;
        public float3 Eye;

        /// <summary>From the camera to where it will be shortly; see <see cref="FieldRockShape.DetailDistance"/>.</summary>
        public float3 Ahead;
        public float PixelsPerRadian;
        public float MinPixels;

        /// <summary>Rocks grow from nothing at the smallest size drawn to their full size at this many times it.</summary>
        public float GrowPixels;

        /// <summary>
        /// Unity's own matrix type: instanced drawing only accepts <see cref="Matrix4x4"/> or a struct with an
        /// objectToWorld field, and rejects <see cref="float4x4"/>.
        /// </summary>
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<Matrix4x4> Matrices;

        public void Execute(int slot)
        {
            int row = slot * Batches;
            int start = slot * Capacity;
            int count = Examined[slot];
            for (int i = 0; i < count; ++i)
            {
                byte batch = RockBatches[start + i];
                if (batch == FieldCullJob.Culled)
                    continue;

                int place = BatchOffsets[row + batch];
                BatchOffsets[row + batch] = place + 1;
                if (place >= MaxDrawn)
                    continue;

                // The smallest rocks grow in from nothing as they pass the smallest size drawn, rather than pop.
                FieldRock rock = Rocks[start + i];
                float pixels = FieldRockShape.Pixels(FieldRockShape.DetailSize(rock, Settings.Stretch),
                    FieldRockShape.DetailDistance(rock.Position, Eye, Ahead), PixelsPerRadian);
                float grow = math.smoothstep(MinPixels, MinPixels * GrowPixels, pixels);
                Matrices[place] = FieldRockShape.Matrix(rock, Settings, Time, OriginOffset, grow);
            }
        }
    }
}
