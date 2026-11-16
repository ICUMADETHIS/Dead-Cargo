using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Fills a body's grid from an asteroid shape, one z-slice per index, and measures each slice's rock so the body's
    /// volume, centre of mass and inertia are known without a second pass.
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

        /// <summary>Rock in each slice, as mass moments in grid coordinates.</summary>
        public NativeArray<MassMoments> SliceMoments;

        public void Execute(int z)
        {
            var grid = new VoxelGrid((sbyte*)Density.GetUnsafePtr(), (byte*)Material.GetUnsafePtr(), Size);
            VoxelGeneration.Fill(grid, Shape, VoxelSize, Origin, z, z + 1);

            MassMoments moments = default;
            for (int y = 0; y < Size.y; ++y)
            {
                int row = grid.Index(0, y, z);
                double volume = 0.0;
                double sumX = 0.0;
                double sumXX = 0.0;
                for (int x = 0; x < Size.x; ++x)
                {
                    float occupancy = VoxelEncoding.Occupancy(grid.Density[row + x]);
                    if (occupancy <= 0f)
                        continue;

                    volume += occupancy;
                    sumX += (double)occupancy * x;
                    sumXX += (double)occupancy * x * x;
                }

                if (volume > 0.0)
                    moments.AddRow(volume, sumX, sumXX, y, z);
            }

            SliceMoments[z] = moments;
        }
    }
}
