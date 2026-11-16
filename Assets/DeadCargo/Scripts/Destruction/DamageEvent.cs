using UnityEngine;

namespace DeadCargo.Destruction
{
    /// <summary>One hit on something destructible: how much, where, from which way and of what kind.</summary>
    public readonly struct DamageEvent
    {
        /// <summary>Damage points, on the same scale as the kit's weapons.</summary>
        public readonly float Amount;

        /// <summary>World position of the hit.</summary>
        public readonly Vector3 Point;

        /// <summary>World direction the hit travelled in, unit length. Zero when unknown.</summary>
        public readonly Vector3 Direction;

        public readonly DamageKind Kind;

        /// <summary>Size of the affected area in metres, for tools that set it exactly. 0 lets the target decide from the amount.</summary>
        public readonly float Radius;

        /// <summary>The root of whatever dealt the damage. May be null.</summary>
        public readonly Transform Source;

        public DamageEvent(float amount, Vector3 point, Vector3 direction, DamageKind kind, float radius = 0f, Transform source = null)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Kind = kind;
            Radius = radius;
            Source = source;
        }
    }
}
