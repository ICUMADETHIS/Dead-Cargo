using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>One piece of rock that has come away from a body: a run of solid voxel indices and their bounds.</summary>
    public struct IslandInfo
    {
        /// <summary>First entry in the result's voxel list.</summary>
        public int Start;

        /// <summary>Solid voxels in the piece.</summary>
        public int Count;

        public int3 Min;
        public int3 Max;

        /// <summary>True for the catch-all entry that collects pieces beyond the result's capacity.</summary>
        public bool IsOverflow;
    }
}
