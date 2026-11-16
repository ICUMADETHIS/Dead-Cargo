using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>A cut waiting for the scheduler: the request in grid space, plus what effects and yields need.</summary>
    internal struct CarveCommand
    {
        public CarveRequest Request;
        public DamageKind Kind;

        /// <summary>World centre of the cut.</summary>
        public Vector3 Point;

        /// <summary>World direction chips fly out along.</summary>
        public Vector3 Normal;

        /// <summary>Radius of the cut, in metres.</summary>
        public float Radius;
    }
}
