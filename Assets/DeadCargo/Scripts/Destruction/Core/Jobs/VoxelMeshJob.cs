using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Meshes a batch of chunks in parallel and writes each straight into its mesh data, so applying the result on
    /// the main thread is a single call with no copying. Each index has its own slice of the scratch buffers.
    /// </summary>
    [BurstCompile]
    public unsafe struct VoxelMeshJob : IJobParallelFor
    {
        /// <summary>Flags that let the job's own bounds and indices stand without Unity checking them again.</summary>
        public const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        [ReadOnly] public NativeArray<MeshRequest> Requests;
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> Layout;

        [NativeDisableParallelForRestriction] public NativeArray<sbyte> Blocks;
        [NativeDisableParallelForRestriction] public NativeArray<byte> BlockMaterials;
        [NativeDisableParallelForRestriction] public NativeArray<int> CellVertices;
        [NativeDisableParallelForRestriction] public NativeArray<VoxelVertex> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<ushort> Indices;

        /// <summary>Vertices and indices each slot may hold; a mesh that needs more is cut short and reported.</summary>
        public int VertexCapacity;
        public int IndexCapacity;

        public NativeArray<MeshResult> Results;
        public Mesh.MeshDataArray Output;

        /// <summary>The vertex layout matching <see cref="VoxelVertex"/>.</summary>
        public static NativeArray<VertexAttributeDescriptor> CreateLayout(Allocator allocator)
        {
            var layout = new NativeArray<VertexAttributeDescriptor>(3, allocator);
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4);
            layout[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
            return layout;
        }

        public void Execute(int i)
        {
            var buffers = new MeshBuffers
            {
                Block = (sbyte*)Blocks.GetUnsafePtr() + (long)i * SurfaceNets.BlockLength,
                BlockMaterial = (byte*)BlockMaterials.GetUnsafePtr() + (long)i * SurfaceNets.BlockLength,
                CellVertex = (int*)CellVertices.GetUnsafePtr() + (long)i * SurfaceNets.CellMapLength,
                Vertices = (VoxelVertex*)Vertices.GetUnsafePtr() + (long)i * VertexCapacity,
                VertexCapacity = VertexCapacity,
                Indices = (ushort*)Indices.GetUnsafePtr() + (long)i * IndexCapacity,
                IndexCapacity = IndexCapacity
            };

            MeshRequest request = Requests[i];
            SurfaceNets.MeshChunk(request.Grid, request.ChunkMin, request.VoxelSize, request.Origin, request.Palette,
                request.PaletteCount, request.Stride, ref buffers);

            Mesh.MeshData data = Output[i];
            data.SetVertexBufferParams(buffers.VertexCount, Layout);
            data.SetIndexBufferParams(buffers.IndexCount, IndexFormat.UInt16);
            if (buffers.VertexCount > 0)
                UnsafeUtility.MemCpy(data.GetVertexData<VoxelVertex>().GetUnsafePtr(), buffers.Vertices, (long)buffers.VertexCount * sizeof(VoxelVertex));
            if (buffers.IndexCount > 0)
                UnsafeUtility.MemCpy(data.GetIndexData<ushort>().GetUnsafePtr(), buffers.Indices, (long)buffers.IndexCount * sizeof(ushort));

            float3 min = buffers.IsEmpty ? float3.zero : buffers.BoundsMin;
            float3 max = buffers.IsEmpty ? float3.zero : buffers.BoundsMax;
            var bounds = new Bounds((min + max) * 0.5f, max - min);
            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor(0, buffers.IndexCount)
            {
                bounds = bounds,
                firstVertex = 0,
                vertexCount = buffers.VertexCount
            }, UpdateFlags);

            Results[i] = new MeshResult
            {
                VertexCount = buffers.VertexCount,
                IndexCount = buffers.IndexCount,
                BoundsMin = min,
                BoundsMax = max,
                Overflow = buffers.Overflow
            };
        }
    }
}
