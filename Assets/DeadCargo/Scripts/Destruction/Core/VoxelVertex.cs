using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// The vertex layout of voxel meshes, 20 bytes: position, a normal packed as four signed bytes, and a colour as four
    /// bytes. The colour carries the ore tint in rgb and its strength in alpha.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelVertex
    {
        public float3 Position;
        public uint Normal;
        public uint Color;

        public static uint PackNormal(float3 normal)
        {
            int3 n = (int3)math.round(math.clamp(normal, -1f, 1f) * 127f);
            return (uint)(n.x & 0xFF) | (uint)(n.y & 0xFF) << 8 | (uint)(n.z & 0xFF) << 16;
        }

        public static float3 UnpackNormal(uint packed)
        {
            return new float3((sbyte)(packed & 0xFF), (sbyte)((packed >> 8) & 0xFF), (sbyte)((packed >> 16) & 0xFF)) / 127f;
        }

        public static uint PackColor(float4 color)
        {
            uint4 c = (uint4)math.round(math.saturate(color) * 255f);
            return c.x | c.y << 8 | c.z << 16 | c.w << 24;
        }

        public static float4 UnpackColor(uint packed)
        {
            return new float4(packed & 0xFF, (packed >> 8) & 0xFF, (packed >> 16) & 0xFF, packed >> 24) / 255f;
        }
    }
}
