using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels.Diagnostics
{
    /// <summary>Unmanaged memory for the voxel self-test, freed all at once. Every block is zeroed.</summary>
    internal sealed unsafe class VoxelTestMemory : IDisposable
    {
        private readonly List<IntPtr> blocks = new List<IntPtr>();

        public T* Alloc<T>(int count) where T : unmanaged
        {
            long bytes = Math.Max(1L, (long)count * sizeof(T));
            IntPtr block = Marshal.AllocHGlobal(new IntPtr(bytes));
            blocks.Add(block);
            new Span<byte>((void*)block, (int)bytes).Clear();
            return (T*)block;
        }

        public VoxelGrid Grid(int3 size)
        {
            int count = size.x * size.y * size.z;
            return new VoxelGrid(Alloc<sbyte>(count), Alloc<byte>(count), size);
        }

        public MeshBuffers MeshBuffers(int vertexCapacity = SurfaceNets.MaxVertices, int indexCapacity = Voxels.MeshBuffers.DefaultIndexCapacity)
        {
            return new MeshBuffers
            {
                Block = Alloc<sbyte>(SurfaceNets.BlockLength),
                BlockMaterial = Alloc<byte>(SurfaceNets.BlockLength),
                CellVertex = Alloc<int>(SurfaceNets.CellMapLength),
                Vertices = Alloc<VoxelVertex>(vertexCapacity),
                VertexCapacity = vertexCapacity,
                Indices = Alloc<ushort>(indexCapacity),
                IndexCapacity = indexCapacity
            };
        }

        public IslandBuffers IslandBuffers(int voxelCount, int islandCapacity)
        {
            return new IslandBuffers
            {
                Labels = Alloc<ushort>(voxelCount),
                Queue = Alloc<int>(voxelCount),
                Parent = Alloc<int>(Voxels.IslandBuffers.MaxSeeds + 1),
                Size = Alloc<int>(Voxels.IslandBuffers.MaxSeeds + 1),
                Frontier = Alloc<int>(Voxels.IslandBuffers.MaxSeeds + 1),
                Voxels = Alloc<int>(voxelCount),
                Islands = Alloc<IslandInfo>(islandCapacity),
                IslandCapacity = islandCapacity
            };
        }

        public void Dispose()
        {
            foreach (IntPtr block in blocks)
                Marshal.FreeHGlobal(block);

            blocks.Clear();
        }
    }
}
