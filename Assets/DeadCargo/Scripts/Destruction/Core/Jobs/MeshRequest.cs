using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// One chunk to mesh. Points straight at its body's grid and palette, so one batch can mesh chunks from many
    /// bodies. The owner guarantees nothing writes to the grid or palette until the batch completes.
    /// </summary>
    public unsafe struct MeshRequest
    {
        public VoxelGrid Grid;
        public int3 ChunkMin;
        public float VoxelSize;
        public float3 Origin;
        public float4* Palette;
        public int PaletteCount;

        /// <summary>Samples read per cell edge: 1 for full detail, 2 and 4 for the half and quarter detail of distant rock.</summary>
        public int Stride;

        /// <summary>A request for one chunk of a body, built without unsafe code at the call site.</summary>
        public static MeshRequest Create(NativeArray<sbyte> density, NativeArray<byte> material, int3 size, int3 chunkMin,
            float voxelSize, float3 origin, NativeArray<float4> palette, int stride = 1)
        {
            return new MeshRequest
            {
                Grid = new VoxelGrid(
                    (sbyte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(density),
                    (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(material),
                    size),
                ChunkMin = chunkMin,
                VoxelSize = voxelSize,
                Origin = origin,
                Palette = (float4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(palette),
                PaletteCount = palette.Length,
                Stride = stride
            };
        }
    }
}
