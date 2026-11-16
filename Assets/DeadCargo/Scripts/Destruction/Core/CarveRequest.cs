using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// One cut to make in a body, in grid coordinates (voxels): a sphere, a flat slab like a saw cut, or a crack that
    /// breaks a share of the rock away.
    /// </summary>
    public struct CarveRequest
    {
        public CarveShape Shape;

        /// <summary>Sphere and slab: the cut's centre. Worked out for a fracture.</summary>
        public float3 Centre;

        /// <summary>Sphere radius, or the slab's reach from its centre across the cut.</summary>
        public float Radius;

        /// <summary>How ragged the cut is, 0 to 1: crater walls for a sphere, crack faces for a fracture.</summary>
        public float Roughness;

        public float3 NoiseOffset;

        /// <summary>Slab and fracture: the cut's facing, unit length. A fracture breaks off the part it faces.</summary>
        public float3 Normal;

        /// <summary>Slab and fracture: half the cut's thickness.</summary>
        public float HalfThickness;

        /// <summary>Slab only: height of the bumps on a ragged face. 0 for a flat cut.</summary>
        public float Relief;

        /// <summary>Fracture only: share of the rock that breaks away, 0 to 1.</summary>
        public float Share;
    }
}
