namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Working memory and output of <see cref="VoxelIslands.Find"/>. The caller owns every buffer. Labels must be all
    /// zero before a search, and the search leaves them zero again, so one set serves every body that fits it.
    /// </summary>
    public unsafe struct IslandBuffers
    {
        /// <summary>Most search fronts, from distinct starting voxels, one search can hold.</summary>
        public const int MaxSeeds = ushort.MaxValue - 1;

        /// <summary>Per-voxel label, at least the grid's voxel count long.</summary>
        public ushort* Labels;

        /// <summary>Visit queue, at least the grid's voxel count long. Holds every voxel labelled, in visit order.</summary>
        public int* Queue;

        /// <summary>Union-find parent, size and open-front count per label, <see cref="MaxSeeds"/> + 1 long each.</summary>
        public int* Parent;
        public int* Size;
        public int* Frontier;

        /// <summary>Island voxel indices, grouped by island. At least the grid's voxel count long.</summary>
        public int* Voxels;

        public IslandInfo* Islands;
        public int IslandCapacity;

        public int IslandCount;

        /// <summary>Voxels the last search visited; its cost.</summary>
        public int Visited;

        /// <summary>Solid voxels in the part that stays with the body, when the search explored it fully. -1 otherwise.</summary>
        public int MainCount;

        /// <summary>True when the region had more separate surfaces than labels, so a piece may have been missed.</summary>
        public bool SeedsTruncated;
    }
}
