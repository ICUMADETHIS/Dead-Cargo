using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Working memory and output of <see cref="SurfaceNets.MeshChunk"/>. The caller owns every buffer: the block and cell
    /// map are scratch, the vertex and index buffers receive the mesh. Nothing is written past a capacity; when the
    /// mesh does not fit, <see cref="Overflow"/> is set and the mesh is cut short.
    /// </summary>
    public unsafe struct MeshBuffers
    {
        /// <summary>Indices one chunk may produce by default: far more than any rock surface needs.</summary>
        public const int DefaultIndexCapacity = 6 * 24576;

        /// <summary>Densities around the chunk, <see cref="SurfaceNets.BlockLength"/> long.</summary>
        public sbyte* Block;

        /// <summary>Materials around the chunk, <see cref="SurfaceNets.BlockLength"/> long.</summary>
        public byte* BlockMaterial;

        /// <summary>The vertex of each cell, or -1, <see cref="SurfaceNets.CellMapLength"/> long.</summary>
        public int* CellVertex;

        public VoxelVertex* Vertices;
        public int VertexCapacity;
        public ushort* Indices;
        public int IndexCapacity;

        public int VertexCount;
        public int IndexCount;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public bool Overflow;

        public bool IsEmpty => IndexCount == 0;

        public void Reset()
        {
            VertexCount = 0;
            IndexCount = 0;
            Overflow = false;
            BoundsMin = new float3(float.MaxValue);
            BoundsMax = new float3(float.MinValue);
        }
    }
}
