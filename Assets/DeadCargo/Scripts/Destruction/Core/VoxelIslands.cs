using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Finds pieces of rock that a cut has separated from a body, and moves them into grids of their own.
    /// <para>
    /// The search starts from every solid voxel near the cut and grows all fronts at once, one layer at a time,
    /// merging fronts that meet. A front that runs out of voxels to visit is a whole, closed piece. The search stops
    /// as soon as only one front is still growing: that is the body, and everything closed is a separate piece. So
    /// a crater that splits nothing costs a few layers, and a split costs about twice the smaller piece, never the
    /// whole body.
    /// </para>
    /// </summary>
    public static unsafe class VoxelIslands
    {
        /// <summary>Finds every piece separated from the body around the given region, in grid coordinates.</summary>
        public static void Find(in VoxelGrid grid, int3 regionMin, int3 regionMax, ref IslandBuffers buffers)
        {
            buffers.IslandCount = 0;
            buffers.Visited = 0;
            buffers.MainCount = -1;

            regionMin = math.max(regionMin, 0);
            regionMax = math.min(regionMax, grid.Size - 1);

            int labels = 0;
            int open = 0;
            int tail = 0;
            bool full = false;

            // Every piece the cut touched has rock facing the cut, so only surface voxels start fronts. Seeds that touch
            // a seed scanned before them join its front at once, so a cut face becomes a handful of fronts, not
            // thousands.
            for (int z = regionMin.z; z <= regionMax.z && !full; ++z)
            {
                for (int y = regionMin.y; y <= regionMax.y && !full; ++y)
                {
                    for (int x = regionMin.x; x <= regionMax.x; ++x)
                    {
                        int i = grid.Index(x, y, z);
                        if (grid.Density[i] <= 0 || buffers.Labels[i] != 0)
                            continue;

                        var p = new int3(x, y, z);
                        if (!HasEmptyNeighbour(grid, p))
                            continue;

                        int root = 0;
                        root = JoinSeed(ref buffers, root, x > regionMin.x ? buffers.Labels[i - 1] : 0, ref open);
                        root = JoinSeed(ref buffers, root, y > regionMin.y ? buffers.Labels[i - grid.Size.x] : 0, ref open);
                        root = JoinSeed(ref buffers, root, z > regionMin.z ? buffers.Labels[i - grid.Size.x * grid.Size.y] : 0, ref open);

                        if (root == 0)
                        {
                            if (labels == IslandBuffers.MaxSeeds)
                            {
                                full = true;
                                break;
                            }

                            root = ++labels;
                            buffers.Parent[root] = root;
                            buffers.Size[root] = 0;
                            buffers.Frontier[root] = 0;
                            ++open;
                        }

                        buffers.Labels[i] = (ushort)root;
                        ++buffers.Size[root];
                        ++buffers.Frontier[root];
                        buffers.Queue[tail++] = i;
                    }
                }
            }

            buffers.SeedsTruncated = full;

            int head = 0;
            while (head < tail && open > 1)
            {
                int v = buffers.Queue[head++];
                int root = FindRoot(buffers.Parent, buffers.Labels[v]);
                --buffers.Frontier[root];
                int3 c = grid.Coord(v);

                for (int n = 0; n < 6; ++n)
                {
                    int3 p = c + Neighbour(n);
                    if (!grid.Contains(p))
                        continue;

                    int u = grid.Index(p);
                    if (grid.Density[u] <= 0)
                        continue;

                    int label = buffers.Labels[u];
                    if (label == 0)
                    {
                        buffers.Labels[u] = (ushort)root;
                        buffers.Queue[tail++] = u;
                        ++buffers.Size[root];
                        ++buffers.Frontier[root];
                    }
                    else
                    {
                        int other = FindRoot(buffers.Parent, label);
                        if (other != root)
                        {
                            root = Union(ref buffers, root, other);
                            --open;
                        }
                    }
                }

                if (buffers.Frontier[root] == 0)
                    --open;
            }

            buffers.Visited = tail;
            if (labels > 0)
                CollectIslands(grid, labels, head, tail, open, ref buffers);

            for (int q = 0; q < tail; ++q)
                buffers.Labels[buffers.Queue[q]] = 0;
        }

        /// <summary>
        /// Moves one piece into the target grid and removes it from the source. The piece keeps the band of distances
        /// around it, so its surface is unchanged; the band it leaves behind in the source is cleared.
        /// </summary>
        /// <param name="voxels">The piece's solid voxels, as source indices.</param>
        /// <param name="sourceMin">The source sample that lands at <paramref name="targetOffset"/> in the target.</param>
        public static void Extract(in VoxelGrid source, int* voxels, int count, int3 sourceMin, in VoxelGrid target, int3 targetOffset)
        {
            int3 shift = targetOffset - sourceMin;
            int targetCount = target.Count;
            for (int i = 0; i < targetCount; ++i)
            {
                target.Density[i] = VoxelEncoding.Empty;
                target.Material[i] = 0;
            }

            for (int k = 0; k < count; ++k)
            {
                int v = voxels[k];
                int t = target.Index(source.Coord(v) + shift);
                target.Density[t] = source.Density[v];
                target.Material[t] = source.Material[v];
            }

            // The empty side of the piece's surface comes too, so the surface lands exactly where it was.
            for (int k = 0; k < count; ++k)
            {
                int3 s = source.Coord(voxels[k]);
                for (int n = 0; n < 6; ++n)
                {
                    int3 p = s + Neighbour(n);
                    int3 t = p + shift;
                    if (!source.Contains(p) || !target.Contains(t))
                        continue;

                    int u = source.Index(p);
                    if (source.Density[u] > 0)
                        continue;

                    int ti = target.Index(t);
                    target.Density[ti] = source.Density[u];
                    target.Material[ti] = source.Material[u];
                }
            }

            for (int k = 0; k < count; ++k)
                source.Density[voxels[k]] = VoxelEncoding.Empty;

            for (int k = 0; k < count; ++k)
            {
                int3 s = source.Coord(voxels[k]);
                for (int n = 0; n < 6; ++n)
                {
                    int3 p = s + Neighbour(n);
                    if (!source.Contains(p))
                        continue;

                    int u = source.Index(p);
                    sbyte d = source.Density[u];
                    if (d > 0 || d == VoxelEncoding.Empty)
                        continue;

                    if (!HasSolidNeighbour(source, p))
                        source.Density[u] = VoxelEncoding.Empty;
                }
            }
        }

        /// <summary>
        /// Removes a piece too small to keep as a body of its own, and the band around it. Adds the rock removed per
        /// material slot, in voxel volumes, to <paramref name="removed"/>.
        /// </summary>
        public static void Erase(in VoxelGrid grid, int* voxels, int count, float* removed)
        {
            for (int k = 0; k < count; ++k)
            {
                int v = voxels[k];
                int slot = math.min((int)grid.Material[v], VoxelEncoding.MaterialSlots - 1);
                removed[slot] += VoxelEncoding.Occupancy(grid.Density[v]);
                grid.Density[v] = VoxelEncoding.Empty;
            }

            for (int k = 0; k < count; ++k)
            {
                int3 s = grid.Coord(voxels[k]);
                for (int n = 0; n < 6; ++n)
                {
                    int3 p = s + Neighbour(n);
                    if (!grid.Contains(p))
                        continue;

                    int u = grid.Index(p);
                    sbyte d = grid.Density[u];
                    if (d > 0 || d == VoxelEncoding.Empty || HasSolidNeighbour(grid, p))
                        continue;

                    int slot = math.min((int)grid.Material[u], VoxelEncoding.MaterialSlots - 1);
                    removed[slot] += VoxelEncoding.Occupancy(d);
                    grid.Density[u] = VoxelEncoding.Empty;
                }
            }
        }

        /// <summary>
        /// Rock volume, centre of mass and volume per material of a whole grid. Volumes are in voxel volumes and the
        /// centre in grid coordinates.
        /// </summary>
        /// <param name="materialVolume"><see cref="VoxelEncoding.MaterialSlots"/> long; overwritten.</param>
        public static void Measure(in VoxelGrid grid, float* materialVolume, out float volume, out float3 centre, out int solidCount)
        {
            for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
                materialVolume[m] = 0f;

            // Sums run in double: a large rock adds up millions of small values, and mining totals must not drift.
            double total = 0.0;
            double3 moment = double3.zero;
            double* materials = stackalloc double[VoxelEncoding.MaterialSlots];
            for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
                materials[m] = 0.0;

            solidCount = 0;
            for (int z = 0; z < grid.Size.z; ++z)
            {
                for (int y = 0; y < grid.Size.y; ++y)
                {
                    int row = grid.Index(0, y, z);
                    for (int x = 0; x < grid.Size.x; ++x)
                    {
                        sbyte d = grid.Density[row + x];
                        if (d == VoxelEncoding.Empty)
                            continue;

                        float occupancy = VoxelEncoding.Occupancy(d);
                        if (occupancy <= 0f)
                            continue;

                        total += occupancy;
                        moment += occupancy * new double3(x, y, z);
                        materials[math.min((int)grid.Material[row + x], VoxelEncoding.MaterialSlots - 1)] += occupancy;
                        if (d > 0)
                            ++solidCount;
                    }
                }
            }

            for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
                materialVolume[m] = (float)materials[m];

            volume = (float)total;
            centre = total > 0.0 ? (float3)(moment / total) : (float3)grid.Size * 0.5f;
        }

        private static void CollectIslands(in VoxelGrid grid, int labels, int head, int tail, int open, ref IslandBuffers buffers)
        {
            int main = 0;
            if (open == 1 && head < tail)
            {
                main = FindRoot(buffers.Parent, buffers.Labels[buffers.Queue[head]]);
            }
            else
            {
                int best = -1;
                for (int l = 1; l <= labels; ++l)
                {
                    if (buffers.Parent[l] == l && buffers.Size[l] > best)
                    {
                        best = buffers.Size[l];
                        main = l;
                    }
                }

                buffers.MainCount = best;
            }

            // The front counts are spent; reuse them to map each closed root to its island slot.
            for (int l = 1; l <= labels; ++l)
            {
                if (buffers.Parent[l] != l || l == main)
                {
                    buffers.Frontier[l] = -1;
                    continue;
                }

                int slot;
                if (buffers.IslandCount < buffers.IslandCapacity)
                {
                    slot = buffers.IslandCount++;
                    buffers.Islands[slot] = new IslandInfo { Min = new int3(int.MaxValue), Max = new int3(int.MinValue) };
                }
                else
                {
                    slot = buffers.IslandCapacity - 1;
                    buffers.Islands[slot].IsOverflow = true;
                }

                buffers.Frontier[l] = slot;
            }

            if (buffers.IslandCount == 0)
                return;

            for (int q = 0; q < tail; ++q)
            {
                int v = buffers.Queue[q];
                int slot = buffers.Frontier[FindRoot(buffers.Parent, buffers.Labels[v])];
                if (slot < 0)
                    continue;

                int3 c = grid.Coord(v);
                IslandInfo* island = buffers.Islands + slot;
                ++island->Count;
                island->Min = math.min(island->Min, c);
                island->Max = math.max(island->Max, c);
            }

            int start = 0;
            for (int s = 0; s < buffers.IslandCount; ++s)
            {
                buffers.Islands[s].Start = start;
                start += buffers.Islands[s].Count;
                buffers.Islands[s].Count = 0;
            }

            for (int q = 0; q < tail; ++q)
            {
                int v = buffers.Queue[q];
                int slot = buffers.Frontier[FindRoot(buffers.Parent, buffers.Labels[v])];
                if (slot < 0)
                    continue;

                IslandInfo* island = buffers.Islands + slot;
                buffers.Voxels[island->Start + island->Count++] = v;
            }
        }

        // Joins a seed to the front of an already-labelled neighbour, merging fronts when it touches two.
        private static int JoinSeed(ref IslandBuffers buffers, int root, int neighbourLabel, ref int open)
        {
            if (neighbourLabel == 0)
                return root;

            int other = FindRoot(buffers.Parent, neighbourLabel);
            if (root == 0)
                return other;
            if (other == root)
                return root;

            --open;
            return Union(ref buffers, root, other);
        }

        private static bool HasEmptyNeighbour(in VoxelGrid grid, int3 p)
        {
            for (int n = 0; n < 6; ++n)
            {
                if (grid.DensityAt(p + Neighbour(n)) <= 0)
                    return true;
            }

            return false;
        }

        private static bool HasSolidNeighbour(in VoxelGrid grid, int3 p)
        {
            for (int n = 0; n < 6; ++n)
            {
                if (grid.DensityAt(p + Neighbour(n)) > 0)
                    return true;
            }

            return false;
        }

        private static int3 Neighbour(int n)
        {
            switch (n)
            {
                case 0: return new int3(1, 0, 0);
                case 1: return new int3(-1, 0, 0);
                case 2: return new int3(0, 1, 0);
                case 3: return new int3(0, -1, 0);
                case 4: return new int3(0, 0, 1);
                default: return new int3(0, 0, -1);
            }
        }

        private static int FindRoot(int* parent, int label)
        {
            while (parent[label] != label)
            {
                parent[label] = parent[parent[label]];
                label = parent[label];
            }

            return label;
        }

        private static int Union(ref IslandBuffers buffers, int a, int b)
        {
            if (buffers.Size[a] < buffers.Size[b])
            {
                int swap = a;
                a = b;
                b = swap;
            }

            buffers.Parent[b] = a;
            buffers.Size[a] += buffers.Size[b];
            buffers.Frontier[a] += buffers.Frontier[b];
            return a;
        }
    }
}
