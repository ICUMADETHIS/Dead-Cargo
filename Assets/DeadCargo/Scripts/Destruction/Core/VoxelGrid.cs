using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// A dense grid of voxel samples seen through raw pointers, so Burst jobs and plain tests run the same code.
    /// Samples are stored x fastest, then y, then z. The grid does not own its memory.
    /// </summary>
    public unsafe struct VoxelGrid
    {
        public sbyte* Density;
        public byte* Material;
        public int3 Size;

        public VoxelGrid(sbyte* density, byte* material, int3 size)
        {
            Density = density;
            Material = material;
            Size = size;
        }

        public int Count => Size.x * Size.y * Size.z;

        public int Index(int x, int y, int z)
        {
            return x + Size.x * (y + Size.y * z);
        }

        public int Index(int3 p)
        {
            return p.x + Size.x * (p.y + Size.y * p.z);
        }

        public int3 Coord(int index)
        {
            int yz = index / Size.x;
            return new int3(index - yz * Size.x, yz % Size.y, yz / Size.y);
        }

        public bool Contains(int3 p)
        {
            return math.all((p >= 0) & (p < Size));
        }

        /// <summary>The density at a sample, or empty space outside the grid.</summary>
        public sbyte DensityAt(int3 p)
        {
            return Contains(p) ? Density[Index(p)] : VoxelEncoding.Empty;
        }
    }
}
