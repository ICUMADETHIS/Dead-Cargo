using UnityEngine;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// A trail rock's physics stand-in while it is near the camera: a ball with a rigidbody, so ships hit it, shove it
    /// aside and get bumped, and weapons shatter it. Pooled by <see cref="TrailRocks"/>. It has no renderer: every trail
    /// rock is drawn in one instanced batch, from this object's pose while it stands in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrailRockProxy : MonoBehaviour, IDestructible
    {
        private TrailRocks owner;

        /// <summary>This stand-in's slot in the pool.</summary>
        internal int Index { get; private set; }

        /// <summary>The trail rock it stands in for, or -1 while it waits in the pool.</summary>
        internal int Rock { get; set; } = -1;

        /// <summary>Asteroid type of the rock, for its chip colour.</summary>
        internal int Kind { get; private set; }

        internal bool IsActive { get; private set; }
        internal Rigidbody Body { get; private set; }
        internal SphereCollider Ball { get; private set; }

        /// <summary>Makes an idle stand-in under the given parent.</summary>
        /// <param name="surface">Shared by every stand-in: how rock meets hull.</param>
        internal static TrailRockProxy Create(TrailRocks owner, int index, Transform parent, PhysicsMaterial surface)
        {
            var holder = new GameObject("Trail Rock");
            holder.transform.SetParent(parent, false);

            // The rigidbody and collider come before the weapon receiver, which looks for both when it wakes.
            var body = holder.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0.02f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var ball = holder.AddComponent<SphereCollider>();
            ball.sharedMaterial = surface;

            // Lets the trail rocks soften how hard a ship feels their hits; see TrailRocks.
            ball.hasModifiableContacts = true;
            var proxy = holder.AddComponent<TrailRockProxy>();
            proxy.owner = owner;
            proxy.Index = index;
            proxy.Body = body;
            proxy.Ball = ball;
            holder.AddComponent<DestructionDamageReceiver>().Bind(proxy);

            holder.SetActive(false);
            return proxy;
        }

        public void ApplyDamage(in DamageEvent damage)
        {
            if (owner != null && IsActive)
                owner.Shatter(this, damage);
        }

        /// <summary>Takes over a trail rock: placed, sized and moving exactly as the rock was.</summary>
        internal void Activate(int rock, int kind, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 spin,
            float radius, float mass)
        {
            Rock = rock;
            Kind = kind;
            IsActive = true;
            Ball.radius = radius;
            Body.mass = mass;
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            Body.position = position;
            Body.rotation = rotation;
            Body.linearVelocity = velocity;
            Body.angularVelocity = spin;
        }

        /// <summary>Stops standing in. The pool decides when it is free to take another rock.</summary>
        internal void Deactivate()
        {
            IsActive = false;
            gameObject.SetActive(false);
        }
    }
}
