using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>Works out where a rock cracks so that a chosen share of it breaks away.</summary>
    public static unsafe class VoxelFracture
    {
        private const int Bins = 256;

        // Samples read to place one crack. A crack only needs to be about right, so large rocks are read sparsely.
        private const int SampleBudget = 200000;

        /// <summary>
        /// Finds the crack plane facing <paramref name="normal"/> with <paramref name="share"/> of the rock beyond it.
        /// Positions and sizes are in grid coordinates (voxels).
        /// </summary>
        /// <param name="normal">Unit direction from the rest of the rock towards the part that breaks away.</param>
        /// <param name="share">Share of the rock that breaks away, 0 to 1.</param>
        /// <param name="point">A point on the crack, level with the rock's centre.</param>
        /// <param name="size">The rock's size: the cube root of its volume.</param>
        /// <returns>False when the grid holds no rock.</returns>
        public static bool Locate(in VoxelGrid grid, float3 normal, float share, out float3 point, out float size)
        {
            point = float3.zero;
            size = 0f;

            int stride = math.max(1, (int)math.ceil(math.pow(grid.Count / (float)SampleBudget, 1f / 3f)));
            float3 extent = grid.Size - 1;
            float low = math.csum(math.min(normal * extent, 0f));
            float high = math.csum(math.max(normal * extent, 0f));
            float binsPerVoxel = Bins / math.max(high - low, 1e-3f);

            float* bins = stackalloc float[Bins];
            for (int b = 0; b < Bins; ++b)
                bins[b] = 0f;

            double total = 0.0;
            double3 moment = double3.zero;
            for (int z = 0; z < grid.Size.z; z += stride)
            {
                for (int y = 0; y < grid.Size.y; y += stride)
                {
                    int row = grid.Index(0, y, z);
                    for (int x = 0; x < grid.Size.x; x += stride)
                    {
                        sbyte d = grid.Density[row + x];
                        if (d == VoxelEncoding.Empty)
                            continue;

                        float occupancy = VoxelEncoding.Occupancy(d);
                        if (occupancy <= 0f)
                            continue;

                        var p = new float3(x, y, z);
                        int bin = math.clamp((int)((math.dot(p, normal) - low) * binsPerVoxel), 0, Bins - 1);
                        bins[bin] += occupancy;
                        total += occupancy;
                        moment += occupancy * new double3(x, y, z);
                    }
                }
            }

            if (!(total > 0.0))
                return false;

            // Walk in from the far side until the share is reached, then place the crack inside that bin.
            float target = (float)(total * math.saturate(share));
            float sum = 0f;
            float cut = low;
            for (int b = Bins - 1; b >= 0; --b)
            {
                if (bins[b] > 0f && sum + bins[b] >= target)
                {
                    float within = (target - sum) / bins[b];
                    cut = low + (b + 1 - within) / binsPerVoxel;
                    break;
                }

                sum += bins[b];
            }

            float3 centre = (float3)(moment / total);
            point = centre + (cut - math.dot(centre, normal)) * normal;
            size = math.pow((float)total * stride * stride * stride, 1f / 3f);
            return true;
        }
    }
}
