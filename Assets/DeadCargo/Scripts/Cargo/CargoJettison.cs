using System;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Ejects ordinary cargo from a ship's hold as loose crates. The sealed dark matter slot is never ejected.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CargoHold))]
    public sealed class CargoJettison : MonoBehaviour
    {
        // Golden angle, in degrees. Successive crates in a dump land evenly around the ejection point.
        private const float FanStepAngle = 137.5f;

        [Tooltip("Crate spawned for each jettisoned stack.")]
        [SerializeField] private CargoPickup cratePrefab;

        [Tooltip("Ejection point in ship local space, in metres.")]
        [SerializeField] private Vector3 ejectOffset = new Vector3(0f, 0f, -10f);

        [Tooltip("Speed the crate leaves the ship at, in metres per second.")]
        [SerializeField, Min(0f)] private float ejectSpeed = 6f;

        [Tooltip("Sideways speed, at right angles to the ejection, so crates drift apart. In metres per second.")]
        [SerializeField, Min(0f)] private float scatterSpeed = 3f;

        [Tooltip("Distance between crates dumped together by Jettison All, in metres.")]
        [SerializeField, Min(0f)] private float dumpSpacing = 4f;

        [Tooltip("Time before a jettisoned crate can be collected again, in seconds.")]
        [SerializeField, Min(0f)] private float recollectDelay = 2f;

        private CargoHold hold;
        private Rigidbody body;

        /// <summary>Raised for each stack that leaves the hold.</summary>
        public event Action<CargoStack> Jettisoned;

        /// <summary>True when there is ordinary cargo to eject and a crate to put it in.</summary>
        public bool CanJettison => cratePrefab != null && hold != null && FindLastEjectable() >= 0;

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

            int heaviest = -1;
            for (int i = 0; i < hold.Stacks.Count; ++i)
            {
                if (IsEjectable(hold.Stacks[i]) && (heaviest < 0 || hold.Stacks[i].Mass > hold.Stacks[heaviest].Mass))
                    heaviest = i;
            }

            CargoStack stack = hold.Stacks[heaviest];
            return Jettison(stack.Item, stack.Quantity);
        }

        /// <summary>Ejects every ordinary stack. The crates fan out around the ejection point so they separate at once.</summary>
        public void JettisonAll()
        {
            float angle = UnityEngine.Random.Range(0f, 360f);
            while (CanJettison)
            {
                CargoStack last = hold.Stacks[FindLastEjectable()];
                Vector2 fan = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                Eject(last.Item, last.Quantity, fan, dumpSpacing);
                angle += FanStepAngle;
            }
        }

        /// <summary>Ejects up to the requested number of units as one crate. Refuses dark matter, whose slot is sealed.</summary>
        public bool Jettison(CargoItemDefinition item, int quantity)
        {
            return Eject(item, quantity, UnityEngine.Random.insideUnitCircle.normalized, 0f);
        }

        private static bool IsEjectable(CargoStack stack)
        {
            return stack.Item.Slot == CargoSlot.Hold;
        }

        private int FindLastEjectable()
        {
            for (int i = hold.Stacks.Count - 1; i >= 0; --i)
            {
                if (IsEjectable(hold.Stacks[i]))
                    return i;
            }

            return -1;
        }

        // Spawns one crate, offset sideways along the given direction, drifting outward along it.
        // The direction lies in the plane at right angles to the ejection, in ship space.
        private bool Eject(CargoItemDefinition item, int quantity, Vector2 sideways, float offset)
        {
            if (cratePrefab == null || item == null || item.Slot != CargoSlot.Hold)
                return false;

            int removed = hold.Remove(item, quantity);
            if (removed <= 0)
                return false;

            Vector3 ejectDirection = ejectOffset.sqrMagnitude > 0f ? ejectOffset.normalized : Vector3.back;
            Vector3 lateral = SidewaysDirection(ejectDirection, sideways);

            Vector3 localPosition = ejectOffset + lateral * offset;
            CargoPickup crate = Instantiate(cratePrefab, transform.position + transform.rotation * localPosition, transform.rotation);
            crate.Initialize(item, removed);
            crate.DelayCollection(recollectDelay);

            if (crate.TryGetComponent(out Rigidbody crateBody))
            {
                Vector3 inherited = body != null ? body.linearVelocity : Vector3.zero;
                Vector3 eject = ejectDirection * ejectSpeed + lateral * scatterSpeed;
                crateBody.linearVelocity = inherited + transform.rotation * eject;
            }

            Jettisoned?.Invoke(new CargoStack(item, removed));
            return true;
        }

        // Maps a 2D direction onto the plane at right angles to the ejection.
        private static Vector3 SidewaysDirection(Vector3 ejectDirection, Vector2 direction)
        {
            Vector3 right = Vector3.Cross(Vector3.up, ejectDirection);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(ejectDirection, right);

            return (right * direction.x + up * direction.y).normalized;
        }
    }
}
