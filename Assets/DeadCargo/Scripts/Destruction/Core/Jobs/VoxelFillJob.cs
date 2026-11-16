using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Fills a body's grid from an asteroid shape, one z-slice per index, and sums each slice's rock so the body's
    /// volume is known without a second pass.
    /// </summary>
    [BurstCompile]
    public unsafe struct VoxelFillJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<sbyte> Density;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Material;
        public int3 Size;
        public AsteroidShape Shape;
        public float VoxelSize;
        public float3 Origin;

        /// <summary>Rock in each slice, in voxel volumes.</summary>
        public NativeArray<float> SliceVolume;

        public void Execute(int z)
        {
            var grid = new VoxelGrid((sbyte*)Density.GetUnsafePtr(), (byte*)Material.GetUnsafePtr(), Size);
            VoxelGeneration.Fill(grid, Shape, VoxelSize, Origin, z, z + 1);

            float volume = 0f;
            int start = grid.Index(0, 0, z);
            int end = start + Size.x * Size.y;
            for (int i = start; i < end; ++i)
                volume += VoxelEncoding.Occupancy(grid.Density[i]);

            SliceVolume[z] = volume;
        }
    }
}
