using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>What meshing one chunk produced.</summary>
    public struct MeshResult
    {
        public int VertexCount;
        public int IndexCount;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public bool Overflow;
    }
}
