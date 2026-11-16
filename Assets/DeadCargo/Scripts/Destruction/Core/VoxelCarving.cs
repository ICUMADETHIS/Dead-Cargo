using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>Removes rock from a voxel grid and accounts for every bit removed, by material.</summary>
    public static unsafe class VoxelCarving
    {
        // How far a fully rough crater's wall strays from its radius, as a share of the radius.
        private const float RoughnessAmplitude = 0.35f;

        /// <summary>
        /// Subtracts a sphere from the grid. Positions and sizes are in voxels, in grid coordinates.
        /// </summary>
        /// <param name="roughness">How lumpy the crater is, 0 for a clean sphere, up to 1.</param>
        /// <param name="removed">Rock removed per material slot, in voxel volumes, added to.
        /// <see cref="VoxelEncoding.MaterialSlots"/> long.</param>
        /// <param name="changedMin">The lowest sample that changed.</param>
        /// <param name="changedMax">The highest sample that changed.</param>
        /// <returns>True when any sample changed.</returns>
        public static bool CarveSphere(in VoxelGrid grid, float3 centre, float radius, float roughness, float3 noiseOffset,
            float* removed, out int3 changedMin, out int3 changedMax)
        {
            changedMin = new int3(int.MaxValue);
            changedMax = new int3(int.MinValue);
            if (!(radius > 0f))
                return false;

            roughness = math.saturate(roughness);
            float wobble = roughness * RoughnessAmplitude;
            float outerRadius = radius * (1f + wobble);
            float innerRadius = radius * (1f - wobble);
            float reach = outerRadius + VoxelEncoding.BandVoxels + 1f;
            int3 lo = math.max((int3)math.floor(centre - reach), 0);
            int3 hi = math.min((int3)math.ceil(centre + reach), grid.Size - 1);
            bool changed = false;

            // Three broad waves around the sphere, each along its own axis, make the wall lumpy. Axes and phases come
            // from the offset, so every crater differs; this costs a fraction of noise evaluated per voxel.
            float3 axisA = math.normalizesafe(math.sin(noiseOffset * 1.37f + 0.5f), new float3(1f, 0f, 0f));
            float3 axisB = math.normalizesafe(math.sin(noiseOffset.yzx * 2.11f + 1.3f), new float3(0f, 1f, 0f));
            float3 axisC = math.normalizesafe(math.sin(noiseOffset.zxy * 2.89f + 2.1f), new float3(0f, 0f, 1f));
            float3 phase = math.frac(noiseOffset * 0.618f) * (2f * math.PI);

            for (int z = lo.z; z <= hi.z; ++z)
            {
                for (int y = lo.y; y <= hi.y; ++y)
                {
                    int row = grid.Index(0, y, z);
                    for (int x = lo.x; x <= hi.x; ++x)
                    {
                        int i = row + x;
                        sbyte old = grid.Density[i];
                        if (old == VoxelEncoding.Empty)
                            continue;

                        var p = new float3(x, y, z);
                        float distance = math.distance(p, centre);
                        if (distance - outerRadius >= VoxelEncoding.BandVoxels)
                            continue;

                        // The ragged edge only matters near the crater wall: deep inside, every radius empties the voxel.
                        float r = radius;
                        if (wobble > 0f && distance - innerRadius > -VoxelEncoding.BandVoxels)
                        {
                            float3 direction = (p - centre) / math.max(distance, 1e-4f);
                            float lumps = math.sin(math.dot(direction, axisA) * 3.1f + phase.x) +
                                          math.sin(math.dot(direction, axisB) * 4.3f + phase.y) +
                                          math.sin(math.dot(direction, axisC) * 5.7f + phase.z);
                            r *= 1f + wobble * lumps * (1f / 3f);
                        }

                        // Distance outside the sphere is how solid the rock may stay there.
                        float outside = distance - r;
                        if (outside >= VoxelEncoding.BandVoxels)
                            continue;

                        sbyte limit = VoxelEncoding.Encode(outside);
                        if (limit >= old)
                            continue;

                        int slot = math.min((int)grid.Material[i], VoxelEncoding.MaterialSlots - 1);
                        removed[slot] += VoxelEncoding.Occupancy(old) - VoxelEncoding.Occupancy(limit);
                        grid.Density[i] = limit;

                        var s = new int3(x, y, z);
                        changedMin = math.min(changedMin, s);
                        changedMax = math.max(changedMax, s);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Removes a flat disc of rock: everything closer than <paramref name="halfThickness"/> to the plane through
        /// <paramref name="centre"/>, out to <paramref name="radius"/> from the centre. Positions and sizes in voxels.
        /// </summary>
        /// <param name="normal">The plane's facing, unit length.</param>
        public static bool CarveSlab(in VoxelGrid grid, float3 centre, float3 normal, float halfThickness, float radius,
            float* removed, out int3 changedMin, out int3 changedMax)
        {
            changedMin = new int3(int.MaxValue);
            changedMax = new int3(int.MinValue);
            if (!(halfThickness > 0f) || !(radius > 0f) || !(math.lengthsq(normal) > 0.5f))
                return false;

            float reach = radius + VoxelEncoding.BandVoxels + 1f;
            int3 lo = math.max((int3)math.floor(centre - reach), 0);
            int3 hi = math.min((int3)math.ceil(centre + reach), grid.Size - 1);
            bool changed = false;

            // Walk rows along the axis the plane faces most, visiting only the stretch of each row near the plane:
            // a thin cut across a whole rock touches a sliver of its box, not all of it.
            float3 facing = math.abs(normal);
            int a = facing.x >= facing.y && facing.x >= facing.z ? 0 : facing.y >= facing.z ? 1 : 2;
            int b = (a + 1) % 3;
            int c = (a + 2) % 3;
            float reachAcross = halfThickness + VoxelEncoding.BandVoxels;

            for (int v = lo[c]; v <= hi[c]; ++v)
            {
                for (int u = lo[b]; u <= hi[b]; ++u)
                {
                    float rest = normal[b] * (u - centre[b]) + normal[c] * (v - centre[c]);
                    float t0 = centre[a] + (-reachAcross - rest) / normal[a];
                    float t1 = centre[a] + (reachAcross - rest) / normal[a];
                    int start = math.max(lo[a], (int)math.floor(math.min(t0, t1)));
                    int end = math.min(hi[a], (int)math.ceil(math.max(t0, t1)));

                    for (int t = start; t <= end; ++t)
                    {
                        int3 s = default;
                        s[a] = t;
                        s[b] = u;
                        s[c] = v;
                        float3 offset = (float3)s - centre;
                        float across = math.dot(offset, normal);
                        float lateral = math.length(offset - across * normal);

                        // Outside the disc in either direction: past its faces or beyond its rim.
                        float outside = math.max(math.abs(across) - halfThickness, lateral - radius);
                        if (outside >= VoxelEncoding.BandVoxels)
                            continue;

                        int i = grid.Index(s);
                        sbyte limit = VoxelEncoding.Encode(outside);
                        sbyte old = grid.Density[i];
                        if (limit >= old)
                            continue;

                        int slot = math.min((int)grid.Material[i], VoxelEncoding.MaterialSlots - 1);
                        removed[slot] += VoxelEncoding.Occupancy(old) - VoxelEncoding.Occupancy(limit);
                        grid.Density[i] = limit;
                        changedMin = math.min(changedMin, s);
                        changedMax = math.max(changedMax, s);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>Applies one request of either shape. See <see cref="CarveSphere"/> and <see cref="CarveSlab"/>.</summary>
        public static bool Carve(in VoxelGrid grid, in CarveRequest request, float* removed, out int3 changedMin, out int3 changedMax)
        {
            return request.Shape == CarveShape.Slab
                ? CarveSlab(grid, request.Centre, request.Normal, request.HalfThickness, request.Radius, removed, out changedMin, out changedMax)
                : CarveSphere(grid, request.Centre, request.Radius, request.Roughness, request.NoiseOffset, removed, out changedMin, out changedMax);
        }
    }
}
