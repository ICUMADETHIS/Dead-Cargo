using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Slows a ship and widens its turns as its cargo hold fills.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CargoHold), typeof(Rigidbody))]
    public sealed class CargoLoadHandling : MonoBehaviour
    {
        [Tooltip("Top speed and acceleration at full load, as a fraction of the empty ship's.")]
        [SerializeField, Range(0.1f, 1f)] private float fullLoadSpeedMultiplier = 0.55f;

        [Tooltip("Turn rate at full load, as a fraction of the empty ship's.")]
        [SerializeField, Range(0.1f, 1f)] private float fullLoadTurnMultiplier = 0.6f;

        private CargoHold hold;
        private Rigidbody body;
        private float emptyMass;
        private float emptyAngularDamping;

        /// <summary>Top speed and acceleration relative to the empty ship, at the current load.</summary>
        public float CurrentSpeedMultiplier => Mathf.Lerp(1f, fullLoadSpeedMultiplier, hold != null ? hold.LoadFraction : 0f);

        /// <summary>Turn-rate multiplier from the current cargo load.</summary>
        public float CurrentTurnMultiplier => Mathf.Lerp(1f, fullLoadTurnMultiplier, hold != null ? hold.LoadFraction : 0f);

        private void Awake()
        {
            hold = GetComponent<CargoHold>();
            body = GetComponent<Rigidbody>();
            emptyMass = body.mass;
            emptyAngularDamping = body.angularDamping;
        }

        private void OnEnable()
        {
            hold.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            hold.Changed -= Apply;
            body.mass = emptyMass;
            body.angularDamping = emptyAngularDamping;
        }

        private void Apply()
        {
            // Kit thrust is a force, so mass scales acceleration and top speed together.
            body.mass = emptyMass / CurrentSpeedMultiplier;

            // Kit steering ignores mass, so turn rate is limited through damping instead.
            body.angularDamping = emptyAngularDamping / CurrentTurnMultiplier;
        }
    }
}
