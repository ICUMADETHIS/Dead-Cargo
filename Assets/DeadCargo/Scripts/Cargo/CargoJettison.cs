using System;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Ejects cargo from a ship's hold as loose crates.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CargoHold))]
    public sealed class CargoJettison : MonoBehaviour
    {
        [Tooltip("Crate spawned for each jettisoned stack.")]
        [SerializeField] private CargoPickup cratePrefab;

        [Tooltip("Ejection point in ship local space, in metres.")]
        [SerializeField] private Vector3 ejectOffset = new Vector3(0f, 0f, -10f);

        [Tooltip("Speed the crate leaves the ship at, in metres per second.")]
        [SerializeField, Min(0f)] private float ejectSpeed = 6f;

        [Tooltip("Random sideways speed, so crates dumped together drift apart.")]
        [SerializeField, Min(0f)] private float scatterSpeed = 2f;

        [Tooltip("Time before a jettisoned crate can be collected again, in seconds.")]
        [SerializeField, Min(0f)] private float recollectDelay = 2f;

        private CargoHold hold;
        private Rigidbody body;

        /// <summary>Raised for each stack that leaves the hold.</summary>
        public event Action<CargoStack> Jettisoned;

        public bool CanJettison => cratePrefab != null && hold != null && !hold.IsEmpty;

        private void Awake()
        {
            hold = GetComponent<CargoHold>();
            body = GetComponent<Rigidbody>();
        }

        /// <summary>Ejects the stack that frees the most mass.</summary>
        public bool JettisonHeaviest()
        {
            if (!CanJettison)
                return false;

            CargoStack heaviest = hold.Stacks[0];
            for (int i = 1; i < hold.Stacks.Count; ++i)
            {
                if (hold.Stacks[i].Mass > heaviest.Mass)
                    heaviest = hold.Stacks[i];
            }

            return Jettison(heaviest.Item, heaviest.Quantity);
        }

        /// <summary>Ejects every stack in the hold.</summary>
        public void JettisonAll()
        {
            while (CanJettison)
            {
                CargoStack last = hold.Stacks[hold.Stacks.Count - 1];
                Jettison(last.Item, last.Quantity);
            }
        }

        /// <summary>Ejects up to the requested number of units as one crate.</summary>
        public bool Jettison(CargoItemDefinition item, int quantity)
        {
            if (cratePrefab == null)
                return false;

            int removed = hold.Remove(item, quantity);
            if (removed <= 0)
                return false;

            Vector3 position = transform.position + transform.rotation * ejectOffset;
            CargoPickup crate = Instantiate(cratePrefab, position, transform.rotation);
            crate.Initialize(item, removed);
            crate.DelayCollection(recollectDelay);

            if (crate.TryGetComponent(out Rigidbody crateBody))
            {
                Vector3 inherited = body != null ? body.linearVelocity : Vector3.zero;
                Vector3 eject = ejectOffset.normalized * ejectSpeed + UnityEngine.Random.insideUnitSphere * scatterSpeed;
                crateBody.linearVelocity = inherited + transform.rotation * eject;
            }

            Jettisoned?.Invoke(new CargoStack(item, removed));
            return true;
        }
    }
}
