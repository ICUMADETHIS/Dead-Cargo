using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Where one separated piece goes: into a new body's grid, or nowhere, when it is too small to keep and crumbles.
    /// Points straight at the new grid; the owner guarantees nothing else touches it until the job completes.
    /// </summary>
    public unsafe struct ExtractTarget
    {
        public VoxelGrid Grid;

        /// <summary>Where the piece's lowest sample lands in the new grid.</summary>
        public int3 Offset;

        public bool Keep;

        /// <summary>A piece moved into the given grid.</summary>
        public static ExtractTarget Into(NativeArray<sbyte> density, NativeArray<byte> material, int3 size, int3 offset)
        {
            return new ExtractTarget
            {
                Grid = new VoxelGrid(
                    (sbyte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(density),
                    (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(material),
                    size),
                Offset = offset,
                Keep = true
            };
        }

        /// <summary>A piece that crumbles: erased from its body.</summary>
        public static ExtractTarget Crumble => default;
    }
}
