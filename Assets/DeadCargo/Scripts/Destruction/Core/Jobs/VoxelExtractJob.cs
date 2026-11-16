using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Moves every piece one search found out of its body, in order, off the main thread. Pieces worth keeping go into
    /// grids of their own and are measured, so they can fly as bodies; small ones are erased and reported as removed.
    /// </summary>
    [BurstCompile]
    public unsafe struct VoxelExtractJob : IJob
    {
        public NativeArray<sbyte> SourceDensity;
        public NativeArray<byte> SourceMaterial;
        public int3 SourceSize;

        [ReadOnly] public NativeArray<int> Voxels;
        [ReadOnly] public NativeArray<IslandInfo> Islands;
        [ReadOnly] public NativeArray<ExtractTarget> Targets;
        public int Count;

        /// <summary><see cref="VoxelEncoding.MaterialSlots"/> volumes per piece: contained when kept, removed when erased.</summary>
        public NativeArray<float> MaterialVolume;

        /// <summary>Per piece: the rock it took from the body, as mass moments in the source grid's coordinates.</summary>
        public NativeArray<MassMoments> Moments;

        public void Execute()
        {
            var source = new VoxelGrid((sbyte*)SourceDensity.GetUnsafePtr(), (byte*)SourceMaterial.GetUnsafePtr(), SourceSize);
            int* voxels = (int*)Voxels.GetUnsafeReadOnlyPtr();
            float* volumes = (float*)MaterialVolume.GetUnsafePtr();

            for (int i = 0; i < Count; ++i)
            {
                IslandInfo island = Islands[i];
                ExtractTarget target = Targets[i];
                float* slots = volumes + i * VoxelEncoding.MaterialSlots;

                if (!target.Keep)
                {
                    for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
                        slots[m] = 0f;

                    MassMoments erased = default;
                    VoxelIslands.Erase(source, voxels + island.Start, island.Count, slots, ref erased);
                    Moments[i] = erased;
                    continue;
                }

                VoxelIslands.Extract(source, voxels + island.Start, island.Count, island.Min, target.Grid, target.Offset);
                VoxelIslands.Measure(target.Grid, slots, out MassMoments moved, out _);
                Moments[i] = moved.Transformed(1.0, island.Min - target.Offset);
            }
        }
    }
}
