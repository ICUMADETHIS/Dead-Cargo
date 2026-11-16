using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>Looks for pieces a cut has separated from one body. See <see cref="VoxelIslands.Find"/>.</summary>
    [BurstCompile]
    public unsafe struct VoxelIslandJob : IJob
    {
        [ReadOnly] public NativeArray<sbyte> Density;
        [ReadOnly] public NativeArray<byte> Material;
        public int3 Size;
        public int3 RegionMin;
        public int3 RegionMax;

        public NativeArray<ushort> Labels;
        public NativeArray<int> Queue;
        public NativeArray<int> Parent;
        public NativeArray<int> UnionSize;
        public NativeArray<int> Frontier;
        public NativeArray<int> Voxels;
        public NativeArray<IslandInfo> Islands;

        /// <summary>One entry: island count, voxels visited, and 1 when the search ran out of labels.</summary>
        public NativeArray<int3> Summary;

        public void Execute()
        {
            var grid = new VoxelGrid((sbyte*)Density.GetUnsafeReadOnlyPtr(), (byte*)Material.GetUnsafeReadOnlyPtr(), Size);
            var buffers = new IslandBuffers
            {
                Labels = (ushort*)Labels.GetUnsafePtr(),
                Queue = (int*)Queue.GetUnsafePtr(),
                Parent = (int*)Parent.GetUnsafePtr(),
                Size = (int*)UnionSize.GetUnsafePtr(),
                Frontier = (int*)Frontier.GetUnsafePtr(),
                Voxels = (int*)Voxels.GetUnsafePtr(),
                Islands = (IslandInfo*)Islands.GetUnsafePtr(),
                IslandCapacity = Islands.Length
            };

            VoxelIslands.Find(grid, RegionMin, RegionMax, ref buffers);
            Summary[0] = new int3(buffers.IslandCount, buffers.Visited, buffers.SeedsTruncated ? 1 : 0);
        }
    }
}
