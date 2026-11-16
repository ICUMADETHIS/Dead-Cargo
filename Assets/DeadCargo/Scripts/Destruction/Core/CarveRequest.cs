using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>One cut to make in a body, in grid coordinates (voxels): a sphere, or a flat slab like a saw cut.</summary>
    public struct CarveRequest
    {
        public CarveShape Shape;
        public float3 Centre;

        /// <summary>Sphere radius, or the slab's reach from its centre across the cut.</summary>
        public float Radius;

        public float Roughness;
        public float3 NoiseOffset;

        /// <summary>Slab only: the cut's facing, unit length.</summary>
        public float3 Normal;

        /// <summary>Slab only: half the cut's thickness.</summary>
        public float HalfThickness;
    }
}
