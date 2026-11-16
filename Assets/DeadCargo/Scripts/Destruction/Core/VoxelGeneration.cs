using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>Fills a voxel grid from an asteroid shape. Slices are independent, so a job can fill them in parallel.</summary>
    public static unsafe class VoxelGeneration
    {
        /// <summary>
        /// Fills the z-slices from <paramref name="zStart"/> up to <paramref name="zEnd"/>. The grid's outer layer is
        /// always left empty, so every surface is closed.
        /// </summary>
        /// <param name="voxelSize">Spacing of the samples, in metres.</param>
        /// <param name="origin">Body-local position of sample (0, 0, 0), in metres.</param>
        public static void Fill(in VoxelGrid grid, in AsteroidShape shape, float voxelSize, float3 origin, int zStart, int zEnd)
        {
            float band = VoxelEncoding.BandVoxels * voxelSize;
            float outer = shape.MaxRadius + band;
            float inner = shape.MinRadius - band;
            float outerSq = outer * outer;
            float innerSq = inner > 0f ? inner * inner : -1f;
            float invVoxel = 1f / voxelSize;
            int3 last = grid.Size - 1;

            for (int z = zStart; z < zEnd; ++z)
            {
                for (int y = 0; y < grid.Size.y; ++y)
                {
                    for (int x = 0; x < grid.Size.x; ++x)
                    {
                        int i = grid.Index(x, y, z);
                        float3 p = origin + new float3(x, y, z) * voxelSize;
                        float lengthSq = math.lengthsq(p);
                        bool border = x == 0 || y == 0 || z == 0 || x == last.x || y == last.y || z == last.z;

                        if (border || lengthSq > outerSq)
                        {
                            grid.Density[i] = VoxelEncoding.Empty;
                            grid.Material[i] = 0;
                            continue;
                        }

                        // The core is rock whatever the noise does, so only its ore needs working out.
                        if (lengthSq < innerSq)
                        {
                            grid.Density[i] = VoxelEncoding.Full;
                            grid.Material[i] = shape.MaterialAt(p, shape.MinRadius - math.sqrt(lengthSq));
                            continue;
                        }

                        float solid = shape.SolidDistance(p);
                        sbyte density = VoxelEncoding.Encode(solid * invVoxel);
                        grid.Density[i] = density;
                        grid.Material[i] = density > VoxelEncoding.Empty ? shape.MaterialAt(p, solid) : (byte)0;
                    }
                }
            }
        }
    }
}
