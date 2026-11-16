using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Applies a body's queued carves in order. For each one, writes the rock removed per material slot and the box of
    /// samples that changed, so the caller can raise yields and remesh only what moved.
    /// </summary>
    [BurstCompile]
    public unsafe struct VoxelCarveJob : IJob
    {
        public NativeArray<sbyte> Density;
        public NativeArray<byte> Material;
        public int3 Size;

        [ReadOnly] public NativeArray<CarveRequest> Requests;
        public int Count;

        /// <summary><see cref="VoxelEncoding.MaterialSlots"/> per request, in voxel volumes.</summary>
        public NativeArray<float> Removed;

        /// <summary>Two per request: lowest and highest changed sample. Min above max when nothing changed.</summary>
        public NativeArray<int3> Changed;

        public void Execute()
        {
            var grid = new VoxelGrid((sbyte*)Density.GetUnsafePtr(), (byte*)Material.GetUnsafePtr(), Size);
            float* removed = (float*)Removed.GetUnsafePtr();

            for (int r = 0; r < Count; ++r)
            {
                float* slots = removed + r * VoxelEncoding.MaterialSlots;
                for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
                    slots[m] = 0f;

                VoxelCarving.Carve(grid, Requests[r], slots, out int3 min, out int3 max);
                Changed[r * 2] = min;
                Changed[r * 2 + 1] = max;
            }
        }
    }
}
