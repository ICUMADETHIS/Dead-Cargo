using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>One small rock in an asteroid's trail: where it is, how it moves and turns, how big it is and how old.</summary>
    public struct TrailRock
    {
        public float3 Position;

        /// <summary>World velocity, in metres per second.</summary>
        public float3 Velocity;

        public quaternion Rotation;

        /// <summary>World angular velocity, in radians per second.</summary>
        public float3 Spin;

        /// <summary>Size along each axis, in metres. Uneven, so no two rocks look alike.</summary>
        public float3 Scale;

        /// <summary>Seconds since it left its asteroid.</summary>
        public float Age;

        /// <summary>Seconds it lives: long enough to slide to the end of the trail.</summary>
        public float Lifetime;

        /// <summary>Which asteroid type it came from, for its density and chip colour.</summary>
        public int Kind;

        /// <summary>
        /// The trail it belongs to (a slot in the owner table), or -1 once it is knocked loose or its asteroid is gone:
        /// then it flies free.
        /// </summary>
        public int Owner;

        /// <summary>Speed it slides back along the trail, relative to its asteroid, in metres per second.</summary>
        public float BackSpeed;

        /// <summary>Its own small drift away from the trail's line, in metres per second, so the trail has width.</summary>
        public float3 Spread;

        /// <summary>The physics stand-in moving it while it is near the camera, or -1 while the job moves it.</summary>
        public int Proxy;

        public bool IsDead => Age >= Lifetime;
    }
}
