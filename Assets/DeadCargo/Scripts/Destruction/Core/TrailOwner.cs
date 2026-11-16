using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>An asteroid with a trail, as the trail job sees it this frame.</summary>
    public struct TrailOwner
    {
        /// <summary>World centre of mass.</summary>
        public float3 Centre;

        /// <summary>World velocity, in metres per second.</summary>
        public float3 Velocity;

        /// <summary>Unit direction the trail streams out in: behind the asteroid.</summary>
        public float3 Back;

        /// <summary>Radius of a ball of the same volume, in metres: how wide the trail is.</summary>
        public float Radius;

        /// <summary>False for a free slot or an asteroid that is gone.</summary>
        public bool Active;
    }
}
