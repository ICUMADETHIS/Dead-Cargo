using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// A field rock's physics stand-in while it is near the player: a kinematic body with the rock's own shape as a
    /// mesh collider, turning as the rock tumbles, so ships hit it and weapons stop on it. Pooled by
    /// <see cref="FieldRockColliders"/>. It has no renderer: the field draws every rock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldRockProxy : MonoBehaviour, IDestructible
    {
        private FieldRockColliders owner;
        private Rigidbody body;
        private MeshCollider shape;
        private quaternion turn;
        private float3 axis;
        private float rate;

        /// <summary>The rock it stands in for. Only meaningful while <see cref="IsActive"/>.</summary>
        public FieldRock Rock { get; private set; }

        public bool IsActive { get; private set; }

        /// <summary>The rock's place in the field's rock array, or -1 while it waits in the pool.</summary>
        internal int RockIndex { get; private set; } = -1;

        /// <summary>The selection pass that last wanted this rock solid.</summary>
        internal int Stamp { get; set; }

        /// <summary>Makes an idle stand-in under the given parent.</summary>
        /// <param name="surface">Shared by every stand-in: how rock meets hull.</param>
        internal static FieldRockProxy Create(FieldRockColliders owner, Transform parent, int layer, PhysicsMaterial surface)
        {
            var holder = new GameObject("Field Rock") { layer = layer };
            holder.transform.SetParent(parent, false);

            // Idle from the start, so nothing wakes or is cooked until it has a rock. The rigidbody and collider come
            // before the weapon receiver, which looks for both when it wakes.
            holder.SetActive(false);
            var body = holder.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var shape = holder.AddComponent<MeshCollider>();
            shape.cookingOptions = ColliderBakeJob.CookingOptions;
            shape.sharedMaterial = surface;

            var proxy = holder.AddComponent<FieldRockProxy>();
            proxy.owner = owner;
            proxy.body = body;
            proxy.shape = shape;
            holder.AddComponent<DestructionDamageReceiver>().Bind(proxy);
            return proxy;
        }

        public void ApplyDamage(in DamageEvent damage)
        {
            if (owner != null && IsActive)
                owner.ReportDamage(this, damage);
        }

        /// <summary>Takes over a rock: placed, sized and turned exactly as the field draws it.</summary>
        /// <param name="mesh">The rock's shape, already cooked.</param>
        /// <param name="position">The rock's centre, in world space.</param>
        internal void Activate(int rockIndex, in FieldRock rock, in FieldSettings settings, Mesh mesh, Vector3 position, float time)
        {
            FieldRockShape.Motion(rock, settings, out turn, out float3 stretch, out axis, out rate);
            Rock = rock;
            RockIndex = rockIndex;
            IsActive = true;

            // Set while idle, so the collider is built once, when it wakes.
            shape.sharedMesh = mesh;
            transform.localScale = stretch * rock.Radius;
            transform.SetPositionAndRotation(position, RotationAt(time));
            gameObject.SetActive(true);
        }

        /// <summary>Turns the body to where the rock has tumbled to. Called every physics step.</summary>
        internal void Step(float time)
        {
            body.MoveRotation(RotationAt(time));
        }

        /// <summary>Stops standing in, and goes idle until the pool hands it another rock.</summary>
        internal void Deactivate()
        {
            IsActive = false;
            RockIndex = -1;
            gameObject.SetActive(false);
        }

        /// <summary>Lets go of its mesh, which is about to be rebuilt.</summary>
        internal void ClearShape()
        {
            shape.sharedMesh = null;
        }

        private Quaternion RotationAt(float time)
        {
            return FieldRockShape.Rotation(turn, axis, rate, time);
        }
    }
}
