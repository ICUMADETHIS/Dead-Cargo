using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>One body of a run's backdrop: which planet, where in the sky, how large, and how it turns.</summary>
    public readonly struct BackdropPlan
    {
        public BackdropPlan(BodyRoles role, int entry, Vector3 direction, float size, Vector3 spinAxis, float spinPhase,
            float spinSpeed, bool metRule)
        {
            Role = role;
            Entry = entry;
            Direction = direction;
            Size = size;
            SpinAxis = spinAxis;
            SpinPhase = spinPhase;
            SpinSpeed = spinSpeed;
            MetRule = metRule;
        }

        /// <summary>The part it plays: primary, moon or distant planet.</summary>
        public BodyRoles Role { get; }

        /// <summary>Index of its planet in the catalogue's entries.</summary>
        public int Entry { get; }

        /// <summary>Unit direction from the sector towards its centre, in sector space.</summary>
        public Vector3 Direction { get; }

        /// <summary>Its size in the sky, in degrees across.</summary>
        public float Size { get; }

        /// <summary>Its size in the sky, in degrees from centre to edge.</summary>
        public float AngularRadius => Size * 0.5f;

        /// <summary>Unit axis it turns about.</summary>
        public Vector3 SpinAxis { get; }

        /// <summary>How far it has turned at the start of the run, in degrees.</summary>
        public float SpinPhase { get; }

        /// <summary>How fast it turns, in degrees a second.</summary>
        public float SpinSpeed { get; }

        /// <summary>
        /// False when no spot met every rule and it took the closest miss. Only the primary and bodies within their kind's
        /// minimum count do; the rest are left out instead.
        /// </summary>
        public bool MetRule { get; }

        /// <summary>How far away a body of <paramref name="radius"/> stands to look this large, in the same units.</summary>
        public float DistanceFor(float radius)
        {
            return radius / Mathf.Sin(Mathf.Max(AngularRadius, 0.01f) * Mathf.Deg2Rad);
        }
    }
}
